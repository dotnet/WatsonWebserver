# Bug fix plan: an empty final chunk writes the chunked terminator twice

Target release: **7.3.1** (patch). This file is a work order for the agent doing the change. Delete it when the release ships.

## The problem

On HTTP/1.1, `HttpResponse.SendChunk(byte[] chunk, bool isFinal, CancellationToken token)` (`src/WatsonWebserver/HttpResponse.cs`) writes a terminating chunk twice when it is called with an empty chunk and `isFinal: true`. The README tells callers to finish a chunked response exactly that way:

```csharp
await ctx.Response.SendChunk(Array.Empty<byte>(), true, ctx.Token);
```

The raw (non-http.sys) path writes:

1. the chunk header `chunk.Length.ToString("X") + "\r\n"`, which for an empty chunk is `0\r\n`;
2. the chunk data (none) and the trailing `\r\n`. Together with step 1, that is already a complete terminating chunk, `0\r\n\r\n`;
3. then, because `isFinal` is true, another `0\r\n\r\n`.

So the response ends with `0\r\n\r\n0\r\n\r\n`. Five bytes follow the end of the HTTP message. What happens next depends on the client:

- Node.js clients (Postman/Newman, anything built on `http` or `undici`) fail the request with `Parse Error: Expected HTTP/`. Their parser reads the extra bytes as the start of another response.
- With keep-alive enabled (`Settings.IO.EnableKeepAlive`), a client that reuses the connection reads `0\r\n\r\n` as the start of its next response.
- curl reports `Leftovers after chunking: 5 bytes` and carries on.

The case found in production: Garrison relays a bodyless `202 Accepted` (an MCP notification answer) with `ChunkedTransfer = true` and a single `SendChunk(Array.Empty<byte>(), true)`. The wire bytes were:

```
HTTP/1.1 202 Accepted\r\nTransfer-Encoding: chunked\r\n...\r\n\r\n0\r\n\r\n0\r\n\r\n
```

Garrison works around this today by not sending empty final chunks (Garrison `src/Garrison.Server/Protocols/McpClusterProxy.cs`, `RelayAsync`). Its regression test, `e2e/mcp-response-framing`, shows the 5 trailing bytes without the workaround.

A non-empty final chunk (`SendChunk(data, true)`) is correct today: it writes `len\r\ndata\r\n0\r\n\r\n`.

## The fix

In the raw HTTP/1.1 branch of `HttpResponse.SendChunk`, when the chunk is empty and `isFinal` is true, write only the terminator `0\r\n\r\n`. Do not write a zero-length data chunk followed by a second terminator. In outline:

```csharp
if (chunk == null || chunk.Length < 1)
{
    // An empty final chunk is the terminator itself.
    payload = "0\r\n\r\n";
}
else
{
    payload = len + "\r\n" + data + "\r\n" + (isFinal ? "0\r\n\r\n" : "");
}
```

Keep the existing early return for an empty non-final chunk, which writes nothing. Leave the http.sys branch (`_Response != null`), the header handling, `ContentLength` accounting, flushing, keep-alive and close decisions, `MarkResponseCompleted`, and the return values exactly as they are.

## Why this is safe for every existing user

Each item below is a requirement. If the change can't meet one, stop and report back instead of shipping.

1. **No API change.** No public signature, type, setting or default changes. Code built against 7.3.0 compiles and binds against 7.3.1 unchanged.
2. **Bytes change in exactly one case.** Only HTTP/1.1 raw-transport responses that end with an empty final chunk change, and only by dropping the 5 invalid trailing bytes. A non-empty final chunk, a non-final chunk, `Send(...)` with a body, HTTP/2, HTTP/3, server-sent events and the http.sys path must stay byte-identical. Prove it with tests (below).
3. **The new bytes are strictly the RFC 9112 form.** A chunked body ends with `last-chunk` (`0\r\n`), optional trailers, and `CRLF`. Every compliant client already accepts this; the double terminator is what some clients reject. No correct client can depend on the extra bytes.
4. **Check HTTP/2 and HTTP/3; don't change them.** Confirm that `Http2Response.SendChunk` and `Http3Response.SendChunk` with an empty final chunk end the stream correctly (a DATA frame with END_STREAM, or an equivalent, and no extra empty frame that matters). If either has a similar problem, report it in the release notes as a separate finding. Don't fix it in this patch unless the fix is equally narrow and byte-identical for every other case.
5. **No new dependencies or target framework changes.** AOT and trim analyzers stay at zero warnings (`TreatWarningsAsErrors` is on).

## Tests (Touchstone, in `src/Test.Shared`, registered in `WatsonTestSuites.cs`)

Add raw-socket cases that send a request over a `TcpClient` and capture the exact response bytes. The existing `RawHttpResponse` helper or `ProtocolGapSharedTests` may already do part of this.

- An empty final chunk after two data chunks: the body is exactly `7\r\npart 1\n\r\n7\r\npart 2\n\r\n0\r\n\r\n`, and no bytes follow it. Read until the server closes the connection or a short timeout passes, and assert zero extra bytes.
- A single empty final chunk with status 202 and no data chunks: the body is exactly `0\r\n\r\n`, with no extra bytes.
- A non-empty final chunk: the bytes are unchanged from 7.3.0 (`len\r\ndata\r\n0\r\n\r\n`).
- With `EnableKeepAlive = true` and a request carrying `Connection: keep-alive`: send two requests on one connection to a route that ends with an empty final chunk. Both responses parse, and the second status line is `HTTP/1.1 200`.
- An HTTP/2 empty final chunk (h2c or TLS, whichever the existing HTTP/2 smoke tests use): the stream ends cleanly and the body is unchanged.

Every existing case must still pass unchanged, including `SharedLegacySmokeTests`, `SharedCoreUnitTests` (the chunk cases near the `finalSendResult` assertions), `SharedHttp2SmokeTests` and `LegacyCoverageSuite`. Don't edit an existing test to make it pass.

Also run the manual interop projects that exercise chunking, if they run headless: `Test.ChunkServer` and `Test.CurlInterop`.

## Release steps

1. Bump `<Version>` in `src/WatsonWebserver/WatsonWebserver.csproj` from 7.3.0 to 7.3.1. The repository owner approved this version change for this release. `Watson.Clients` is versioned in lockstep with `Watson` (see the 7.3.0 changelog entry), so bump it to 7.3.1 too, with no code changes. If the lockstep convention turns out not to apply to patch releases, leave `Watson.Clients` alone and say so in the report.
2. Update `CHANGELOG.md`: set the current version to `v7.3.1` and add a `## v7.3.1` entry in the style of earlier ones, along these lines:

   > Fixed: on HTTP/1.1, finishing a chunked response with an empty final chunk (`SendChunk(Array.Empty<byte>(), true)`, the documented pattern) wrote the terminating chunk twice. Node.js clients failed with "Expected HTTP/", and kept-alive connections read the extra bytes as the next response.
3. README: the chunking example can stay as is, since it becomes correct. Optionally add one sentence saying that the final call may carry data or be empty.
4. Build in Release with zero warnings and errors. Run `Test.Automated`, `Test.XUnit` and `Test.Nunit` on every target they run on, plus the `Test.Aot` native publish and run.
5. Follow `CLAUDE.md` in this repository for code style and test placement. Don't use em-dashes anywhere.
6. Commit and publish the package only when the repository owner asks for it.

## Downstream follow-ups (not part of this repository)

- Voltaic references `Watson` 7.3.0. Its MCP HTTP server uses `HttpListener`, not Watson, but `A2AGrpcServer` ends gRPC streams with `SendChunk(Array.Empty<byte>(), true)` on Watson. gRPC runs over HTTP/2, so requirement 4 (checking the HTTP/2 empty final chunk) covers it. If that check finds an HTTP/2 problem, Voltaic needs a bump to the fixed Watson as well.
- Garrison (`~/Code/Garrison`) references `Watson` 7.3.0. After 7.3.1 is published, Garrison can bump the package. Its proxy workaround (buffering non-stream responses, `Connection: close` on event streams) can stay; it is harmless. Its `e2e/mcp-response-framing` test keeps guarding the behavior either way.
