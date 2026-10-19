# Bug fix plan: HTTP/2 PRIORITY, unknown frames, and client RST_STREAM close the whole connection

Issue: [dotnet/WatsonWebserver#197](https://github.com/dotnet/WatsonWebserver/issues/197), "HTTP/2: PRIORITY / unknown frame types and client RST_STREAM close the whole connection (breaks Firefox)".

Baseline: `13f8897` (Watson 7.3.1). Prepared release: **7.3.2** (patch), version increment authorized by the owner in this session. Publication remains on hold while the updated test results are reported.

This file is a work order. Tick boxes as you go, put your initials and the date next to each completed item, and write anything surprising in the "Notes" line under each phase. Delete this file (or move it to `archive/`) once the release ships, as was done for `archive/BUGFIX.md`.

## Protocol-validation follow-up (Codex, 2026-10-19)

The previously outstanding frame-size, idle-stream control, lower stream-ID, SETTINGS ACK, SETTINGS initial-window, stream-window overflow, and HPACK placement defects are now fixed in production. Peer GOAWAY drains existing requests and processes PING instead of immediately closing a socket with unread control bytes. Dedicated h2c listeners now validate every preface as HTTP/2; shared cleartext listeners retain HTTP/1.1 protocol detection. Initial SETTINGS and all later frames are length-checked before allocating or waiting for payload bytes.

Added 36 shared regression checks (109 resilience cases in total), including positive cases and both h2c/TLS transports. Three complete h2spec runs per transport against dedicated HTTP/2 listeners each report 144/146 raw passes; only the two legacy self-dependency checks fail. All six RFC 9113 profile summaries report 144/144 applicable passes, zero failures/skips, and two N/A cases. Raw failures are retained and never counted as passes. Evidence: `artifacts/http2-197-conformance/`.

The first full conformance-fix run exposed two invalid legacy test inputs: both 32 KB body-access cases emitted one 32 KB DATA frame despite the server's advertised 16 KB frame limit. Their shared upload helper now splits at the advertised maximum and sets END_STREAM only on the final fragment. Payload sizes, byte-for-byte echo assertions, and response expectations are unchanged. The new oversized-frame rejection tests remain strict. Both corrected body cases pass individually and in all six complete final runner runs. Initial failing results are retained under `/tmp/watson-conformance-staggered-*`.

Fresh native AOT publications on .NET 8 and .NET 10 each pass all 43 checks. All six final repository runs (CLI, xUnit, NUnit on .NET 8 and .NET 10) pass 1,243 tests each, with zero failures/skips. Debug and Release solution builds have zero warnings/errors. Package validation against 7.3.1 passes. The original A–F reproducer passes all six scenarios on both transports; Firefox 155 and Chrome 155.0.8059.85 pass repeat navigation/subresource checks. All package DLLs and symbol PDBs match the current Release build, and embedded READMEs match their source files. Nine report-interpreter tests pass. Final logs and initial failing test-input runs are retained under `artifacts/http2-197-conformance/`. The sections below retain the earlier measurements as historical evidence; their remaining-failure counts predate these fixes. Publication remains on hold.

## Implementation evidence (Codex, 2026-10-19)

**Closed-stream DATA policy updated at the owner's request. All six final shared-suite runs, native AOT checks, API validation, browser checks, and the issue repro pass. No package has been published, no release commit/tag has been created, and nothing has been pushed.** Version 7.3.2 and release documentation are prepared locally. Staged packages and symbols have been rebuilt with the corrected closure history and verified for archive integrity, all three Watson target-framework assemblies, and the current embedded README.

Implemented frame dispatch, atomic committed writes using the connection token, faulted-writer handling, per-stream terminal gates, exactly-once connection-credit ownership/refunds, reset isolation, bounded local-reset history, HPACK decoding of discarded trailers, and nonblocking batch reservations. Public request validation remains in the frame-reader dispatch path so malformed Content-Length still gets the existing connection error. Small batched responses also reserve flow-control credit, fixing an additional omission found during implementation. HTTP/1.1 and HTTP/3 production code are unchanged. Optional setting 0x9 is deferred.

Resolved compatibility regression: the initial implementation followed step 1b.2's broad DATA discard rule and changed four previously passing h2spec results. The owner requested the closure-reason policy described below. Direct review of RFC 9113 section 5.1 clarified that minimal processing for all closed streams is explicitly permitted; the earlier assertion that those failures alone proved RFC violations was incorrect. The correction restores strict errors for known normal/client-reset closures while retaining minimal processing for local resets and uncertain evicted history. All four affected h2spec DATA checks now pass over h2c and TLS. The only baseline-pass/current-fail comparison is the RFC 7540 self-dependent PRIORITY rejection check, whose requirement is absent from RFC 9113.

Updated implementation: `Http2ClosedStreamHistory` records up to 128 closure reasons under the session lock. Local reset state and history removal happen atomically before handler cancellation; normal cleanup cannot overwrite a local-reset reason. Known normal/client-reset closures receive GOAWAY(STREAM_CLOSED). A tracked half-closed remote stream receives RST_STREAM(STREAM_CLOSED), with padded DATA accounted against the connection window and sibling requests preserved. Evicted history uses RFC 9113 minimal processing, including HPACK decoding for discarded HEADERS. No timers or unbounded records were added.

Updated checks: the 67-case resilience suite passes, including eight h2c/TLS closure-policy cases, bounded-history classification, and HPACK synchronization after eviction. The standalone A-F repro passes on both transports. h2spec 2.6.0 reports 132/146 passing over h2c and 133/146 over TLS, with all four DATA regressions fixed. Final full-suite results: all 1,201 cases pass in each of the CLI, xUnit, and NUnit runners on both .NET 8 and .NET 10, with zero failures/skips. Both native AOT executables pass 43 checks. Debug/Release solution builds report zero warnings/errors, and package validation against 7.3.1 passes. Firefox 155 and Chrome 155 repeat the browser checks successfully. Fresh three-repetition HTTP/2 performance comparison: baseline hello/json medians 31,665.68 / 32,664.10 requests/sec; corrected implementation 34,288.35 / 32,582.44, about +8.3% / -0.25%. Median latency was 0.92 / 0.89 ms on baseline and 0.86 / 0.89 ms on the corrected build. No throughput regression was observed beyond run-to-run variation. The comparison ran after the correctness suites finished.

Test orchestration note: launching all runners simultaneously initially caused the legacy fixed-port harness to report Address already in use in .NET 8 runs. Affected/in-progress adapter runs were stopped and rerun with staggered initialization, waiting for actual test execution to start before initializing the next runner. All five reruns exited 0, without changing existing tests or production behavior to bypass the collision. The initial .NET 10 CLI run also passed. Final evidence is stored under `artifacts/http2-197-closure/`.

## RFC-specific priority test update (Codex, 2026-10-19)

At the owner's request, six explicit positive cases were added for self-dependent idle PRIORITY, open-stream PRIORITY, and HEADERS priority fields over h2c and TLS. They check the response status/body, absence of reset/GOAWAY, connection reuse, and PING acknowledgments. The idle case checks that a phantom priority stream does not advance the request stream watermark. Mandatory framing checks remain unchanged. The resilience suite now contains 73 cases.

`tools/summarize_h2spec.py` interprets h2spec JUnit reports using a required `--rfc 7540` or `--rfc 9113` profile. Only the exact HEADERS/PRIORITY self-dependency rejection checks in `http2/5.3.1` are marked not applicable under RFC 9113; both remain applicable under RFC 7540. Raw statuses/totals and input SHA-256 are retained. N/A cases are never counted as passes, and unrelated failures keep the tool's exit status nonzero. Nine unit tests in `src/Test.Shared/Http2Conformance/` cover profile differences, exact matching, skipped cases, input immutability, diagnostic control octets, output-overwrites-input rejection, and invalid inputs; all pass.

Fresh raw h2spec results are 132 passed and 14 failed out of 146 on each transport. The RFC 9113 interpretation reports 132 passed and 12 failed out of 144 applicable cases, plus two N/A cases. Both the raw run and interpretation remain failing. The TLS run additionally failed `generic/3.8` (Sends a GOAWAY frame), which passed in the preceding closure-policy run; this result remains visible and applicable. The GOAWAY case passed all ten targeted TLS repeats and the subsequent complete TLS rerun. That full rerun reports 133/146 raw passes and 133/144 applicable passes (11 applicable failures plus two N/A). The initial raw failure has been retained; it is not reclassified or counted as a pass. No production code changed during this priority-test update. Both existing native AOT executables were rerun and passed all 43 checks. Raw reports and profile summaries are under `artifacts/http2-197-priority/`; All six final shared-suite reruns (CLI, xUnit, NUnit; .NET 8 and .NET 10) pass all 1,207 cases each with zero failures/skips. The original four DATA regression checks still pass. No production code or existing test expectations were weakened.

Evidence collected (initial implementation, retained as history):

- Standalone issue repro downloaded and inspected outside the checkout. Local 7.3.1: A passed, B-F failed, over both h2c and TLS. Patched build: all six pass on both transports.
- `Http2Resilience`: initially 57 cases (now 67 after the closure-policy correction) in Test.Shared, registered once for all runners. Includes all 14 dispatcher scenarios; deterministic writer commit/cancel/fault tests; h2c and TLS late writes, chunks, SSE, stream sends, and token-observing handlers; default-window byte-array/stream transfers; reset credit; source failure; late padded DATA; closed/idle behavior; timeout/fallback guards; six golden sequences; 1,000 atomic reservation/termination races; a queued-response refund test; discarded-trailer HPACK; 200 tombstone evictions; 1,000 rapid resets with exactly 900 refusals and a maximum of 100 handlers; 200 multiplexed requests; and an API reset test.
- Original baseline run reproduced the dispatcher, committed-write cancellation, late response/reset, large-response deadlock, credit-leak, source failure, and late-DATA defects. Token-observing reset handlers, timeout, and idle-DATA guards already passed. The missing-response guard initially assumed a fallback 500 even when a custom exception route was installed; baseline verification showed the custom route is invoked, so the new guard was corrected to check the actual compatibility contract. Existing test cases were not edited.
- Six golden fixtures captured from detached 7.3.1 (`13f8897`) match the patched build: 1 KB, 32 KB, CONTINUATION headers, chunks, trailers, and 4 MB with both windows raised. Only Date values are normalized.
- Full CLI: .NET 10, 1,191 passed; .NET 8, 1,166 passed. xUnit: .NET 8, 1,166 passed; .NET 10, 1,191 passed. NUnit: 1,191 passed on each framework. Zero failures/skips in all six runs. Counts from the legacy coverage projection vary with available protocol/runtime coverage.
- Debug and Release solution builds: zero warnings and errors. Package validation against Watson 7.3.1 passed without public API differences. Native .NET 8 and .NET 10 AOT executables each pass all 43 checks, including the added large-response checks. .NET 8's native linker emits an environment warning that `-ld_classic` is obsolete; trim/AOT analyzers report no warnings.
- HTTP/2 hello/json benchmarks, three repetitions on each build: baseline medians 35,433.79 / 33,140.63 requests/sec; patched medians 34,899.29 / 33,276.78, about -1.5% / +0.4%, with unchanged median latency (0.85 / 0.88 ms).
- h2spec 2.6.0: baseline h2c 118/146 and TLS 119/146; patched h2c 129/146 and TLS 130/146. Remaining failures include pre-existing frame-size, idle control frames, SETTINGS/flow-control, and HPACK checks; those measurements were from the initial broad-discard implementation; its four DATA regressions are now fixed, as described above. Reports are under `artifacts/http2-197/`. h2spec XML contains NUL bytes in diagnostic payloads, which were stripped only in the comparison parser.
- Automated actual-browser checks: Firefox 155.0 and Chrome 155.0.8059.84, TLS HTTP/2, initial/final full subresource loads and ten reload/navigation cycles. Both passed. Firefox HTTP logging shows no received GOAWAY; its only GOAWAY is client-generated at shutdown. Chrome netlog shows zero received GOAWAY and zero standalone PRIORITY sends. These are automated browser checks, not a manual about:logging session.
- Follow-ups filed and linked from #197: batching deadlock #198, ignored-frame/reset rate limits #199, HTTP/3 conformance audit #200. HTTP/3 request streams ignore unknown types but also forbidden HTTP/2-only types; settings ignore unknown identifiers including forbidden HTTP/2 identifiers; the control handler reads only initial SETTINGS. Unknown unidirectional streams are drained. Request-side QUIC/IO exceptions are isolated by the existing catches. No HTTP/3 changes made.

Deviations from the work order: a single final commit is being prepared rather than committing temporary skipped tests to main. The initial 14 dispatcher cases were run before production changes; subsequent baseline checks used a detached 7.3.1 worktree. Some requested upload/half-closed-handler scenarios assume request dispatch before the body finishes; this server buffers the complete request before dispatch. Pending uploads were reset with malformed PRIORITY to exercise late upload and HPACK handling instead. No claim of exhaustive protocol conformance is made; complete protocol conformance remains outside the evidence established here; the four measured DATA regressions have been corrected.

## Progress tracker

| Phase | Title | Ships in | Status | Owner | Done (date) |
|-------|-------|----------|--------|-------|-------------|
| 0 | Failing tests first, plus test helpers | 7.3.2 | In progress (baseline tests complete; deviations documented) | Codex | 2026-10-19 |
| 1 | Frame dispatch (PRIORITY, unknown types, CONTINUATION, PUSH_PROMISE) | 7.3.2 | Done | Codex | 2026-10-19 |
| 1b | Receive side after a server-sent RST_STREAM | 7.3.2, together with 2, 3 and 4 | Done (closure policy corrected at owner request) | Codex | 2026-10-19 |
| 2 | Writer commit rule | 7.3.2, together with 1b, 3 and 4 | Done | Codex | 2026-10-19 |
| 3 | Reservation ownership and terminal ordering | 7.3.2, together with 1b, 2 and 4 | Done | Codex | 2026-10-19 |
| 4 | Reset isolation (no GOAWAY for response-side failures) | 7.3.2, together with 1b, 2 and 3 | Done | Codex | 2026-10-19 |
| 5 | Flow-control batching liveness (separate issue) | 7.3.2 if green, else next patch | Done | Codex | 2026-10-19 |
| 6 | `SETTINGS_NO_RFC7540_PRIORITIES` (optional) | Owner decision | Skipped (deferred) | Codex | 2026-10-19 |
| 7 | HTTP/3 audit (no code change expected) | 7.3.2 notes | Done (follow-up #200) | Codex | 2026-10-19 |
| 8 | Release | 7.3.2 | In progress (verification complete; publication held for test-results review) | Codex | 2026-10-19 |

Status values: `Not started`, `In progress`, `Blocked (reason)`, `Done`, `Skipped (reason)`.

## What is wrong

Issue #197 is correct on both counts. It was verified by reading the source at `13f8897`. The attached repro has not been run yet (Phase 0 does that).

**Frame dispatch.** `Http2ConnectionSession.HandleFrameAsync` (`src/WatsonWebserver/Http2/Http2ConnectionSession.cs:185`) handles SETTINGS, PING, HEADERS, DATA, RST_STREAM, GOAWAY and WINDOW_UPDATE. Every other frame type reaches `default:` at line 213. That branch sends `GOAWAY(INTERNAL_ERROR, "Unsupported HTTP/2 frame type was received.")` and closes the connection, so every in-flight stream dies. The writer gets disposed under the running handlers, which is where the `ObjectDisposedException: Http2ConnectionWriter` in the issue's log comes from. RFC 9113 §4.1 and §5.5 say unknown frame types MUST be ignored. §6.3 says PRIORITY can arrive in any stream state. Firefox sends PRIORITY on idle "phantom" streams and may send `PRIORITY_UPDATE` (0x10, RFC 9218), so Firefox pages load partially or fail. Chromium normally puts priority inside HEADERS, which Watson already handles, so it is not expected to hit this path. That has not been verified; the manual Chrome check in 8.8 settles it.

**Client RST_STREAM.** When a client cancels a stream (Firefox does this during ordinary navigation), `HandleRstStreamFrame` (line 755) closes the stream state machine, removes it, and cancels the request token. A handler that ignores `ctx.Token` keeps running and eventually calls `Send`. `Http2StreamStateMachine.SendHeaders` throws `Http2StreamStateException`. `WebserverBase` (`Core/WebserverBase.cs:887`) catches it as a generic exception and runs `Routes.Exception` or the default 500, which throws again. `ProcessPendingRequestAsync` (`Http2ConnectionSession.cs:416`) then turns that into `GOAWAY(PROTOCOL_ERROR)` and kills every sibling stream. The server is blaming the peer for its own write after close.

Reviewing the fix turned up five related defects in the same code. A fix that ignores them would hide the GOAWAY symptom while letting a client cancellation still corrupt or starve sibling streams, which is harder to diagnose than what we have now.

1. **Request cancellation can tear a frame.** `Http2ConnectionWriter` passes the caller's token (usually `ctx.Token`) to `_Stream.WriteAsync` *inside* `_WriteLock` (`Core/Http2/Http2ConnectionWriter.cs:99-117`, `131-161`, `251-264`). An RST_STREAM that cancels `ctx.Token` can stop a write between HEADERS and CONTINUATION, or between a 9-byte frame header and its payload (lines 260 and 263 are separate writes). Either way the connection's framing is broken for every stream.
2. **Connection credit leaks on reset.** `Http2Response.WriteDataFramesAsync` (`Http2/Http2Response.cs:481-544`) and `WriteDataFramesFromStreamAsync` (`547-607`) reserve flow-control window for each segment, and `ReserveSendWindowAsync` deducts `_ConnectionSendWindow` right away (`Http2ConnectionSession.cs:838`). The segments are written only after the loop (`543`, `606`). If the loop exits early because of a reset, a cancelled token or a state exception, the reserved bytes never reach the wire and are never refunded. The client only returns credit for DATA it actually receives (RFC 9113 §6.9), so each reset during a large write permanently shrinks the window every stream shares.
3. **DATA can follow RST_STREAM.** A batch already queued on `_WriteLock` when the stream is reset still gets written, so frames go out on a closed stream after the reset.
4. **Large responses can deadlock with no reset at all.** The byte-array path reserves every segment before writing any of them. If the peer leaves the stream window (`SETTINGS_INITIAL_WINDOW_SIZE`) at the RFC default of 65,535 bytes and only sends WINDOW_UPDATE after receiving DATA, a response over 64 KB waits forever. The stream window alone is enough to trigger it; the connection window does not need to be small. The response waits for credit while holding unsent reservations. The stream path has the same problem when the peer advertises `SETTINGS_MAX_FRAME_SIZE >= 65536`, because its buffer becomes 65,536 bytes. Browsers advertise huge windows and mostly hide this. nghttp2 and .NET `HttpClient` at defaults can hit it. This is a separate bug from #197 and gets its own GitHub issue (Phase 5).
5. **Frames after a server-sent RST_STREAM close the connection.** `HandleDataFrameAsync` (`Http2ConnectionSession.cs:327-331`) answers DATA on any stream missing from `_Streams` with `GOAWAY(PROTOCOL_ERROR)`. `HandleHeadersFrameAsync` (`:282`) recreates state for a removed stream, so trailing HEADERS on it fail parsing and also end in GOAWAY. RFC 9113 §5.1 and §6.4 say that after sending RST_STREAM an endpoint MUST ignore frames the peer sent before it saw the reset, still count their DATA against the connection flow-control window, and still process HEADERS through HPACK (§4.3) so the decoder's dynamic table stays in step. 7.3.1 rarely sends RST_STREAM, but this plan adds server-sent resets (Phase 4 and the malformed-PRIORITY case), so without Phase 1b a reset during a request-body upload would become a GOAWAY.

## Zero-disruption requirements

Each item is a release gate. If a change can't meet one, stop and report back instead of working around it.

- [ ] **R1. No public API change.** No public type, member, signature, setting, or default is added, removed, or changed. All new types and members are `internal` (`Http2Response`, `Http2StreamStateMachine`, `Http2ConnectionWriter` and `Http2FrameWriteSegment` are public, so add internal overloads and internal members rather than changing existing signatures). Verify with package validation against 7.3.1 (Phase 8, step 3).
- [ ] **R2. No new dependencies or target framework changes.** AOT and trim analyzers stay at zero warnings (`TreatWarningsAsErrors` is on).
- [ ] **R3. Successful exchanges are semantically unchanged.** For any request where the client does not reset a stream and sends no PRIORITY, unknown, orphaned CONTINUATION, or PUSH_PROMISE frame, the response status, headers, trailers, body bytes, and the order of HEADERS, DATA and trailers on each stream are the same as 7.3.1. HTTP/1.1 and HTTP/3 are not touched at all. DATA frame boundaries and interleaving between streams are not part of the contract: correct batching (Phase 5) may change them, and the existing suites could not prove byte identity anyway. Two checks prove R3:
  - the existing suites pass unedited;
  - golden-frame tests (T0.1 and T5.5) show that a response fitting inside the initial windows produces exactly the same frame sequence (types, flags, lengths, payload bytes) as 7.3.1.
- [ ] **R4. Cancellation looks the same on HTTP/2 as it already does elsewhere.** A write to a stream the peer has reset throws `OperationCanceledException`. `WebserverBase.ProcessHttpContextAsync` already catches `OperationCanceledException` (`Core/WebserverBase.cs:869`), marks the request terminated, raises `RequestAborted`, and does **not** call `Routes.Exception`. That is today's behavior when a handler observes `ctx.Token`, and when an HTTP/1.1 client disconnects. No new exception type is introduced.
- [ ] **R5. Error codes change only where 7.3.1 was wrong.** The only wire-visible changes are: connections stay open where 7.3.1 sent GOAWAY; an orphaned CONTINUATION or client PUSH_PROMISE gets `PROTOCOL_ERROR` where 7.3.1 sent `INTERNAL_ERROR`; a response abandoned by the server gets `RST_STREAM` on that stream, where 7.3.1 sent GOAWAY or left the stream hanging; and frames that arrive on a stream after the server reset it are ignored, where 7.3.1 sent GOAWAY.
- [ ] **R6. No performance regression on the happy path.** `Test.Benchmark` HTTP/2 `hello` and `json` scenarios stay within run-to-run noise of 7.3.1 (Phase 8, step 5). The new checks are a flag read and an integer field, nothing more.

The one user-visible behavior change is that `Routes.Exception` and `Events.ExceptionEncountered` stop firing for HTTP/2 requests the client cancelled, and `Events.RequestAborted` fires instead. Today those exception callbacks fire with an `Http2StreamStateException` immediately before the connection is torn down, so no application can be depending on that sequence working. A second, smaller change: a handler's late `Send` on a reset stream now throws `OperationCanceledException` where 7.3.1 threw `Http2StreamStateException`. Code that caught `Http2StreamStateException` around `Send` will no longer see it. The CHANGELOG says both plainly (Phase 8).

## Phase 0: failing tests first, plus test helpers

`CLAUDE.md` asks for coverage before behavioral fixes. Every test in the test plan below goes in now, and the ones marked "fails on 7.3.1" must fail on the baseline before any production code changes. A test that passes on 7.3.1 when it shouldn't is testing the wrong thing.

- [x] 0.1 File a GitHub issue for the batching deadlock (defect 4 above) and record its number here: `#198`. Link it from #197. (Codex, 2026-10-19)
- [x] 0.2 Download the issue's `WatsonHttp2Repro.zip` into its own empty directory outside this repository, read `run-repro.ps1` and both projects before running anything, then run it against a local 7.3.1 build. Record the A to F results here. Expected: A passes, B to F fail as in the issue. (Codex, 2026-10-19)
- [ ] 0.3 Add raw-frame helpers to `src/Test.Shared/Http2SharedTestUtilities.cs` (internal, no console output): write an arbitrary frame (type, flags, stream ID, payload) including unknown type bytes; build PRIORITY, PRIORITY_UPDATE, RST_STREAM, WINDOW_UPDATE, CONTINUATION and PUSH_PROMISE frames; read frames until a predicate or timeout, returning every frame seen (so tests can assert "no GOAWAY" and "no DATA after RST_STREAM on stream N"); and handshake with caller-chosen `SETTINGS_INITIAL_WINDOW_SIZE` and `SETTINGS_MAX_FRAME_SIZE`. Reuse `PerformClientHandshakeAsync` and `BuildRequestHeaderBlock` where possible.
- [x] 0.4 Add a test-only `Stream` (one class per file, e.g. `src/Test.Shared/GatedWriteStream.cs`) that records written bytes and can block a write until released. It lets the writer tests in Phase 2 control timing without sleeps. (Codex, 2026-10-19)
- [ ] 0.5 Create `src/Test.Shared/SharedHttp2ResilienceTests.cs` with the cases in the test plan, and register a new `Http2Resilience` suite in `WatsonTestSuites.cs`. The three runners pick it up with no per-runner edits.
- [ ] 0.6 Every case binds to `127.0.0.1`, uses its own port, cleans up its server, and has a hard timeout (10 seconds unless noted). A deadlock must show up as a failure, never as a hung run.
- [x] 0.6a Capture golden frame sequences from 7.3.1 for T0.1 and T5.5 and commit them as test data under `src/Test.Shared/`. Capture them before any production change. (Codex, 2026-10-19)
- [ ] 0.7 Run the new suite on 7.3.1 and record which cases fail. Commit the tests (all new failing cases marked `skip: true` with `skipReason: "Fixed in Phase N"`, so `main` stays green). Each phase removes the skip for its own cases.

Notes:

## Phase 1: frame dispatch

This phase alone fixes the Firefox symptom. It can ship on its own if Phases 1b to 4 are not ready (see Phase 8). It never sends RST_STREAM, because without the write gate from Phases 3 and 4 the reset stream's handler would keep running and write HEADERS or DATA on a stream the server closed, which §5.1 forbids and which hits the same `Http2StreamStateException` -> GOAWAY path as #197.

Replace the `default:` branch at `Http2ConnectionSession.cs:213`:

```csharp
case Http2FrameType.Priority:
    await HandlePriorityFrameAsync(writer, frame, token).ConfigureAwait(false);
    break;
case Http2FrameType.PushPromise:
    await SendGoAwayAsync(writer, _LastRemoteStreamIdentifier, Http2ErrorCode.ProtocolError, "Clients must not send PUSH_PROMISE.", token).ConfigureAwait(false);
    break;
case Http2FrameType.Continuation:
    await SendGoAwayAsync(writer, _LastRemoteStreamIdentifier, Http2ErrorCode.ProtocolError, "CONTINUATION frame without a preceding header block.", token).ConfigureAwait(false);
    break;
default:
    // RFC 9113 sections 4.1 and 5.5: unknown and extension frame types (PRIORITY_UPDATE 0x10 and others) are ignored.
    break;
```

**Last-stream-ID in GOAWAY.** Do not copy the existing pattern of passing `frame.Header.StreamIdentifier`. `SendGoAwayAsync` takes the maximum of its argument and `_LastRemoteStreamIdentifier` (`:814`), so passing the frame's stream ID for a PRIORITY on idle stream 13 would tell the client stream 13 may have been processed, which is false (§6.8). For every GOAWAY sent from the PRIORITY, CONTINUATION and PUSH_PROMISE paths, pass `_LastRemoteStreamIdentifier` (or 0, which gives the same result). Priority-only frames never advance the watermark.

- [ ] 1.1 Add `HandlePriorityFrameAsync` (private) with these rules:
  - Stream 0: `GOAWAY(PROTOCOL_ERROR)` (§6.3).
  - Payload length not 5, on any stream: `GOAWAY(FRAME_SIZE_ERROR)` in this phase. §6.3 makes this a stream error, and §5.4.1 lets an endpoint treat a stream error as a connection error. Step 4.7 later narrows the open-stream case to `RST_STREAM(FRAME_SIZE_ERROR)`, once the write gate exists. For a stream that isn't tracked, GOAWAY stays the answer: RST_STREAM is forbidden on an idle stream (§5.1, §6.4), and while "idle" is decidable (an odd ID above `_LastRemoteStreamIdentifier`), a single GOAWAY rule is simpler and equally compliant.
  - Valid 5-byte PRIORITY on any other stream: ignore it. Do **not** call `GetOrCreateRemoteStream`, do not allocate anything, and do not touch `_LastRemoteStreamIdentifier`. Firefox's phantom streams 3 to 13 must not create stream state or inflate the last-stream ID in a later GOAWAY.
- [x] 1.2 Confirm `ReadHeaderBlockAsync` (line 456) still consumes CONTINUATION frames that legitimately follow HEADERS, so only orphaned ones reach the dispatcher. The existing "Continuation Header Block Request" smoke test covers the legitimate path. (Codex, 2026-10-19)
- [x] 1.3 Confirm that any frame other than CONTINUATION arriving between HEADERS and its CONTINUATION, including PRIORITY and unknown or extension types, is still rejected by `ReadHeaderBlockAsync` (`:493-497`) as a connection `PROTOCOL_ERROR` (§6.10, and §5.5 for extension frames). The new "ignore unknown frames" rule applies only in the dispatcher, never inside a header block. T1.10 and T1.14 guard this. (Codex, 2026-10-19)
- [x] 1.4 Confirm `Http2FrameSerializer.cs:100` reads frames of unknown type and any length up to `MAX_FRAME_SIZE` without throwing, so the payload is consumed and the next frame parses correctly. Cases T1.4 and T1.5 prove it. (Codex, 2026-10-19)
- [ ] 1.5 Un-skip T1.x. All pass. All existing suites pass unedited.

Notes:

## Phase 1b: receive side after a server-sent RST_STREAM

Ships with Phases 2 to 4, because those phases are what add server-sent resets. RFC 9113 §5.1: after sending RST_STREAM, the server MUST ignore frames the client sent before it saw the reset. It must still count DATA against the connection window, and must still decode HEADERS through HPACK, discarding the result.

- [x] 1b.1 Add an internal bounded record of streams the server reset (for example the last 128 stream IDs, oldest evicted first, in a new internal class, one class per file). Any client stream ID at or below `_LastRemoteStreamIdentifier` that is not in `_Streams` is closed, not idle (§5.1.1). The record only decides how strictly to treat it; nothing below depends on the record being complete. (Codex, 2026-10-19)
- [x] 1b.2 (Codex, 2026-10-19; revised at owner request) For a removed stream at or below the watermark, consult bounded closure-reason history. Known normal/client-reset closures return GOAWAY(STREAM_CLOSED). Local resets and uncertain evicted history minimally process DATA, count the entire payload including padding against connection flow control, return connection credit, and discard it. A live half-closed remote stream instead receives RST_STREAM(STREAM_CLOSED). DATA on truly idle streams or stream zero retains GOAWAY(PROTOCOL_ERROR). RFC 9113 section 5.1 explicitly permits conservative minimal processing when history is uncertain.
- [x] 1b.3 `HandleHeadersFrameAsync`: for a stream ID at or below `_LastRemoteStreamIdentifier` that is not in `_Streams`, read the full header block (`ReadHeaderBlockAsync`) and run it through the HPACK decoder so the dynamic table stays in step, then discard it. Do not call `GetOrCreateRemoteStream` for it. If the stream ID is in the reset record, that is all. If it is not (the client reused a closed stream ID), send `GOAWAY(STREAM_CLOSED)` after decoding (§5.1). (Codex, 2026-10-19)
- [x] 1b.4 RST_STREAM and WINDOW_UPDATE for these streams are already ignored silently; confirm and leave them alone. PRIORITY is handled by Phase 1. (Codex, 2026-10-19)
- [ ] 1b.5 Un-skip T1b.x. All pass.

Notes:

## Phase 2: writer commit rule

Phases 1b, 2, 3 and 4 ship together or not at all. Without Phase 1b, a server reset during an upload becomes a GOAWAY. Without Phase 2, a client cancellation can still tear a frame. Without Phase 3, resets still leak connection credit and can put DATA on the wire after RST_STREAM. Phase 4 by itself would only hide the GOAWAY.

The rule: **the request token is honored only while waiting for `_WriteLock`. Once the lock is held, the write is committed and runs to completion with the connection token.** A connection-level failure after commit (transport error, connection token cancelled) faults the writer: it closes the transport, and every later write fails fast. The lock must never be released for another writer after a partial write.

- [x] 2.1 Give `Http2ConnectionWriter` an internal connection token, set by `Http2ConnectionSession` when it creates the writer (an internal constructor overload or internal property, per R1). (Codex, 2026-10-19)
- [x] 2.2 In each write method (`WriteFrameAsync` both overloads, `WriteFramesAsync`, `WriteFrameSegmentsAsync`, and the private helpers at lines 240-264), pass the caller's token to `_WriteLock.WaitAsync` only. Inside the lock, call `_Stream.WriteAsync` and `FlushAsync` with the connection token. (Codex, 2026-10-19)
- [x] 2.3 Add an internal faulted state. Any exception thrown after commit sets it, disposes or aborts the transport, and rethrows. Writes that start or are queued after the fault throw `IOException` right away. The existing `catch (IOException)` in `ProcessPendingRequestAsync` already handles that as a terminated request. (Codex, 2026-10-19)
- [x] 2.4 Add an internal `Http2WriteNotCommittedException` (one class per file, `internal sealed`, derives from `OperationCanceledException`). The writer throws it when the caller's token cancels the lock wait, or when the terminal check in Phase 3 rejects a batch after the lock is taken. It guarantees that no byte was written. Since it is an `OperationCanceledException`, existing callers' catch blocks behave exactly as before. (Codex, 2026-10-19)
- [x] 2.5 XML docs on the writer describe the commit rule and the thread-safety guarantee (Code Style requires documenting thread safety). (Codex, 2026-10-19)
- [ ] 2.6 Un-skip T2.x. All pass.

Notes:

## Phase 3: reservation ownership and terminal ordering

Reserved but unwritten credit belongs to the response until the writer accepts the batch. Every exit before acceptance refunds it exactly once. Nothing is refunded after acceptance. Once a stream is terminal, no new batch for it is accepted, so nothing follows RST_STREAM.

- [x] 3.1 Add an internal per-stream write gate that both the session and the response can see. Put it in a new internal class (for example `Http2StreamWriteGate`, one class per file) owned by the session and passed to `Http2Response` through an internal constructor overload. Don't add a public member to `Http2StreamStateMachine` or change the public `Http2Response` constructor (R1). The gate is a single `int` state with three values: `Active`, `Committing`, `Terminal`. Every transition uses `Interlocked.CompareExchange`; no code reads a flag and then acts on it separately. The rules: (Codex, 2026-10-19)
  - **Commit** (writer, under `_WriteLock`): `Active -> Committing`. If that fails because the state is `Terminal`, the batch is rejected. After the batch is written: `Committing -> Active`.
  - **Terminate** (peer reset or local abandonment): `Active -> Terminal`. If the state is `Committing`, the terminator does not wait or spin: it sets a pending-terminate bit (a second `Interlocked` field), and the committing writer moves the state to `Terminal` instead of `Active` when it finishes. Either way exactly one side decides, and a committed batch always finishes whole.
- [x] 3.2 Add an internal per-batch reservation owner holding the count of reserved, unwritten connection bytes. `Http2Response` creates one per batch. Every successful reservation adds to it with `Interlocked.Add`. (Codex, 2026-10-19)
- [x] 3.3 Add an internal overload of `WriteFrameSegmentsAsync` that takes the write gate and the reservation owner. After acquiring `_WriteLock` and before writing anything, try the commit transition: (Codex, 2026-10-19)
  - if it fails (gate terminal): release the lock and throw `Http2WriteNotCommittedException`;
  - otherwise: claim the owner's count with `Interlocked.Exchange(ref count, 0)` (the commit), then write.
- [x] 3.4 In both DATA paths of `Http2Response`, wrap batch assembly and submission in `try`/`finally`. In `finally`, claim the owner's count with `Interlocked.Exchange(ref count, 0)` and refund whatever was claimed to `_ConnectionSendWindow` under `_FlowControlLock`, then signal `_SendWindowSignal`. Expose the refund as an internal callback from the session, alongside the existing `ReserveSendWindowAsync` delegate. Commit and refund both claim the count with the same `Exchange`, so exactly one of them gets the bytes: no double refund, and no refund after commit. The abandonment path never refunds on a batch's behalf; only the batch's own `finally` does. This covers every pre-commit exit: source stream read failure, `SendData` state exception, cancellation while reserving, lock-wait cancellation, and terminal rejection. (Codex, 2026-10-19)
- [x] 3.5 Refund connection credit only. A terminal stream's window is gone. For a stream that is still active, Phase 4 makes abandonment terminal, so there is never a live stream that needs its own credit back. (Codex, 2026-10-19)
- [x] 3.6 `HandleRstStreamFrame` (peer reset) runs the terminate transition **before** cancelling the request token and removing flow-control state. (Codex, 2026-10-19)
- [x] 3.7 Local abandonment (Phase 4) runs the terminate transition first, then sends RST_STREAM through the writer. A batch that already won the lock finishes before the RST. A batch that takes the lock afterwards is rejected and refunded. (Codex, 2026-10-19)
- [x] 3.8 HEADERS and trailers go through the same gate check, so a response that has not started gets no HEADERS after the stream is reset. (Codex, 2026-10-19)
- [ ] 3.9 Un-skip T3.x. All pass.

Notes:

## Phase 4: reset isolation

Nothing the application's handler does should be able to close the connection. GOAWAY belongs to the frame reader, which is the only code that can see a genuine peer protocol violation.

- [x] 4.1 In `Http2Response`, every write entry point (`Send` overloads, `SendChunk`, `SendEvent`, trailers) checks the write gate first. If the stream is terminal because the peer reset it, set `TransportFailure` and throw `OperationCanceledException` using the request token. `Http2WriteNotCommittedException` from the writer is handled the same way. Per R4, `WebserverBase` then marks the request terminated, raises `RequestAborted`, and skips `Routes.Exception`. (Codex, 2026-10-19)
- [x] 4.2 Check that `WebserverBase.ProcessHttpContextAsync` needs no change. Its `catch (OperationCanceledException)` at line 869 rethrows after marking the request terminated. The `finally` block skips the fallback 500 because `RequestAborted` is set. If a route type (API route, middleware, content route) has its own catch that turns the cancellation into a 500 or calls `Routes.Exception`, fix it there with an exception filter (`when (WasRequestTerminated(ctx))`) and record it here. (Codex, 2026-10-19)
- [x] 4.3 In `ProcessPendingRequestAsync` (`Http2ConnectionSession.cs:393`): (Codex, 2026-10-19)
  - add `catch (OperationCanceledException)`: mark the request terminated, then apply the reset rule below with `CANCEL`.
  - change `catch (Http2StreamStateException)` and `catch (Http2ProtocolException)`: apply the reset rule below with `INTERNAL_ERROR`. Never call `SendGoAwayAsync` here.
  - **Reset rule.** Send `RST_STREAM` only when the server is abandoning a response it has not finished. Decide from the actual stream state and response outcome, not from whether END_STREAM was sent. Sending END_STREAM only moves the stream to half-closed (local), where RST_STREAM is still legal (§5.1; §8.1 even describes `RST_STREAM(NO_ERROR)` after a complete response). So:
    - the peer already reset the stream (gate terminal from 3.6): send nothing. §5.4.2 forbids answering RST_STREAM with RST_STREAM.
    - the stream state machine is closed (both sides sent END_STREAM): send nothing; §5.1 allows only PRIORITY on a closed stream.
    - the response completed (END_STREAM sent, for example the 408 from the timeout path, and the exception came afterwards): send nothing. This matches 7.3.1 and keeps R3 and T4.7 intact. Do not send `RST_STREAM(NO_ERROR)` to stop an unfinished upload either; 7.3.1 doesn't, and it is out of scope.
    - otherwise (response started or not started, but not finished): run the terminate transition, then send `RST_STREAM` with the code above.
  - Confirm `ObserveRequestTaskAsync` (line 1255) treats a rethrown `OperationCanceledException` as normal completion and doesn't log it as an error or tear down the connection.
- [x] 4.4 A handler that returns without sending, on a stream nobody reset, still gets today's fallback 500. Don't change that path. (Codex, 2026-10-19)
- [x] 4.5 RST_STREAM sent by the server uses the connection token and goes through the writer like any other frame. (Codex, 2026-10-19)
- [ ] 4.6 Un-skip T4.x. All pass.
- [x] 4.7 Narrow Phase 1's malformed-PRIORITY rule now that the write gate exists: payload length not 5 on a stream that is tracked in `_Streams` and open or half-closed gets `RST_STREAM(FRAME_SIZE_ERROR)` on that stream, through the terminate transition, so its handler's later writes fail per 4.1. Every other malformed PRIORITY keeps `GOAWAY(FRAME_SIZE_ERROR)`. Un-skip T1.8. (Codex, 2026-10-19)
- [x] 4.8 **Handler-work bound (Rapid Reset, CVE-2023-44487).** After this release, client resets no longer close the connection, so a client can open and reset streams cheaply. RFC 9113 §5.1.2 says reset streams are closed and stop counting toward `SETTINGS_MAX_CONCURRENT_STREAMS`, and that must stay true. The bound on application work is separate, and 7.3.1 already has one: `DispatchRequestAsync` (`:382`) refuses a new stream with `REFUSED_STREAM` when `_ActiveRequestTasks.Count >= MaxConcurrentStreams`, and a reset stream's task stays in `_ActiveRequestTasks` until its handler actually finishes (`:1270`). Keep it that way: (Codex, 2026-10-19)
  - do not remove a task from `_ActiveRequestTasks` in `HandleRstStreamFrame` or anywhere other than `ObserveRequestTaskAsync`;
  - do not reuse this count for the stream count advertised in SETTINGS or for the §5.1.2 check.
  - T4.12 guards it. `REFUSED_STREAM` tells the client the request was not processed and is safe to retry (§8.7), so well-behaved clients are not disrupted.
  - Rate limits for ignored frames and resets (`ENHANCE_YOUR_CALM`, §10.5; CVE-2019-9512, 9514, 9518) go in a follow-up issue. Record its number here: `#199`.

Notes:

## Phase 5: flow-control batching liveness (separate issue, see 0.1)

The rule for both DATA paths: **never wait for flow-control credit while holding reservations that haven't been written.** Batching stays, because it saves lock round-trips and flushes.

- [x] 5.1 Add an internal non-blocking reserve (`TryReserveSendWindow`) next to `ReserveSendWindowAsync`. It takes whatever credit is available right now, up to the requested amount, and returns 0 if there is none. (Codex, 2026-10-19)
- [x] 5.2 In `WriteDataFramesAsync(byte[], ...)`: the first reservation of each batch may wait (`ReserveSendWindowAsync`). The rest use `TryReserveSendWindow` up to a batch limit. When the try returns 0 or the limit is reached, submit the batch (through the Phase 3 overload) and start a new batch. (Codex, 2026-10-19)
- [x] 5.3 Do the same in `WriteDataFramesFromStreamAsync`. The buffer stays `min(_PeerMaxFrameSize, 65536)`; the change is to submit before waiting. (Codex, 2026-10-19)
- [ ] 5.4 Batch limit: use the existing contiguous-write or buffer constant, whichever the current code uses. If none applies, an internal constant equal to today's effective batch size. Not a public setting (R1).
- [x] 5.5 The flow-control wait never happens while `_WriteLock` is held. Confirm by reading the code and with T5.4. (Codex, 2026-10-19)
- [x] 5.6 Un-skip T5.x. Run them at both `MAX_FRAME_SIZE` 16,384 and 65,536. All pass. (Codex, 2026-10-19)
- [x] 5.7 Golden check T5.5 matches the 7.3.1 capture from 0.6a. If it does not, R3 is broken for in-window responses: stop and report. (Codex, 2026-10-19)

If Phase 5 is not green when Phases 1 to 4 are, ship without it and leave the separate issue open. It does not affect #197.

Notes:

## Phase 6: advertise `SETTINGS_NO_RFC7540_PRIORITIES` (optional, owner decision)

RFC 9218 §2.1 setting 0x9 with value 1 tells the client that Watson ignores RFC 7540 priorities, so compliant clients stop sending PRIORITY. It is a nice-to-have and must not replace Phase 1: older Firefox versions and intermediaries can still send PRIORITY.

It is also the only part of this plan that changes bytes on every HTTP/2 connection (one extra 6-byte setting in the server's initial SETTINGS frame). Clients ignore settings they don't recognize (RFC 9113 §6.5.2), but for the strictest reading of "zero disruption" the recommendation is **defer to the next minor release**, where it can also become a public opt-out on `Http2Settings`.

- [x] 6.1 Owner decision recorded here: `ship in 7.3.2` / `defer`. (Codex, 2026-10-19)
- [ ] 6.2 If shipping: emit the setting internally by casting `(Http2SettingIdentifier)9` in the settings writer. Don't add an enum member (R1). Add T6.1.

Notes:

## Phase 7: HTTP/3 audit

RFC 9114 §9 also requires ignoring unknown frame types. A first read shows `Core/Http3/Http3MessageSerializer.cs:44` documents "Unknown frame types are ignored" for request streams, and `Http3ConnectionSession.cs:236` only insists that the first control-stream frame is SETTINGS, which is correct. No change is expected.

- [x] 7.1 Confirm unknown and reserved frame types (`0x1f * N + 0x21`) on request streams and the control stream are ignored after SETTINGS, and that HTTP/2-only types (0x2 PRIORITY, 0x6, 0x8, 0x9) are a connection error (`H3_FRAME_UNEXPECTED`) as §7.2.8 requires. (Codex, 2026-10-19)
- [ ] 7.2 Confirm a client stream reset (`STOP_SENDING` / `RESET_STREAM`) while the handler is still running doesn't close the QUIC connection. If it does, file a separate issue; don't fix it in this release.
- [x] 7.3 Confirm an unknown unidirectional stream type is ignored (read and discarded, or aborted with `STOP_SENDING(H3_STREAM_CREATION_ERROR)`) and does not close the connection (RFC 9114 §6.2.3). Reserved stream types (`0x1f * N + 0x21`) are the common case. (Codex, 2026-10-19)
- [x] 7.4 Confirm unknown SETTINGS identifiers are ignored, and that HTTP/2 setting identifiers 0x02, 0x03, 0x04 and 0x05 in an HTTP/3 SETTINGS frame give `H3_SETTINGS_ERROR` (RFC 9114 §7.2.4.1). (Codex, 2026-10-19)
- [x] 7.5 Confirm RFC 9218 PRIORITY_UPDATE frames (0xF0700 request stream, 0xF0701 push stream) on the control stream are ignored and do not close the connection. (Codex, 2026-10-19)
- [x] 7.6 Record findings here and in the release notes. Anything non-compliant goes in a separate issue, not this release. (Codex, 2026-10-19)

Notes:

## Phase 8: release

- [x] 8.1 **Version approval.** `Agents/requirements/VERSIONING.md` says agents never change version numbers without explicit approval. Ask the repository owner to approve 7.3.1 -> 7.3.2 for `Watson` (and `Watson.Clients` in lockstep, as in 7.3.0 and 7.3.1). Record approval here before editing `src/WatsonWebserver/WatsonWebserver.csproj`. (Codex, 2026-10-19)
- [x] 8.2 Build `src/WatsonWebserver.sln` in Debug and Release with zero warnings and zero errors. (Codex, 2026-10-19)
- [x] 8.3 Public API check (R1): build once with `<EnablePackageValidation>true</EnablePackageValidation>` and `<PackageValidationBaselineVersion>7.3.1</PackageValidationBaselineVersion>` in `WatsonWebserver.csproj`, confirm no API differences, then revert those two lines. Paste the result here. (Codex, 2026-10-19)
- [x] 8.4 (Codex, 2026-10-19) Run `Test.Automated`, `Test.XUnit` and `Test.Nunit` on every target framework they support. Zero failures, zero skips in `Http2Resilience` (except Phase 6 if deferred).
- [ ] 8.5 Run `Test.Benchmark --targets Watson7 --protocols http2 --scenarios hello,json` three times on 7.3.1 and three times on the branch, on the same machine. Record medians here. The branch must be within noise (R6).
- [x] 8.6 Publish and run `Test.Aot` with `--require-native` for `net8.0` and `net10.0`. (Codex, 2026-10-19)
- [x] 8.7 Rerun the issue's repro (0.2) against the branch. All of A to F pass, over h2c and TLS. (Codex, 2026-10-19)
- [ ] 8.7a Run [h2spec](https://github.com/summerwind/h2spec) against a 7.3.1 build and against the branch, h2c and TLS, and paste both summaries here. It covers §4.1, §5.5, §6.3 and §6.10 among others. It is supporting evidence, not a release gate: the branch must not fail any case 7.3.1 passed, and cases still failing are triaged here against RFC 9113. h2spec's checks come from RFC 7540, so RFC 7540-only priority checks (for example a stream depending on itself) are recorded as "not applicable under RFC 9113", not as blockers.
- [ ] 8.8 Manual browser check: serve a page with about 50 subresources over TLS HTTP/2. In Firefox (with `about:logging`, "HTTP" preset), load it, reload it repeatedly mid-load, and navigate away mid-load. No GOAWAY in the log, and every load completes. Repeat once in Chrome or Edge as a control, and note whether Chrome sent any standalone PRIORITY frame (this checks the claim in "What is wrong"). Record browser versions here.
- [x] 8.9 `CHANGELOG.md`: set the current version to `v7.3.2` and add a `## v7.3.2` entry in the style of v7.3.1. Suggested text: (Codex, 2026-10-19)

  > - Fixed: with HTTP/2 enabled, a PRIORITY frame, a PRIORITY_UPDATE frame, or any unknown frame type closed the whole connection with `GOAWAY(INTERNAL_ERROR)`, so pages loaded partially or failed in Firefox ([#197](https://github.com/dotnet/WatsonWebserver/issues/197)). These frames are now ignored as RFC 9113 requires. A malformed PRIORITY frame is a `FRAME_SIZE_ERROR`, and an orphaned CONTINUATION or client PUSH_PROMISE is a `PROTOCOL_ERROR`
  > - Fixed: on HTTP/2, a client cancelling one stream (`RST_STREAM`) while its handler was still running closed the connection with `GOAWAY(PROTOCOL_ERROR)` and failed every other request on it ([#197](https://github.com/dotnet/WatsonWebserver/issues/197)). The cancelled request now ends as an aborted request and other streams are unaffected
  > - Fixed: on HTTP/2, a cancelled request could interrupt a frame halfway through, leak connection flow-control window, or send DATA after the stream was reset. Each of these could break or stall other streams on the same connection
  > - Fixed: on HTTP/2, a response larger than the client's flow-control window could stall when the client used the default 65,535-byte window ([#____](https://github.com/dotnet/WatsonWebserver/issues/____))
  > - Added the `Http2Resilience` suite to `Test.Shared` (N cases, wired into all three runners)
  >
  > ### Compatibility notes
  >
  > - No public API, setting, or default changes. Successful HTTP/2 exchanges return the same status, headers and body as before; responses larger than the client's flow-control window may be split into DATA frames differently. HTTP/1.1 and HTTP/3 are untouched
  > - When an HTTP/2 client cancels a stream, a handler's later `Send` throws `OperationCanceledException`, the same as when the handler observes `ctx.Token` or an HTTP/1.1 client disconnects. `Events.RequestAborted` fires, and `Routes.Exception` and `Events.ExceptionEncountered` no longer fire for that request. In 7.3.1 they fired with an `Http2StreamStateException` just before the connection was torn down
  > - Code that caught `Http2StreamStateException` around a `Send` call on HTTP/2 now sees `OperationCanceledException` when the client has reset the stream
  > - Frames a client sends on a stream after the server reset it are now ignored, as RFC 9113 requires. In 7.3.1 they closed the connection

- [x] 8.10 `TESTING_HTTP2_HTTP3.md`: add the new suite and how the repro was verified. `README.md`: no change expected; confirm nothing there describes the old behavior. (Codex, 2026-10-19)
- [ ] 8.11 No em-dashes anywhere in code comments, XML docs, CHANGELOG, or commit messages. Follow `CLAUDE.md` code rules (no `var`, no tuples, `_PascalCase` privates, one class per file, `using` statements inside the namespace, `ConfigureAwait(false)`, XML docs on public members only).
- [ ] 8.12 Comment on #197 with a summary of the fix and the release that contains it. Do this, commit, tag, and publish only when the repository owner asks.
- [ ] 8.13 Move this file to `archive/HTTP2_ISSUE_197.md` (or delete it) after the release ships.

**Ship boundary.** If Phase 1 is done and Phases 1b to 4 are not, the owner may ship Phase 1 alone as 7.3.2. It fixes the Firefox symptom and every T1 case except T1.8 passes (T1.8 needs step 4.7). In that case #197 stays open with a note that RST_STREAM isolation is pending. Phases 1b, 2, 3 and 4 never ship separately from each other.

## Test plan

All cases live in `src/Test.Shared/SharedHttp2ResilienceTests.cs`, registered as the `Http2Resilience` suite in `WatsonTestSuites.cs`. They use raw frames over `TcpClient` to `127.0.0.1` with h2c prior knowledge. Cases marked (TLS) run a second time over TLS with ALPN `h2`, using `LoopbackCertificateFactory`, because the issue reproduced on both and TLS changes write behavior. "No GOAWAY" means the frame log contains no GOAWAY frame and the connection is still open at the end of the case, checked by sending a PING and getting the ACK.

"Fails on 7.3.1" is a check on the test itself. Each case marked yes must fail on the baseline in Phase 0.7. If it passes, the test is not reproducing the bug and has to be fixed before the production change.

### T0: golden frames (R3)

| ID | Scenario | Expected | Fails on 7.3.1 | Done |
|----|----------|----------|----------------|------|
| T0.1 | Default client settings: GET returning 200 with a 1 KB body, a 32 KB body, a header block that needs CONTINUATION, a chunked response, and a response with trailers, each on its own stream | Frame sequence (types, flags, lengths, payload bytes) identical to the 7.3.1 capture from 0.6a | no (guard) | [ ] |

### T1: frame dispatch (Phase 1)

| ID | Scenario | Expected | Fails on 7.3.1 | Done |
|----|----------|----------|----------------|------|
| T1.1 | Issue B (TLS): PRIORITY on idle streams 3, 5, 7, 9, 11, 13, then GET `/fast` on 15 | 200 on 15, no GOAWAY | yes | [ ] |
| T1.2 | Issue C (TLS): GET `/slow` on 1, PRIORITY on 1 while it's open | 200 on 1, no GOAWAY | yes | [ ] |
| T1.3 | Issue D: PRIORITY_UPDATE (0x10) on stream 0, then GET `/fast` | 200, no GOAWAY | yes | [ ] |
| T1.4 | Issue E: unknown type 0xFA on stream 0 with a 100-byte payload, then GET `/fast` | 200, no GOAWAY, next frame parsed correctly | yes | [ ] |
| T1.5 | Unknown type 0xFA on an open stream 1 with flags 0xFF, then that stream's request completes | 200, no GOAWAY | yes | [ ] |
| T1.6 | Phantom streams don't count: GET on 1 (completes), PRIORITY on 3 to 13, then PRIORITY on stream 0 to force a GOAWAY | GOAWAY(PROTOCOL_ERROR) whose last-stream-ID is 1, not 13 | yes | [ ] |
| T1.7 | PRIORITY with payload length 4 on idle stream 5 | GOAWAY(FRAME_SIZE_ERROR) with last-stream-ID 0 (nothing processed), not 5 | yes (code was INTERNAL_ERROR) | [ ] |
| T1.8 | PRIORITY with payload length 6 on open stream 1, plus GET on 3 | RST_STREAM(FRAME_SIZE_ERROR) on 1, 200 on 3, no GOAWAY, no frame for 1 after the RST. Enabled in step 4.7; in a Phase-1-only release the expected result is GOAWAY(FRAME_SIZE_ERROR) | yes | [ ] |
| T1.9 | PRIORITY on stream 0 | GOAWAY(PROTOCOL_ERROR) | yes (code was INTERNAL_ERROR) | [ ] |
| T1.10 | HEADERS without END_HEADERS on 1, then PRIORITY on 1, then CONTINUATION | GOAWAY(PROTOCOL_ERROR) (interleaving) | no (guard; `ReadHeaderBlockAsync` already rejects it) | [ ] |
| T1.11 | Orphaned CONTINUATION on stream 1 with no open header block | GOAWAY(PROTOCOL_ERROR) | yes (code was INTERNAL_ERROR) | [ ] |
| T1.12 | Client PUSH_PROMISE on stream 1 | GOAWAY(PROTOCOL_ERROR) | yes (code was INTERNAL_ERROR) | [ ] |
| T1.13 | 1,000 valid PRIORITY frames on distinct idle streams, then GET | 200, no GOAWAY; server's tracked stream count unchanged afterwards (check via `WebserverStatistics` or the active-stream gauge, whichever exposes it) | yes | [ ] |
| T1.14 | HEADERS without END_HEADERS on 1, then unknown type 0xFA on 1, then CONTINUATION | GOAWAY(PROTOCOL_ERROR) (§5.5: extension frames inside a header block are a connection error) | no (guard against Phase 1 "ignoring" it) | [ ] |

### T1b: receive side after a server-sent RST_STREAM (Phase 1b)

These need the server to reset a stream while the client is still sending. Use a handler that throws (or a route with a short timeout) before reading the request body, so Phase 4 sends RST_STREAM while the client uploads.

| ID | Scenario | Expected | Fails on 7.3.1 | Done |
|----|----------|----------|----------------|------|
| T1b.1 | POST on 1 with a 200 KB body; the server resets 1 after the first DATA; the client keeps sending the rest of the body; GET on 3 | No GOAWAY; 3 gets 200; the server sends connection WINDOW_UPDATEs covering every DATA byte the client sent on 1 (the client's connection send window is not leaked) | yes | [ ] |
| T1b.2 | Same, with padded DATA frames on 1 after the reset | Connection WINDOW_UPDATE includes the padding (§6.9.1) | yes | [ ] |
| T1b.3 | POST on 1 without END_STREAM; the server resets 1; the client sends trailing HEADERS on 1 that add a new HPACK dynamic-table entry; then GET on 3 whose header block refers to that entry by index | No GOAWAY; 3 gets 200 with the right header value (proves the trailers were decoded and the dynamic table stayed in step, §4.3) | yes | [ ] |
| T1b.4 | Server resets 1, then the client sends RST_STREAM and WINDOW_UPDATE on 1 | Both ignored, no GOAWAY, no server RST in reply | check in 0.7 | [ ] |
| T1b.5 | 200 server resets on one connection, then DATA on the oldest reset stream (evicted from the record) | Ignored with connection-window accounting, no GOAWAY | yes | [ ] |
| T1b.6 | DATA on stream 7 when the highest stream opened is 3 (truly idle) | GOAWAY(PROTOCOL_ERROR), unchanged from 7.3.1 | no (guard) | [ ] |
| T1b.7 | Client opens HEADERS on stream 1, it completes, then sends a new HEADERS on stream 1 (stream ID reuse) | GOAWAY(STREAM_CLOSED) | check in 0.7 | [ ] |

### T2: writer commit rule (Phase 2)

T2.1 to T2.3 drive `Http2ConnectionWriter` directly over `GatedWriteStream`, so they are deterministic and need no sleeps.

| ID | Scenario | Expected | Fails on 7.3.1 | Done |
|----|----------|----------|----------------|------|
| T2.1 | Writer: start a 3-frame HEADERS+CONTINUATION write, block the stream after frame 1, cancel the caller's token, release | All 3 frames written intact; the call completes or throws only after the full block is written | yes | [ ] |
| T2.2 | Writer: write a frame larger than the contiguous-write limit, block between header and payload, cancel the caller's token, release | Header and full payload written | yes | [ ] |
| T2.3 | Writer: hold the lock with write A, queue write B with a token, cancel B's token, release A | B throws an `OperationCanceledException` (`Http2WriteNotCommittedException`); zero bytes from B on the stream | no (passes today, guards regression) | [ ] |
| T2.4 | Wire (TLS): response with a header block larger than `MAX_FRAME_SIZE` (forcing CONTINUATION) on 1, client sends RST_STREAM(CANCEL) on 1 while it's sent, sibling GET on 3 | All frames parse; 3 gets 200; no GOAWAY | flaky on 7.3.1 by timing; keep as a regression guard | [ ] |
| T2.5 | Writer: transport write throws after commit | Writer faulted; the next write throws `IOException` immediately; no further bytes written | yes | [ ] |

### T3: reservation ownership and terminal ordering (Phase 3)

| ID | Scenario | Expected | Fails on 7.3.1 | Done |
|----|----------|----------|----------------|------|
| T3.1 | Credit leak: client window 65,535. Stream 1 handler sends 200 KB. Client reads part of it without sending WINDOW_UPDATE, then RST_STREAM(CANCEL) on 1 and connection WINDOW_UPDATE for exactly the bytes received. GET on 3 for a body equal to the full 65,535-byte connection window | 3 completes within 10 s | yes (3 stalls) | [ ] |
| T3.2 | Same as T3.1 but the reset arrives while the stream-1 handler is between reserving and writing (use a handler that writes from a source stream gated by the test) | 3 completes within 10 s | yes | [ ] |
| T3.3 | Ordering, peer reset: hold the writer busy (a sibling streaming a large response), queue a DATA batch for 1, client sends RST_STREAM on 1 while the batch is queued, release | No DATA frame for stream 1 appears after the client's RST_STREAM is processed; sibling still has its full window | yes | [ ] |
| T3.4 | Ordering, local abandonment: same setup, but the stream-1 handler throws after its batch is queued | Server RST_STREAM on 1; no DATA for 1 after it; sibling completes | yes | [ ] |
| T3.5 | Source stream throws partway through `Send(long, Stream)` on 1 | RST_STREAM(INTERNAL_ERROR) on 1; sibling on 3 then sends a response equal to the remaining connection window and completes | yes | [ ] |
| T3.6 | Race, repeated 1,000 times: a batch for 1 is waiting for `_WriteLock` while the client's RST_STREAM on 1 and the handler's own failure both try to terminate it | Connection send window after each iteration equals what it was before the batch was reserved (no credit lost, none counted twice); at most one server RST_STREAM on 1, and none if the client's arrived first | yes | [ ] |

### T4: reset isolation (Phase 4)

| ID | Scenario | Expected | Fails on 7.3.1 | Done |
|----|----------|----------|----------------|------|
| T4.1 | Issue F (TLS): GET `/slow` on 1 and 3, RST_STREAM(CANCEL) on 1 after 500 ms | 3 gets 200, no GOAWAY, no server RST_STREAM on 1 (§5.4.2) | yes | [ ] |
| T4.2 | Same as T4.1 with `Routes.Exception` set, plus `Events.RequestAborted` and `Events.ExceptionEncountered` handlers | `RequestAborted` fires once for 1; `Routes.Exception` and `ExceptionEncountered` don't fire for 1 | yes | [ ] |
| T4.3 | RST_STREAM mid-body: handler on 1 sends 1 MB in `SendChunk` calls ignoring `ctx.Token`; client resets 1 after the first DATA; GET on 3 and 5 | 3 and 5 get 200; no GOAWAY; handler's next `SendChunk` throws `OperationCanceledException` | yes | [ ] |
| T4.4 | Same as T4.3 using SSE (`SendEvent`) | Same | yes | [ ] |
| T4.5 | Same as T4.3 using `Send(long, Stream)` | Same | yes | [ ] |
| T4.6 | API route (`server.Get`) on 1 that ignores the token, client resets 1, sibling on 3 | 3 gets 200; no 500 or GOAWAY for 1 | yes | [ ] |
| T4.7 | API-route timeout (`Settings.Timeout.DefaultTimeout` short) on HTTP/2 with no client reset | 408 response as today, connection stays open, no server RST_STREAM after the 408's END_STREAM | no (guards existing behavior) | [ ] |
| T4.8 | Handler throws an ordinary exception on 1 (no reset), `Routes.Exception` set | `Routes.Exception` fires and its response is sent, as today | no (guards existing behavior) | [ ] |
| T4.9 | Handler returns without sending on 1 (no reset) | Fallback 500, as today | no (guards existing behavior) | [ ] |
| T4.10 | Handler that respects `ctx.Token` (`Task.Delay(..., ctx.Token)`), client resets 1, sibling on 3 | 3 gets 200, `RequestAborted` fires, no GOAWAY | check in 0.7 | [ ] |
| T4.11 | Soak: 20 iterations of 10 concurrent streams where every other stream is reset at a random point in its response, on one connection | Every non-reset stream gets a complete, correct body; no GOAWAY; the connection window after the run equals what the client granted minus what it received | yes | [ ] |
| T4.12 | Rapid Reset guard: `MaxConcurrentStreams` = 100, handler ignores `ctx.Token` and sleeps 5 s. Client opens and immediately resets 1,000 streams, then GET on a fresh stream | Concurrently running handlers never exceed 100; streams over the limit get `REFUSED_STREAM`; after the handlers drain, the GET gets 200. Run on 7.3.1 too and record its result here as the baseline | no (guard; record the 7.3.1 baseline) | [ ] |
| T4.13 | Handler throws after its response sent END_STREAM, while the client is still uploading a body (stream half-closed local) | No server RST_STREAM (response completed, matches 7.3.1); client upload ignored per Phase 1b; no GOAWAY | check in 0.7 | [ ] |

### T5: batching liveness (Phase 5)

Except in T5.5, the client sends WINDOW_UPDATE only after receiving DATA (for the bytes received), never in advance. In T5.1 to T5.3 the connection window also stays at 65,535, but the stream window alone is what triggers the deadlock.

| ID | Scenario | Expected | Fails on 7.3.1 | Done |
|----|----------|----------|----------------|------|
| T5.1 | Client window 65,535, `MAX_FRAME_SIZE` 16,384, `Send(byte[])` of 100,000 bytes | Completes, body correct | yes | [ ] |
| T5.2 | Same with `MAX_FRAME_SIZE` 65,536 | Completes, body correct | yes | [ ] |
| T5.3 | Client window 65,535, `MAX_FRAME_SIZE` 65,536, `Send(long, Stream)` of 200,000 bytes | Completes, body correct | yes | [ ] |
| T5.4 | Same as T5.1 with a sibling stream requesting a small response while stream 1 waits for credit | Sibling gets its response (proves the credit wait doesn't hold the writer lock) | check in 0.7 | [ ] |
| T5.5 | Client sets `SETTINGS_INITIAL_WINDOW_SIZE` to 16 MB **and** sends WINDOW_UPDATE on stream 0 raising the connection window to 16 MB (`SETTINGS_INITIAL_WINDOW_SIZE` never changes the connection window, §6.9.2); then a 4 MB `Send(byte[])` | Frame sequence identical to the 7.3.1 capture from 0.6a (R3 guard for the common browser case) | no (guard) | [ ] |

### T6: optional setting (Phase 6, only if shipped)

| ID | Scenario | Expected | Fails on 7.3.1 | Done |
|----|----------|----------|----------------|------|
| T6.1 | Read the server's initial SETTINGS frame | Contains 0x9 = 1; all other parameters unchanged | yes | [ ] |

### Regression suites (must pass unedited)

Don't edit an existing test to make it pass. If one fails, the change is wrong.

- [ ] `Http2Smoke` (`SharedHttp2SmokeTests`), especially "Continuation Header Block Request" and "Padded Priority Headers And Data Request", which exercise the paths next to Phase 1
- [ ] HTTP/2 cases in `BodyAccess` (`SharedBodyAccessTests`), including the 32 KB and 48 KB multi-frame bodies
- [ ] `ChunkTerminator` (`SharedChunkTerminatorTests`), including its HTTP/2 empty-final-chunk cases
- [ ] `ProtocolGapSharedTests`, `SharedCoreUnitTests`, `LegacyCoverageSuite`, `SharedTelemetryTests` (abort and exception counters must match the new semantics only for reset HTTP/2 streams)
- [ ] Everything else in `WatsonTestSuites.All`
- [ ] `Test.Aot --require-native` on `net8.0` and `net10.0`
- [ ] The issue's repro A to F, h2c and TLS (8.7)
- [ ] Firefox and Chrome manual check (8.8)

## Out of scope

- Implementing RFC 9218 priorities (acting on PRIORITY_UPDATE or the `priority` header). Watson ignores priority, which the RFCs allow.
- Any public setting for the batching limit or for the new PRIORITY behavior.
- HTTP/3 code changes (Phase 7 only audits).
- Bumping any version without the owner's approval (8.1).
