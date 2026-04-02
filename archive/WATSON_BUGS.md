# Watson Webserver 7.0 - Known Bugs

## 1. Request.Data Stream Read Hangs After ContentLength Bytes Are Consumed

**Severity:** High
**Affected Version:** 7.0.5
**Reproducer:** `src/Test.StreamBug/`

### Description

Reading from `HttpRequest.Data` (the raw request body stream) using the standard `while (Read() > 0)` pattern hangs indefinitely after all `ContentLength` bytes have been consumed. The underlying stream (a `PrefixBufferedStream` wrapping a `NetworkStream`) blocks on `Read()` waiting for more socket data instead of returning 0 (EOF).

### Reproduction

```csharp
// This pattern hangs after reading ContentLength bytes:
int bytesRead;
byte[] buffer = new byte[4096];
while ((bytesRead = ctx.Request.Data.Read(buffer, 0, buffer.Length)) > 0)
{
    // Process bytesRead bytes...
}
// Never reaches here - Read() blocks indefinitely
```

A standalone reproducer is available at `src/Test.StreamBug/`:
```bash
dotnet run --project src/Test.StreamBug/Test.StreamBug.csproj
```

Output:
```
=== Watson Webserver Data Stream Bug Reproduction ===
  DataAsBytes reading... PASS
  Data stream reading (while bytesRead > 0)... FAIL
```

### Root Cause

`HttpRequest.Data` exposes the raw `PrefixBufferedStream` which wraps the TCP `NetworkStream`. After the prefix buffer is drained, reads delegate to the `NetworkStream`. `NetworkStream.Read()` is a blocking call that waits for data from the socket - it only returns 0 when the remote end closes the connection. Since HTTP keep-alive keeps the socket open, the read blocks forever.

The `DataAsBytes` and `DataAsString` properties work correctly because they call `ReadStreamFully(Data, ContentLength)`, which reads exactly `ContentLength` bytes and stops. The raw `Data` stream has no such ContentLength awareness.

### Workaround

Use `DataAsBytes` or `DataAsString` instead of reading from `Data` directly:

```csharp
// Safe: uses ContentLength-aware reading internally
byte[] body = ctx.Request.DataAsBytes;

// Also safe
string bodyStr = ctx.Request.DataAsString;
```

If streaming is required, callers must limit reads to `ContentLength` bytes manually:

```csharp
long bytesRemaining = ctx.Request.ContentLength;
byte[] buffer = new byte[65536];
while (bytesRemaining > 0)
{
    int toRead = (int)Math.Min(buffer.Length, bytesRemaining);
    int bytesRead = ctx.Request.Data.Read(buffer, 0, toRead);
    if (bytesRead <= 0) break;
    bytesRemaining -= bytesRead;
    // Process bytesRead bytes...
}
```

### Affected Code

The `Test.Stream` sample (`src/Test.Stream/Program.cs` line 83) uses the vulnerable pattern:
```csharp
while ((bytesRead = ctx.Request.Data.Read(buffer, 0, buffer.Length)) > 0)
```
This test is manual-only (run, then POST with curl) and was never automated, so the hang was not caught.

### Suggested Fix

Wrap the `Data` stream in a length-limited stream that returns 0 after `ContentLength` bytes, similar to how `DataAsBytes` uses `ReadStreamFully`. This would make the standard `while (Read() > 0)` pattern work correctly without requiring callers to track `ContentLength` manually.
