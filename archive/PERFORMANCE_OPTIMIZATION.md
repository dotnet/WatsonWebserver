# WatsonWebserver 7.0 — Performance Optimization Plan

> **Goal**: Close the ~2x throughput gap to Kestrel under keep-alive HTTP/1.1 workloads.
> **Baseline** (Test.Benchmark, .NET 10.0.4, HTTP/1.1): Watson ~62k req/s (hello), ~74k req/s (json) vs Kestrel ~146k/~139k.
> **Rule**: Each change is implemented, benchmarked, and reviewed independently. No change is kept without measured improvement. No change that regresses common-case performance is acceptable.

---

## How to Use This Document

1. Before starting any item, run `Test.Benchmark` and record the **Before** baseline.  
2. Implement the change in isolation (one item per branch/commit).
3. Run `Test.Benchmark` again and record the **After** results.
4. Fill in the tracking table for that item.
5. Present before/after to the reviewer with your recommendation (keep/discard).
6. The reviewer decides. Mark the item accordingly.
7. Every run of `Test.Benchmark` must include Watson6, Watson6.Lite, Watson7, and Kestrel.

### Metrics to Capture Per Item

| Metric | Description |
|--------|-------------|
| **Req/s (hello)** | Plaintext requests per second |
| **Req/s (json)** | JSON requests per second |
| **Throughput (MB/s)** | Aggregate bytes per second |
| **P50 latency** | Median response time |
| **P99 latency** | Tail response time |
| **GC Gen0 collections** | Allocation pressure indicator |

---

## Phase 1 — Immediate, Narrow, Safe

These are low-risk, high-impact changes targeting the current hot path. No architectural changes required.

---

### 1. Eliminate response header allocation chain

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [x] Discarded
- **Priority**: Highest
- **Files**: `src/WatsonWebserver/HttpResponse.cs` (lines ~745-851)
- **Problem**: Every response allocates a `new ArrayBufferWriter<byte>(256)`, serializes headers into it, calls `WrittenMemory.ToArray()` (allocating a fresh `byte[]` and copying), then `BlockCopy`s headers + payload into yet another rented buffer. Three allocations and two copies per response that produce zero value.
- **Fix**: Replace `ArrayBufferWriter` + `ToArray()` with a single `ArrayPool<byte>.Shared.Rent()` buffer. Write headers directly into it. For the small-response fast path, write headers + payload into the same rented buffer in one pass — no intermediate copy. Return the buffer after the stream write.
- **Kestrel comparison**: Kestrel writes headers directly into transport-owned buffers with zero intermediate copies.
- **Risk**: Low. The change is confined to `GetHeaderBytes()`, `GetSimpleHeaderBytes()`, and `WriteSmallResponseAsync()`.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 91,632.48 | 91,428.85 | -203.63 |
| Req/s (json) | 69,174.44 | 68,685.87 | -488.57 |
| Throughput (MB/s) | 357.94 MiB/s | 357.14 MiB/s | -0.80 MiB/s |
| P50 latency | 0.22 ms | 0.22 ms | 0.00 ms |
| P99 latency | 2.48 ms | 2.90 ms | +0.42 ms |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Discard. Managed allocation dropped (6.10 GiB -> 5.56 GiB for Watson7 hello), but throughput regressed slightly on both common-case scenarios._
**Decision**: _Discard._
**Notes**: _Implementation was tested and reverted. The benchmark harness reports managed allocation, not Gen0 collections._

---

### 2. Fix `Http1ChunkReader` single-byte reads

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [x] Discarded
- **Priority**: Highest
- **Files**: `src/WatsonWebserver.Core/Http1/Http1ChunkReader.cs` (lines ~29, ~92)
- **Problem**: Reads chunk size lines one byte at a time: `byte[] buffer = new byte[1]` in a loop. Each iteration is a separate syscall (kernel transition) AND a `byte[]` allocation. For a chunk header like `1A2B\r\n`, that is 6 syscalls and 6 allocations where 1 of each would suffice.
- **Fix**: Read into a pooled buffer (e.g., 128 bytes from `ArrayPool`), scan for `\r\n` in the buffer. Parse the hex chunk size from the buffered data. Same pattern already used in `Http1HeaderReader`.
- **Kestrel comparison**: Kestrel reads chunks from the `PipeReader` buffer — zero extra syscalls.
- **Risk**: Low-medium. Chunked transfer encoding has edge cases (trailers, extensions). Test with varied chunk sizes including 0-length terminal chunk.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | N/A | N/A | N/A |
| Req/s (json) | N/A | N/A | N/A |
| Throughput (MB/s) | 53.15 MiB/s total (`ChunkedEcho`, Watson7 baseline, 3s/8c) | Canceled / timed out | Regression |
| P50 latency | 0.94 ms (`ChunkedEcho`, Watson7 baseline, 3s/8c) | Canceled / timed out | Regression |
| P99 latency | 3.45 ms (`ChunkedEcho`, Watson7 baseline, 3s/8c) | Canceled / timed out | Regression |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Discard. Once `Test.Benchmark` was extended with an HTTP/1.1 `ChunkedEcho` scenario, the optimized implementation caused Watson7 chunked runs to hang/cancel under load, while the pre-change reader completed successfully._
**Decision**: _Discard._
**Notes**: _The benchmark harness now includes `ChunkedEcho` for HTTP/1.1 only. The optimized reader plus replay stream passed simple smoke tests but regressed under benchmark load; the code has been reverted to the original implementation._

---

### 3. Deduplicate and optimize route normalization

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [x] Kept / [ ] Discarded
- **Priority**: High
- **Files**: `src/WatsonWebserver.Core/UrlDetails.cs` (line ~139, ~162), `src/WatsonWebserver.Core/Routing/StaticRouteManager.cs` (line ~20, ~170)
- **Problem**: `UrlDetails.NormalizedRawWithoutQuery` lowercases and normalizes the path per request. Then `StaticRouteManager` normalizes route paths *again* under a `ReaderWriterLockSlim` on every lookup. Double normalization plus lock acquisition on every request.
- **Fix**:
  - Normalize the URL path once in `UrlDetails` and reuse the result everywhere.
  - Replace `ReaderWriterLockSlim` in `StaticRouteManager` with `FrozenDictionary<string, ...>` (.NET 8+) for zero-overhead lookups. Routes are registered at startup and rarely change — perfect use case.
  - Fallback: `ConcurrentDictionary` if `FrozenDictionary` is not feasible for the target framework.
- **Kestrel comparison**: Kestrel uses a prefix-tree with pre-normalized keys; no per-request normalization or locking.
- **Risk**: Medium. Route matching is correctness-critical. Ensure case-insensitive matching is preserved. Test with parameterized, dynamic, and static routes.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 84,029.02 | 85,601.05 | +1,572.03 |
| Req/s (json) | 63,806.75 | 65,614.40 | +1,807.65 |
| Throughput (MB/s) | 328.24 MiB/s | 334.38 MiB/s | +6.14 MiB/s |
| P50 latency | 0.23 ms | 0.23 ms | 0.00 ms |
| P99 latency | 3.05 ms | 3.26 ms | +0.21 ms |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Keep. Watson7 improved on both common-case scenarios after removing redundant static-route normalization and replacing read-side static-route lookups with immutable snapshots._
**Decision**: _Keep._
**Notes**: _Implemented a shared path-normalization helper in `UrlDetails`, switched static-route reads from `ReaderWriterLockSlim` to frozen snapshots, and added a normalized-path fast path from `WebserverBase` into `StaticRouteManager`. Route-count scaling beyond the benchmark harness default is still unmeasured._

---

### 4. Cache `JsonSerializerOptions` as a static field

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Priority**: High
- **Files**: `src/WatsonWebserver.Core/DefaultSerializationHelper.cs` (line ~45)
- **Problem**: `new JsonSerializerOptions()` is created on every `SerializeJson()` call. The options object builds internal reflection caches that are discarded each time. Microsoft documents this as a major performance anti-pattern with 10-100x overhead.
- **Fix**: One `private static readonly JsonSerializerOptions` field, initialized once. Use it in all serialization calls.
- **Kestrel comparison**: N/A (Kestrel doesn't serialize JSON itself), but this is a .NET best practice.
- **Risk**: Minimal. Ensure the options are not mutated after construction (they become immutable after first use by `System.Text.Json` anyway).

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | N/A | N/A | N/A |
| Req/s (json) | N/A | N/A | N/A |
| Throughput (MB/s) | 111.97 MiB/s total (`SerializeJson`, Watson7) | 150.47 MiB/s total (`SerializeJson`, Watson7) | +38.50 MiB/s |
| P50 latency | 0.43 ms (`SerializeJson`, Watson7) | 0.25 ms (`SerializeJson`, Watson7) | -0.18 ms |
| P99 latency | 6.52 ms (`SerializeJson`, Watson7) | 5.90 ms (`SerializeJson`, Watson7) | -0.62 ms |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Keep. A dedicated `SerializeJson` scenario was added to `Test.Benchmark` to exercise `DefaultSerializationHelper.SerializeJson()`, and caching serializer options improved Watson7 from 28,665 req/s to 38,520 req/s on that path._
**Decision**: _Keep._
**Notes**: _The standard `json` benchmark route does not use `DefaultSerializationHelper`, so a new `SerializeJson` scenario was added for all targets. The before/after comparison for this item is based on Watson7, which is the only target using the current helper implementation in this repository._

---

## Phase 2 — Medium Effort, High Impact

These require more design work but deliver significant improvements.

---

### 5. Pre-serialize common response header templates

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Priority**: High
- **Files**: `src/WatsonWebserver/HttpResponse.cs`
- **Problem**: Every response serializes the status line, Server header, Date header, Content-Type, and Content-Length individually — string formatting, encoding, and writing each header one at a time.
- **Fix**: For common response profiles (e.g., `200 OK` + `application/json` + `Server: Watson`), pre-compute the header block as a `byte[]` template. At response time, copy the template and patch only the variable parts (Content-Length value, Date value). This turns N header writes into one `BlockCopy` + two small patches.
- **Kestrel comparison**: Kestrel pre-computes known header bytes and writes them as contiguous blocks.
- **Risk**: Medium. Must handle custom headers correctly (fall back to per-header serialization when the template doesn't match). Cache invalidation if server name changes at runtime.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 61,420.50 | 70,270.88 | +8,850.38 |
| Req/s (json) | 49,638.88 | 58,089.99 | +8,451.11 |
| Throughput (MB/s) | 239.92 MiB/s | 274.50 MiB/s | +34.58 MiB/s |
| P50 latency | 0.26 ms | 0.26 ms | 0.00 ms |
| P99 latency | 5.53 ms | 4.15 ms | -1.38 ms |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Keep. Caching the serialized simple-response header prefix improved Watson7 on both common-case benchmark scenarios and reduced tail latency._
**Decision**: _Keep._
**Notes**: _Implemented on the existing simple-response path only: cached serialized prefix for protocol + status + content-type, leaving `Content-Length` and `Date` dynamic. The benchmark harness does not measure template hit rate with custom headers, and Watson6/Lite6 numbers were noisy because they run out-of-process legacy hosts._

---

### 6. Pool `MemoryStream` instances via `RecyclableMemoryStreamManager`

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Priority**: High
- **Files**: All files that `new MemoryStream()` for request/response body accumulation
- **Problem**: Every request/response body that uses `MemoryStream` allocates fresh internal `byte[]` buffers that grow via doubling. Large bodies hit the Large Object Heap (LOH), causing expensive Gen2 collections.
- **Fix**: Replace `new MemoryStream()` with `RecyclableMemoryStreamManager.GetStream()`. The manager pools internal buffers and avoids LOH allocations. Microsoft.IO.RecyclableMemoryStream is a NuGet package maintained by Microsoft.
- **Kestrel comparison**: Kestrel avoids `MemoryStream` entirely (uses Pipelines), but `RecyclableMemoryStream` is the standard mitigation for code that still needs the `Stream` API.
- **Risk**: Low. Drop-in replacement. Ensure streams are properly disposed (they already should be).

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | N/A | N/A | N/A |
| Req/s (json) | N/A | N/A | N/A |
| Throughput (MB/s) | 1,237.12 MiB/s total (`Echo` + `JsonEcho`, Watson7, HTTP/1.1+2+3) | 990.23 MiB/s total (`Echo` + `JsonEcho`, Watson7, HTTP/1.1+2+3) | -246.89 MiB/s |
| P50 latency | Watson7 `Echo`: HTTP/1.1 `0.21 ms`, HTTP/2 `0.94 ms`, HTTP/3 `0.83 ms` | Watson7 `Echo`: HTTP/1.1 `0.21 ms`, HTTP/2 `1.43 ms`, HTTP/3 `1.18 ms` | Regressed on HTTP/2 and HTTP/3 |
| P99 latency | Watson7 `JsonEcho`: HTTP/1.1 `2.03 ms`, HTTP/2 `1.86 ms`, HTTP/3 `2.19 ms` | Watson7 `JsonEcho`: HTTP/1.1 `2.85 ms`, HTTP/2 `3.27 ms`, HTTP/3 `3.33 ms` | Regressed |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Discard. Replacing body-buffering `MemoryStream` allocations with `RecyclableMemoryStreamManager` regressed Watson7 on the body-bearing benchmark paths across HTTP/1.1, HTTP/2, and HTTP/3._
**Decision**: _Discard._
**Notes**: _Benchmarked with `Echo` and `JsonEcho` across all targets and all supported protocols. Watson6 and WatsonLite6 remain HTTP/1.1-only in the harness; Watson7 and Kestrel were measured on HTTP/1.1, HTTP/2, and HTTP/3. The implementation was reverted after benchmarking so the working tree remains on the kept baseline._

---

### 7. Pool HttpContext / HttpRequest / HttpResponse

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Priority**: High
- **Files**: `src/WatsonWebserver/Webserver.cs` (line ~569), `src/WatsonWebserver/HttpContext.cs`, `src/WatsonWebserver.Core/HttpContextBase.cs`, `src/WatsonWebserver.Core/HttpRequestBase.cs`, `src/WatsonWebserver.Core/HttpResponseBase.cs`
- **Problem**: Every request in the keep-alive loop allocates `new HttpContext` → `new HttpRequest` → `new HttpResponse`, plus their internal lazy fields (Timestamp, Guid, CancellationTokenSource, ConnectionMetadata, StreamMetadata, closure factories via `SetConnectionFactory`/`SetStreamFactory`).
- **Fix**:
  1. Add `IReset` interface (or `Reset()` method) to `HttpContext`, `HttpRequest`, `HttpResponse`.
  2. Each `Reset()` must clear: all lazy fields, closure factory references, per-request state, CancellationTokenSource (use `TryReset()` on .NET 8+).
  3. Use `ObjectPool<T>` (from `Microsoft.Extensions.ObjectPool`) or a simple `ConcurrentBag<T>`-based pool.
  4. Rent at request start (line 569), return after response completes.
  5. **Design constraint**: The `Reset()` contract and object shape should anticipate a future Pipelines migration (e.g., abstract the I/O surface so `Reset()` doesn't need a full rewrite when `NetworkStream` becomes `PipeReader`/`PipeWriter`).
- **Kestrel comparison**: Kestrel pools all request/response objects.
- **Risk**: Medium-high. Leaking state between requests is a security and correctness risk. Every field must be verified as cleared. Thorough testing required.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 69,491.75 | 80,288.74 | +10,796.99 |
| Req/s (json) | 58,187.41 | 73,255.15 | +15,067.74 |
| Throughput (MB/s) | 498.74 MiB/s total (Watson7 `Hello` + `Json`) | 599.78 MiB/s total (Watson7 `Hello` + `Json`) | +101.04 MiB/s |
| P50 latency | 0.15 ms (`Hello`, Watson7) | 0.14 ms (`Hello`, Watson7) | -0.01 ms |
| P99 latency | 2.74 ms (`Hello`, Watson7) | 1.76 ms (`Hello`, Watson7) | -0.98 ms |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Keep. Pooling the HTTP/1.1 `HttpContext`/`HttpRequest`/`HttpResponse` objects improved Watson7 materially on the keep-alive `Hello` and `Json` benchmarks._
**Decision**: _Keep._
**Notes**: _Implementation is intentionally scoped to the HTTP/1.1 keep-alive loop, which is where these exact types are allocated today. A follow-up `Echo`/`JsonEcho` smoke benchmark completed with zero failures after the pooling change, which reduces concern about stale per-request body state, but HTTP/2 and HTTP/3 still allocate their own request/response objects. This item therefore improves only the HTTP/1.1 portion of the three-protocol plan._

---

### 8. Replace per-chunk `byte[]` allocations with `stackalloc` + `Utf8Formatter`

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Priority**: Medium-high
- **Files**: `src/WatsonWebserver/HttpResponse.cs` (lines ~269-276)
- **Problem**: Each chunk write does `Encoding.UTF8.GetBytes(chunk.Length.ToString("X") + "\r\n")` — allocating a string from `ToString("X")`, a concatenated string, and a `byte[]` from `GetBytes()`. Three allocations per chunk.
- **Fix**: Use `Span<byte>` with `stackalloc byte[16]` and `Utf8Formatter.TryFormat()` to write the hex chunk length directly. Append `\r\n` bytes manually. Zero allocations.
- **Kestrel comparison**: Kestrel writes chunk headers directly into pipe buffers with no allocations.
- **Risk**: Low. Small, contained change.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | N/A | N/A | N/A |
| Req/s (json) | N/A | N/A | N/A |
| Throughput (MB/s) | 8.35 MiB/s (`ChunkedResponse`, Watson7, HTTP/1.1) | 1.66 MiB/s (`ChunkedResponse`, Watson7, HTTP/1.1) | -6.69 MiB/s |
| P50 latency | 6.62 ms (`ChunkedResponse`, Watson7, HTTP/1.1) | 11.63 ms (`ChunkedResponse`, Watson7, HTTP/1.1) | +5.01 ms |
| P99 latency | 23.03 ms (`ChunkedResponse`, Watson7, HTTP/1.1) | 33.35 ms (`ChunkedResponse`, Watson7, HTTP/1.1) | +10.32 ms |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Discard. A dedicated HTTP/1.1 `ChunkedResponse` scenario was added to `Test.Benchmark`, and the attempted optimization regressed Watson7 badly enough to introduce benchmark failures under load._
**Decision**: _Discard._
**Notes**: _This item is HTTP/1.1-specific by design. The benchmark harness now includes `ChunkedResponse` for all HTTP/1.1 targets, and the implementation was reverted after the failed run so the codebase remains on the item-7 baseline._

---

## Phase 3 — Structural / Architectural

This is the ceiling-raiser. It is a firm commitment, not optional, but it should be done after Phases 1-2 stabilize.

---

### 9. Migrate to `System.IO.Pipelines` (`PipeReader` / `PipeWriter`)

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Priority**: High (but sequenced after Phases 1-2)
- **Files**: Entire I/O layer — `Http1HeaderReader`, `Http1ChunkReader`, `HttpResponse` write paths, `Webserver` connection handling
- **Problem**: The current `TcpClient`/`NetworkStream` model requires manual buffer management, explicit `ArrayPool` rent/return, and separate read-parse-copy steps. Pipelines provide zero-copy `ReadOnlySequence<byte>` parsing, built-in backpressure, and automatic buffer lifecycle management.
- **Fix**: Replace `NetworkStream` read/write with `PipeReader`/`PipeWriter`. Rewrite header parsing to operate on `ReadOnlySequence<byte>`. Rewrite response writing to use `PipeWriter.GetMemory()`/`Advance()`.
- **Kestrel comparison**: Kestrel's entire I/O model is built on Pipelines. This is what enables its throughput ceiling.
- **Risk**: High. This touches the entire I/O layer. Multi-week effort. Must be thoroughly tested. Phase 1-2 pooling designs should anticipate this migration.
- **Expected impact**: 30-50% throughput improvement on the read path. Closes the remaining gap to Kestrel that per-request allocation fixes cannot reach.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 80,288.74 | 60,101.86 | -20,186.88 |
| Req/s (json) | 73,255.15 | 59,548.64 | -13,706.51 |
| Throughput (MB/s) | 599.78 MiB/s total (Watson7 `Hello` + `Json`, HTTP/1.1) | 467.38 MiB/s total (Watson7 `Hello` + `Json`, HTTP/1.1) | -132.40 MiB/s |
| P50 latency | 0.14 ms (`Hello`, Watson7) | 0.16 ms (`Hello`, Watson7) | +0.02 ms |
| P99 latency | 1.76 ms (`Hello`, Watson7) | 3.41 ms (`Hello`, Watson7) | +1.65 ms |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Discard this incremental slice. A first HTTP/1.1-only read-side `PipeReader` migration (replacing the keep-alive header read + prefix replay path) benchmarked worse than the current stream-based implementation._
**Decision**: _Discard._
**Notes**: _This is still the single largest architectural change and likely needs a dedicated feature branch. The benchmarked attempt did not migrate the full I/O stack; it only replaced the HTTP/1.1 header read path with a `PipeReader` while leaving request-body and response writes on streams. That narrower slice was reverted after benchmarking, so the codebase remains on the item-8 baseline plus the new chunked-response benchmark scenario._

---

## Phase 4 — Polish and Long-Tail Optimizations

Lower individual impact, but cumulative effect is meaningful. Implement after Phases 1-3 are stable.

---

### 10. Replace `string.Split()` in URL/query parsing with `Span<T>`-based tokenization

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Files**: `src/WatsonWebserver.Core/QueryDetails.cs`, `src/WatsonWebserver.Core/UrlDetails.cs`
- **Problem**: `rawUrl.Split(new char[] { '/' }, ...)` allocates the `char[]` separator, the `string[]` result, and each substring.
- **Fix**: Use `ReadOnlySpan<char>.IndexOf('/')` in a loop. Allocate only the final results needed.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 88,415.36 | 86,246.10 | -2,169.26 |
| Req/s (json) | 70,254.42 | 69,915.81 | -338.61 |
| P99 latency | 2.88 ms (`Hello`, Watson7, HTTP/1.1) | 2.86 ms (`Hello`, Watson7, HTTP/1.1) | -0.02 ms |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Discard. A direct Watson7 HTTP/1.1 A/B against the item-7 baseline showed a small but repeatable throughput regression after replacing the current URL/query parsing with the span-based tokenizer attempt. The code has been reverted._ **Decision**: _Discard._

---

### 11. Use `ValueTask` on response write path

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Files**: `src/WatsonWebserver/HttpResponse.cs` — `SendPayloadAsync()` and related methods
- **Problem**: `Task` return type allocates when the write completes synchronously (common with small buffered responses). `ValueTask` avoids this.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 88,415.36 | 75,711.64 | -12,703.72 |
| Req/s (json) | 70,254.42 | 57,428.82 | -12,825.60 |
| P99 latency | 2.88 ms (`Hello`, Watson7, HTTP/1.1) | 4.38 ms (`Hello`, Watson7, HTTP/1.1) | +1.50 ms |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Discard. A narrow item-11 implementation that converted the internal response write helpers to `ValueTask` and switched them to the `Memory<byte>` stream overloads regressed Watson7 materially on both `Hello` and `Json`. The code has been reverted._ **Decision**: _Discard._

---

### 12. Replace `NameValueCollection` with struct-based header collection

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Files**: `src/WatsonWebserver.Core/Http1/Http1RequestMetadata.cs`, header-related types
- **Problem**: `NameValueCollection` uses `ArrayList` internally and allocates strings for every key/value. For HTTP/1.1, the lazy `HeaderSlice` path defers this cost, so urgency is lower. Primary benefit is for HTTP/2/3 and header-heavy workloads.
- **Fix**: Custom header collection with known-header slots (Content-Type, Content-Length, Host, etc.) as `StringValues` fields. Small dictionary only for uncommon headers.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 7,392.54 (`HTTP/2`, Watson7) / 9,850.46 (`HTTP/3`, Watson7) | 12,280.27 (`HTTP/2`, Watson7) / 13,653.86 (`HTTP/3`, Watson7) | Improved |
| Req/s (json) | 8,129.10 (`HTTP/2`, Watson7) / 9,688.38 (`HTTP/3`, Watson7) | 12,902.44 (`HTTP/2`, Watson7) / 13,438.84 (`HTTP/3`, Watson7) | Improved |
| P99 latency | 10.26 ms (`HTTP/2 Hello`) / 16.06 ms (`HTTP/3 Hello`) | 4.69 ms (`HTTP/2 Hello`) / 15.15 ms (`HTTP/3 Hello`) | Improved |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Keep. A contained item-12 slice replaced eager HTTP/2 and HTTP/3 request-header `NameValueCollection` materialization with deferred `HttpHeaderField[]` storage plus lazy materialization only when `Request.Headers` is accessed. That materially improved Watson7 on both `Hello` and `Json` for HTTP/2 and HTTP/3 in direct A/B runs._ **Decision**: _Keep._
**Notes**: _This is not a full public API replacement of `NameValueCollection`; it is a deferred-materialization slice focused on the request hot path for HTTP/2 and HTTP/3. XML-doc warnings remain in the touched request classes because the new internal-first shape was not fully re-documented during the benchmark pass._

---

### 13. Pool `CancellationTokenSource` instances

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Files**: Wherever `new CancellationTokenSource()` is used for timeouts
- **Fix**: Use `CancellationTokenSource.TryReset()` (.NET 8+) and pool instances.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 13,653.86 (`HTTP/3`, Watson7) | 27.37 (`HTTP/3`, Watson7) | Catastrophic regression |
| Req/s (json) | 13,438.84 (`HTTP/3`, Watson7) | 39.10 (`HTTP/3`, Watson7) | Catastrophic regression |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Discard. A narrow item-13 slice pooled plain `CancellationTokenSource` instances for `HttpContextBase`, the HTTP/1.1 connection read-timeout path, and HTTP/3 per-request tokens. That caused a severe HTTP/3 regression under load and introduced benchmark failures, so the code has been reverted._ **Decision**: _Discard._

---

### 14. Replace `Task.Run()` per-connection with `IThreadPoolWorkItem`

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Files**: `src/WatsonWebserver/Webserver.cs` (line ~279)
- **Problem**: `Task.Run()` allocates a `Task` + closure per accepted connection. Under keep-alive workloads this is amortized (fires once per connection, not per request), so impact is limited to high connection-churn scenarios.
- **Fix**: Have the connection handler implement `IThreadPoolWorkItem` and queue via `ThreadPool.UnsafeQueueUserWorkItem`.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 71,293.67 | 73,231.03 | +1,937.36 |
| Req/s (json) | 49,760.64 | 51,383.84 | +1,623.20 |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Keep. In a direct HTTP/1.1 A/B run with the same harness settings, replacing per-connection `Task.Run` dispatch with `ThreadPool.UnsafeQueueUserWorkItem` plus `IThreadPoolWorkItem` wrappers improved Watson7 modestly on both `Hello` and `Json`._ **Decision**: _Keep / Discard._

---

### 15. Pre-compute common status line bytes

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Files**: `src/WatsonWebserver/HttpResponse.cs`
- **Fix**: Static `byte[]` arrays for `HTTP/1.1 200 OK\r\n`, `HTTP/1.1 404 Not Found\r\n`, etc. (~40 common codes). Direct `BlockCopy` instead of per-response string formatting + encoding.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 73,231.03 | 74,026.80 | +795.77 |
| Req/s (json) | 51,383.84 | 55,259.99 | +3,876.15 |
| GC Gen0 | N/A in harness | N/A in harness | N/A |

**Recommendation**: _Keep. Extending the existing response-header caching so the general header path also reuses precomputed status-line bytes improved Watson7 on the HTTP/1.1 `Hello` and `Json` benchmark scenarios in direct A/B runs._ **Decision**: _Keep / Discard._

---

### 16. `Http3VarInt.Encode()` — accept `Span<byte>` instead of allocating arrays

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [x] Discarded
- **Files**: HTTP/3 varint encoding paths
- **Fix**: Change signature to accept a `Span<byte>` destination, return bytes written. Caller provides stack-allocated or pooled buffer.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 15,400.71 (`HTTP/3`, Watson7 median of 3) | 13,782.71 (`HTTP/3`, Watson7 median of 3) | -1,618.00 |
| Req/s (json) | 14,893.58 (`HTTP/3`, Watson7 median of 3) | 13,187.23 (`HTTP/3`, Watson7 median of 3) | -1,706.35 |
| Managed alloc | 2.70 GiB (`Hello`) / 4.05 GiB (`Json`) | 2.41 GiB (`Hello`) / 3.58 GiB (`Json`) | Lower alloc, lower throughput |

**Recommendation**: _Discard. A contained item-16 slice added a span-based `Http3VarInt.Encode(long, Span<byte>)` API and switched the HTTP/3 frame, control-stream, and settings serializers to stack-backed intermediate encoding. It reduced managed allocation in the benchmark runs, but repeated Watson7 HTTP/3 median throughput regressed on both `Hello` and `Json`, so the change was reverted._ **Decision**: _Discard._

**Notes**: _This benchmark was measured only on Watson7 HTTP/3 because the affected code path is specific to the HTTP/3 serializers. The reverted baseline materially outperformed the attempted optimization despite the small allocation drop, which suggests the extra span-copy shape was not a net win in the current implementation._

---

### 17. Use `ConfigureAwait(false)` consistently on all internal async calls

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [ ] Discarded
- **Fix**: Audit all `await` calls in the library. Add `ConfigureAwait(false)` where missing.
- **Note**: Small but consistent reduction in async state machine overhead. Good hygiene for library code.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | N/A | N/A | N/A |
| Req/s (json) | N/A | N/A | N/A |
| Req/s (`Echo`, Watson7 HTTP/1.1 median of 3) | 54,058.55 | 59,011.59 | +4,953.04 |

**Recommendation**: _Keep. The audit found a single remaining internal `await` without `ConfigureAwait(false)`: `HttpResponse.Send(long, Stream, ...)` on the HTTP/1.1 stream-response path. A direct Watson7 HTTP/1.1 `Echo` A/B median improved materially after adding it._ **Decision**: _Keep._

**Notes**: _The standard `Hello` and `Json` scenarios do not exercise this path, so they are marked N/A for this item. The benchmark used `Echo` because it routes through `context.Response.Send(context.Request.ContentLength, context.Request.Data, ...)`, which is the exact affected method._

---

### 18. Inline event handler null-check fast path

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [x] Discarded
- **Fix**: `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on the null-check path, `[MethodImpl(MethodImplOptions.NoInlining)]` on the try-catch slow path. Improves instruction cache locality.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 95,580.85 | 97,551.80 | +1,970.95 |
| Req/s (json) | 73,293.51 | 72,931.31 | -362.20 |

**Recommendation**: _Discard. A contained event-dispatch refactor moved subscribed handlers to a noinline slow path and made the null-check fast path inlinable, but the Watson7 HTTP/1.1 median results were mixed: `Hello` improved modestly while `Json` regressed slightly. That is not a clear enough win to justify the extra specialization, so the code was reverted._ **Decision**: _Discard._

**Notes**: _This benchmark used the normal no-subscriber hot path, which is where the proposed change was intended to help. The aggregate effect was small enough to be within the noise band for this harness, so the conservative decision is to leave the simpler original event wrapper in place._

---

### 19. Cache `Encoding.UTF8` / `Encoding.ASCII` as `static readonly` fields

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [x] Discarded
- **Fix**: `private static readonly Encoding _UTF8 = Encoding.UTF8;` — direct field load instead of property getter. Marginal but free.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 95,580.85 | 75,720.95 | -19,859.90 |
| Req/s (json) | 73,293.51 | 51,902.00 | -21,391.51 |

**Recommendation**: _Discard. A contained item-19 slice cached `Encoding.UTF8` and `Encoding.ASCII` fields in the HTTP/1.1 parser plus common request/response paths. In direct Watson7 HTTP/1.1 median runs, that regressed both `Hello` and `Json` materially, so the code was reverted._ **Decision**: _Discard._

**Notes**: _This benchmark covered only a first hot-path slice rather than every encoding call in the repository. Given the magnitude of the regression on that slice, there was no reason to continue widening the change._

---

### 20. Sharded per-core request counter

- **Status**: [ ] Not started / [ ] In progress / [x] Benchmarked / [ ] Kept / [x] Discarded
- **Files**: `_RequestCount` usage via `Interlocked.Increment`/`Decrement`
- **Problem**: Under extreme concurrency on 16+ core machines, `Interlocked` operations on a single counter cause cache line bouncing.
- **Fix**: `ThreadLocal<long>` or a sharded counter. Sum on read.
- **Note**: Only matters at high core counts. Questionable win on typical deployments.

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Req/s (hello) | 95,580.85 | 80,036.11 | -15,544.74 |
| Req/s (json) | 73,293.51 | 58,079.82 | -15,213.69 |

**Recommendation**: _Discard. A contained item-20 slice replaced the single `_RequestCount` field with per-thread sharded counters in the HTTP/1.1 keep-alive loop and summed shards on read. On Watson7 HTTP/1.1 median runs, that regressed both `Hello` and `Json`, so the change was reverted._ **Decision**: _Discard._

**Notes**: _This item only touched the `RequestCount` bookkeeping in `Webserver`; it did not attempt to shard the broader statistics subsystem. That narrower experiment was enough to show the extra indexing and multi-counter writes were not a win on this benchmark machine._

---

## Key Corrections from Analysis

These items were initially considered but **do not need optimization**:

| Item | Why It's Already Fine |
|------|-----------------------|
| **Request semaphore** | Acquired per-connection, not per-request. It's an admission gate, not a hot-path tax. Under keep-alive benchmarks, fires ~128-256 times total, not 62k/s. |
| **Date header lock** | Already has an unlocked fast path — checks cached second value before the lock. Lock only entered on cache miss (once/second). |

---

## Workflow for Each Item

```
1. git checkout -b perf/item-N-short-description
2. Run Test.Benchmark → record "Before" numbers
3. Implement the change
4. Run Test.Benchmark → record "After" numbers
5. Fill in the tracking table above
6. Agent provides recommendation (keep/discard) with reasoning
7. User reviews and decides
8. If kept: merge to main
   If discarded: git branch -D perf/item-N-short-description
```

## Cumulative Progress Tracker

| Phase | Items | Expected Cumulative Impact | Actual Cumulative Req/s (hello) | Actual Cumulative Req/s (json) |
|-------|-------|---------------------------|--------------------------------|-------------------------------|
| Baseline | — | — | ~62,000 | ~74,000 |
| Phase 1 | 1-4 | +30-50% | | |
| Phase 2 | 5-8 | +15-25% additional | | |
| Phase 3 | 9 | +30-50% additional | | |
| Phase 4 | 10-20 | +5-10% additional | | |
| **Target** | **All** | **Kestrel parity (~140k+)** | | |
