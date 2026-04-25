# Disconnect Detection Audit

## Scope

This document audits how Watson 7 currently detects and reports client disconnects, request aborts, and failed response sends.

The findings below are based on the current source in `C:\code\dotnet\watsonwebserver-7.0`, with special attention to the HTTP/1.1 path because that is the path exercised by the observed Jurat failures:

- large `GET` response
- response send begins
- transport becomes unwritable mid-send
- Watson falls back to its default HTML `500` page
- downstream application does not receive a positive disconnect signal

## Executive Summary

Watson 7 does not currently provide reliable disconnect detection for HTTP/1.1 response transport failures.

There are five concrete issues:

1. `RequestorDisconnected` is declared but never fired.
2. `RequestAborted` is only set for `OperationCanceledException`, not for normal I/O disconnects.
3. HTTP/1.1 response send failures are swallowed and reduced to `false`, losing the transport exception and bypassing abort/disconnect signaling.
4. HTTP/1.1 read-side `IOException` is silently dropped with no disconnect/abort event.
5. `ResponseSent` is emitted even when the response was not actually sent.

These behaviors explain why downstream applications can observe a failed large response send without seeing `RequestAborted == true` or a disconnect event.

## Verified Findings

### 1. `RequestorDisconnected` is dead API surface

`RequestorDisconnected` is declared in:

- `src/WatsonWebserver/Core/WebserverEvents.cs:44`

However:

- there is no `HandleRequestorDisconnected(...)` method
- there is no `HasRequestorDisconnectedHandlers` property
- there is no call site anywhere under `src` that raises the event

Repository search confirms this:

- `RequestorDisconnected` exists only in:
  - `src/WatsonWebserver/Core/WebserverEvents.cs`
  - generated XML docs

Impact:

- applications can subscribe to `RequestorDisconnected`, but it will never fire
- any downstream logic depending on this signal is currently non-functional

This is not a timing problem. It is a missing implementation problem.

### 2. `RequestAborted` is only set on `OperationCanceledException`

The only place where Watson sets `ctx.RequestAborted = true` is:

- `src/WatsonWebserver/Core/WebserverBase.cs:809-815`

That code path is:

- catch `OperationCanceledException`
- set `ctx.RequestAborted = true`
- emit `RequestAborted`

This means:

- ordinary socket disconnects
- broken pipe
- connection reset
- write/flush failure during response send
- read-side `IOException`

do **not** automatically result in `RequestAborted = true`.

Impact:

- downstream consumers relying on `ctx.RequestAborted` will miss many real disconnects
- disconnect classification becomes biased toward cancellation-token cases only

### 3. HTTP/1.1 response send failures are swallowed and collapsed to `false`

The HTTP/1.1 response implementation is:

- `src/WatsonWebserver/HttpResponse.cs`

Normal string send path:

- `Send(string)` at `HttpResponse.cs:196-205`
- `SendPayloadAsync(...)` at `HttpResponse.cs:621-672`

Normal streamed/body send path:

- `Send(long, Stream, ...)` at `HttpResponse.cs:219-223`
- `SendInternalAsync(...)` at `HttpResponse.cs:537-618`

In both cases, the actual write/flush happens inside a `try`, and any exception is swallowed:

- payload send:
  - write at `HttpResponse.cs:653`
  - flush at `HttpResponse.cs:657`
  - catch-all returns `false` at `HttpResponse.cs:669-671`

- stream send:
  - stream writes at `HttpResponse.cs:574`
  - flush at `HttpResponse.cs:597`
  - catch-all returns `false` at `HttpResponse.cs:609-617`

Consequences:

- Watson does not preserve the underlying transport exception
- Watson does not set `RequestAborted`
- Watson does not emit `RequestAborted`
- Watson does not emit `RequestorDisconnected`
- callers only see `bool false`

This is the key reason downstream code can know "response was not sent" while Watson never reports a disconnect.

### 4. HTTP/1.1 read-side disconnects are silently dropped

The TCP client loop is in:

- `src/WatsonWebserver/Webserver.cs:735-889`

Inside the request-processing loop, Watson catches read-side `IOException` here:

- `src/WatsonWebserver/Webserver.cs:848-850`

Behavior:

- `catch (IOException) { break; }`

No additional action is taken:

- no `ctx.RequestAborted = true`
- no `RequestAborted` event
- no `RequestorDisconnected` event
- no exception telemetry beyond the loop terminating

Impact:

- abrupt client disconnects during the HTTP/1.1 connection loop are treated as silent loop exit
- applications cannot reliably distinguish idle connection closure from abnormal disconnect

### 5. `ResponseSent` is emitted even when the response was not sent

At the end of request processing:

- `src/WatsonWebserver/Core/WebserverBase.cs:864-873`

Watson computes:

- `emitResponseStarting = ctx.Response.ResponseStarted && Events.HasResponseStartingHandlers`
- `emitResponseSent = Events.HasResponseSentHandlers`
- `emitResponseCompleted = ctx.Response.ResponseCompleted && Events.HasResponseCompletedHandlers`

Then it fires:

- `ResponseStarting` if `emitResponseStarting`
- `ResponseSent` if `emitResponseSent`
- `ResponseCompleted` if `emitResponseCompleted`

Problem:

- `emitResponseSent` does **not** check `ctx.Response.ResponseSent`
- if a handler is attached, `ResponseSent` fires unconditionally

This is inconsistent with the name of the event.

`ResponseEventArgs` also does not expose a `ResponseSent` boolean:

- `src/WatsonWebserver/Core/ResponseEventArgs.cs:80-126`

It only exposes:

- `ResponseStarted`
- `ResponseCompleted`

Impact:

- downstream telemetry may interpret `ResponseSent` as success even when send failed
- event consumers must infer failure indirectly from `ResponseCompleted == false`
- the event name is misleading in failure scenarios

## How This Explains the Jurat Failure

Observed downstream telemetry:

- attempted response status: `200`
- response started: `true`
- response completed: `false`
- content length: very large payload
- Watson fallback HTML `500` page eventually returned
- no positive disconnect signal

That sequence is consistent with the Watson HTTP/1.1 code:

1. app builds a large normal response
2. Watson starts writing it
3. write or flush throws
4. `HttpResponse.SendPayloadAsync(...)` catches the exception and returns `false`
5. downstream wrapper converts `false` into its own transport exception
6. Watson never sets `RequestAborted` because no `OperationCanceledException` occurred
7. Watson never raises `RequestorDisconnected` because that event is not implemented
8. Watson fallback logic later emits the default HTML `500`

So the downstream app is correct to conclude "the response transport failed", but Watson is not giving sufficient first-party disconnect classification for that case.

## Recommended Fixes

### Fix 1: Implement real disconnect signaling

Add full support for `RequestorDisconnected`:

- add `HandleRequestorDisconnected(...)` to `WebserverEvents`
- add `HasRequestorDisconnectedHandlers`
- raise it from concrete I/O disconnect paths

At minimum, fire it on:

- HTTP/1.1 read-side `IOException` in `Webserver.cs:848-850`
- HTTP/1.1 write/flush failures in `HttpResponse.cs`
- any equivalent HTTP/2 or HTTP/3 transport close/reset path

### Fix 2: Stop treating only `OperationCanceledException` as an abort

Broaden abort classification so transport failures can mark:

- `ctx.RequestAborted = true`
- `RequestAborted`

This should include at least:

- `IOException` associated with connection reset / broken pipe / closed stream
- `SocketException`
- `ObjectDisposedException` on transport objects during active request processing

### Fix 3: Do not swallow HTTP/1.1 send exceptions as bare `false`

Current behavior in `HttpResponse.cs` loses the real transport error.

Preferred options:

1. let the original exception propagate
2. or wrap and propagate a Watson-specific response transport exception
3. or capture a structured failure reason on the response/context before returning `false`

If `bool` return must remain, Watson still needs to:

- preserve failure classification somewhere structured
- mark the context aborted/disconnected when appropriate

### Fix 4: Handle HTTP/1.1 read-side `IOException` explicitly

Replace silent `break` in:

- `Webserver.cs:848-850`

with explicit classification and event emission.

At minimum:

- mark the active request/context as aborted if one exists
- raise disconnect/abort event
- optionally emit exception telemetry when useful

### Fix 5: Correct `ResponseSent` semantics

In `WebserverBase.cs:865-873`, change `emitResponseSent` to require:

- `ctx.Response.ResponseSent`
- and handler presence

Suggested shape:

- `bool emitResponseSent = ctx.Response.ResponseSent && Events.HasResponseSentHandlers;`

Also consider adding `ResponseSent` to `ResponseEventArgs` for symmetry and easier downstream interpretation.

## Recommended Tests

Add regression tests for:

1. HTTP/1.1 client disconnect during large response body send
   - expected:
     - send fails
     - positive disconnect/abort signal emitted
     - `RequestAborted == true`
     - `ResponseSent` not emitted as success

2. HTTP/1.1 client disconnect while reading next keep-alive request
   - expected:
     - disconnect signal emitted
     - not silently dropped

3. HTTP/1.1 write failure via broken stream / forced socket close
   - expected:
     - disconnect classification preserved
     - no silent `false` with no metadata

4. `ResponseSent` event fires only on actual completed send

5. `RequestorDisconnected` subscription actually receives events

## Bottom Line

Watson 7 currently exposes disconnect-related API surface that suggests stronger semantics than the implementation actually provides.

The main defects are:

- unimplemented `RequestorDisconnected`
- overly narrow `RequestAborted`
- swallowed HTTP/1.1 send exceptions
- silent HTTP/1.1 read disconnect handling
- incorrect `ResponseSent` event emission semantics

For downstream applications attempting to suppress noisy operational alerts caused by client/network disconnects, the current implementation is not sufficient.
