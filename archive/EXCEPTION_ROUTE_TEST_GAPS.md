# Exception Route Test Gaps

## Summary

Watson has a top-level request-processing fallback that emits the stock HTML `500` page when request processing completes without a response having been sent.

That behavior is expected and intentional.

The gap is that the custom exception-route path is not tested for failure modes where the exception route itself:

- throws
- returns without sending a response
- attempts to send a response, but the send fails before `ResponseSent` becomes `true`

Those cases currently fall through to the same stock HTML `500` behavior.

## Current Behavior

Relevant source:

- `src/WatsonWebserver/Core/WebserverBase.cs`
- `src/WatsonWebserver/Core/WebserverConstants.cs`

The main request-processing flow catches unhandled exceptions and does this:

1. If `Routes.Exception` is configured, call it.
2. Otherwise, send Watson's default HTML `500`.
3. In `finally`, if `ctx.Response.ResponseSent` is still `false`, send Watson's default HTML `500`.

That means the custom exception route gets only one chance to fully handle the response.

If it does not complete that work successfully, Watson falls back to:

- `WebserverConstants.PageContent500`
- the HTML page containing `There's a problem here, but it's on me, not you.`

## Why This Is a Problem

For API-oriented hosts, a custom exception route is usually expected to be authoritative.

If that exception route fails internally or decides not to send, the framework silently changes response shape from:

- host-defined error output

to:

- Watson's generic HTML `500`

This can create inconsistent wire behavior for clients that expect JSON, XML, or some other structured error format.

## Existing Coverage

Watson does already test the basic route-exception case.

Examples:

- `src/Test.Shared/SharedLegacySmokeTests.cs`
- `src/Test.Shared/LegacyCoverageSuite.cs`

These tests verify that:

- a normal route handler can throw
- the server returns HTTP `500`
- the server stays alive

That is good baseline coverage.

## Missing Coverage

The following cases do not appear to be covered in `Test.Shared`:

### 1. Custom exception route returns without sending

Scenario:

- a route throws
- Watson enters the top-level catch
- `Routes.Exception` runs
- the delegate returns
- `ctx.Response.ResponseSent` is still `false`

Expected current outcome:

- Watson emits its stock HTML `500` in `finally`

This behavior should be explicitly tested.

### 2. Custom exception route throws

Scenario:

- a route throws
- Watson invokes `Routes.Exception`
- `Routes.Exception` itself throws

Expected current outcome:

- processing escapes the custom exception delegate
- `finally` emits Watson's stock HTML `500` if no response was sent

This is a critical path and should be tested directly.

### 3. Custom exception route attempts a send, but send fails

Scenario:

- a route throws
- `Routes.Exception` sets status/content-type
- `ctx.Response.Send(...)` throws before `ResponseSent` is set

Expected current outcome:

- Watson reaches `finally`
- Watson emits its stock HTML `500` if the response still appears unsent

This should be tested because it is different from a simple exception-route throw.

### 4. Pre-routing/authentication exceptions with a configured exception route

Watson's top-level catch also covers:

- `Routes.PreRouting`
- `Routes.AuthenticateApiRequest`
- `Routes.AuthenticateRequest`
- pre-auth and post-auth route groups

There does not appear to be coverage for exception-route behavior when the original exception comes from:

- `PreRouting`
- authentication delegates

Those cases should be tested too.

## Recommended Framework Fixes

### Option 1: Harden `Routes.Exception` invocation

Wrap the call to `Routes.Exception(ctx, e)` in its own `try/catch`.

If the custom exception route throws:

- raise `ExceptionEncountered` for the secondary failure
- fall back deterministically to Watson's built-in `500` response path

This would make the behavior explicit instead of relying on the `finally` block.

### Option 2: Treat "returned without sending" as a first-class outcome

After `Routes.Exception` returns, if `ctx.Response.ResponseSent` is still `false`, handle that explicitly in the catch block instead of letting `finally` discover it later.

Benefits:

- clearer control flow
- clearer observability
- easier testing
- easier future customization

### Option 3: Add a content-negotiated or configurable fallback

Today the framework fallback is always HTML.

That is reasonable for browser-oriented defaults, but API hosts may want a different fallback shape.

Consider adding a setting or callback for default exception fallback content, such as:

- HTML
- plain text
- JSON

## Recommended Tests

Add explicit tests for all of the following:

1. Route throws, custom exception route sends successfully.
2. Route throws, custom exception route returns without sending.
3. Route throws, custom exception route throws.
4. Route throws, custom exception route send fails before `ResponseSent`.
5. `PreRouting` throws with custom exception route configured.
6. `AuthenticateRequest` throws with custom exception route configured.
7. `AuthenticateApiRequest` throws with custom exception route configured.

Each test should verify:

- status code
- content type
- response body shape
- whether Watson's stock HTML `500` page appears
- whether the server remains healthy for the next request

## Notes

`Routes.PostRouting` is not the same problem area.

In the current implementation, `PostRouting` executes after the main response path and its exceptions are swallowed locally. That path should be tested for observability, but it is not the same source of stock HTML `500` responses.
