# WatsonWebserver v6.6.0 Fix Plan

Architectural audit and stability fixes in preparation for HTTP/2 and HTTP/3 support.

## Status Legend
- [ ] Not started
- [x] Complete

---

## Bugs

### Fix #1: Post-authentication content routes call pre-authentication handler
- **Files**: `Webserver.cs` (Watson), `Webserver.cs` (Lite)
- **Issue**: Copy-paste bug. `Routes.PreAuthentication.Content.Handler(ctx)` is called in the post-auth content route block instead of `Routes.PostAuthentication.Content.Handler(ctx)`. This bypasses authentication for content routes.
- **Fix**: Resolved by extracting `ProcessRoutingGroup` method which correctly uses `group.Content.Handler(ctx)`.
- [x] Watson Webserver.cs
- [x] Watson.Lite Webserver.cs

### Fix #2: Static CancellationTokenSource in HttpContextBase
- **File**: `HttpContextBase.cs` (Core)
- **Issue**: `_TokenSource` was `static`, meaning all contexts shared the same CancellationTokenSource. Cancelling one would cancel all. After server stop/start, new contexts get a pre-cancelled token.
- **Fix**: Removed `static` keyword. Each HttpContextBase instance now has its own CancellationTokenSource. Token property changed from field initializer to getter.
- [x] HttpContextBase.cs

### Fix #3: PostRouting invocation is fire-and-forget (no await)
- **Files**: `Webserver.cs` (Watson), `Webserver.cs` (Lite)
- **Issue**: `Routes.PostRouting?.Invoke(ctx).ConfigureAwait(false);` returns a Task that is never awaited. Exceptions are silently lost.
- **Fix**: Now properly awaited with try/catch to prevent PostRouting exceptions from masking the original response.
- [x] Watson Webserver.cs
- [x] Watson.Lite Webserver.cs

---

## Security

### Fix #4: Directory traversal in content route file serving
- **File**: `ContentRouteManager.cs` (Core)
- **Issue**: File path is constructed by concatenating base directory with raw URL without canonicalization.
- **Fix**: Added `Path.GetFullPath()` validation ensuring resolved path starts with base directory. Returns 404 on traversal attempts.
- [x] ContentRouteManager.cs

### Fix #5: No request body size limit
- **Files**: `WebserverSettings.cs`, `HttpRequest.cs` (Lite)
- **Issue**: No maximum Content-Length validation.
- **Fix**: Added `MaxRequestBodySize` setting (default 0, unlimited). Values <= 0 disable the check. Lite validates Content-Length during header parsing and throws IOException if exceeded.
- [x] WebserverSettings.cs (add setting)
- [x] Lite HttpRequest.cs (enforce limit)

### Fix #6: No header count/size limits in Lite
- **File**: `HttpRequest.cs` (Lite)
- **Issue**: Manual header parsing has no bounds on header count.
- **Fix**: Added `MaxHeaderCount` setting (default 64). Values <= 0 disable the check. Lite validates after each header is added.
- [x] WebserverSettings.cs (add setting)
- [x] Lite HttpRequest.cs (enforce limit)

---

## Resource Management

### Fix #7: No IDisposable on context, request, or response base classes
- **Files**: `HttpContextBase.cs`, `HttpRequestBase.cs`, `HttpResponseBase.cs` (Core)
- **Issue**: Base classes hold streams and references but don't implement IDisposable.
- **Fix**: Added IDisposable with virtual Dispose(bool) pattern. HttpContextBase disposes CancellationTokenSource. HttpRequestBase disposes Data stream.
- [x] HttpContextBase.cs
- [x] HttpRequestBase.cs
- [x] HttpResponseBase.cs

### Fix #8: MemoryStream allocations never disposed on error paths
- **Files**: `HttpResponse.cs` (Watson), `HttpResponse.cs` (Lite)
- **Issue**: `Send(string)` and `Send(byte[])` create MemoryStreams without `using` statements.
- **Fix**: Wrapped MemoryStream creation in `using` statements in both implementations.
- [x] Watson HttpResponse.cs
- [x] Lite HttpResponse.cs

### Fix #9: Watson doesn't dispose HttpListenerContext
- **File**: `Webserver.cs` (Watson)
- **Issue**: `HttpListenerContext` is obtained but never explicitly disposed on early error paths.
- **Fix**: Added `listenerCtx.Response.Close()` in finally block when ctx is null.
- [x] Watson Webserver.cs

### Fix #10: Lite's shared TCP stream has no ownership model
- **File**: Covered by Fix #7 (IDisposable) and existing `DisconnectClient` in finally block.
- [x] Covered by Fix #7

### Fix #11: AppendBytes() O(n^2) memory behavior in Lite
- **Files**: `HttpRequest.cs` (Lite), `HttpResponse.cs` (Lite)
- **Issue**: `AppendBytes()` allocates a new array and copies all data on every call.
- **Fix**: Replaced with MemoryStream accumulation in `ReadChunkedBodyAsync()`. Replaced with StringBuilder in `GetHeaderBytes()`. Replaced with direct MemoryStream writes in `SendChunk()`.
- [x] Lite HttpRequest.cs (body reading)
- [x] Lite HttpResponse.cs (header building)

---

## Performance

### Fix #13: Response header building in Lite uses AppendBytes()
- **Fix**: Replaced with StringBuilder-based implementation in `GetHeaderBytes()`.
- [x] Covered by Fix #11 (Lite HttpResponse.cs)

### Fix #14: QueryDetails.Elements re-parses on every access
- **File**: `QueryDetails.cs` (Core)
- **Issue**: The `Elements` property allocates a new `NameValueCollection` on every access.
- **Fix**: Added `_Elements` cache field. Parsed once on first access, cached for subsequent calls.
- [x] QueryDetails.cs

### Fix #15: Route matching re-normalizes paths repeatedly
- **Files**: `StaticRouteManager.cs`, `ContentRouteManager.cs` (Core)
- **Issue**: Path normalization (ToLower, prepend/append `/`) duplicated across Get/Match/Exists/Add.
- **Fix**: Extracted `NormalizePath()` helper in StaticRouteManager. Consolidated normalization in ContentRouteManager.
- [x] StaticRouteManager.cs
- [x] ContentRouteManager.cs

### Fix #16: MaxRequests enforcement uses polling loop
- **File**: `Webserver.cs` (Watson)
- **Issue**: `Task.Delay(100)` polling loop when at capacity.
- **Fix**: Replaced with `SemaphoreSlim` initialized to `MaxRequests`. Uses `WaitAsync()` for backpressure, `Release()` in finally block.
- [x] Watson Webserver.cs

---

## Concurrency

### Fix #17: Route manager lock contention
- **Files**: `StaticRouteManager.cs`, `ParameterRouteManager.cs`, `DynamicRouteManager.cs`, `ContentRouteManager.cs` (Core)
- **Issue**: Exclusive `lock(_Lock)` on all route lookups serializes concurrent requests.
- **Fix**: Replaced `object _Lock` with `ReaderWriterLockSlim`. Read locks for Match/Get/Exists, write locks for Add/Remove.
- [x] StaticRouteManager.cs
- [x] ParameterRouteManager.cs
- [x] DynamicRouteManager.cs
- [x] ContentRouteManager.cs

### Fix #18: Route addition has TOCTOU race condition
- **Files**: `StaticRouteManager.cs`, `ContentRouteManager.cs` (Core)
- **Issue**: `Exists()` acquires/releases lock, then `Add()` acquires lock again.
- **Fix**: Combined Exists+Add into a single atomic operation under one write lock.
- [x] StaticRouteManager.cs
- [x] ContentRouteManager.cs

---

## Architecture

### Fix #20: Duplicated routing pipeline (~300 lines, twice)
- **Files**: `Webserver.cs` (Watson), `Webserver.cs` (Lite)
- **Issue**: Pre-auth and post-auth routing blocks are near-identical copy-paste. This is where Bug #1 came from.
- **Fix**: Extracted `ProcessRoutingGroup(HttpContextBase ctx, RoutingGroup group, string authPhase)` method in both implementations. Pre-auth and post-auth blocks now each call this method with the appropriate RoutingGroup.
- [x] Watson Webserver.cs
- [x] Lite Webserver.cs

### Fix #22: Chunked transfer is protocol-version-specific but not abstracted
- **Files**: `HttpRequestBase.cs`, `HttpResponseBase.cs` (Core)
- **Issue**: Chunked encoding is HTTP/1.1-specific. HTTP/2 uses DATA frames.
- **Fix**: Added XML doc annotations to `ChunkedTransfer`, `ReadChunk()`, and `SendChunk()` documenting they are HTTP/1.1 specific. Full abstraction deferred to HTTP/2 implementation.
- [x] HttpRequestBase.cs (document/annotate)
- [x] HttpResponseBase.cs (document/annotate)

---

## Tests Updated
- [x] Added `TestQueryDetailsCaching` - verifies Elements property returns cached instance
- [x] Added `TestMaxRequestBodySizeSetting` - verifies default, configurable, and disable behavior
- [x] Added `TestMaxHeaderCountSetting` - verifies default, configurable, and disable behavior
- [x] Added directory traversal test to `TestComprehensiveRouting` - verifies `../../` returns 404

---

## Not Fixing (by design)

### Item #12: Byte-by-byte chunk length reading in Lite
- **Reason**: Reading up to a delimiter requires reading until you find it. The current approach is correct; optimizing with a larger read buffer would require managing leftover bytes that belong to the chunk body, adding complexity without clear benefit.

### Item #19: 1:1 connection-to-request assumption (HTTP/2 blocker)
- **Reason**: Architectural change required for HTTP/2. Tracked separately as part of HTTP/2 implementation planning.

### Item #21: No transport layer abstraction
- **Reason**: Architectural change required for HTTP/2/3. Tracked separately.
