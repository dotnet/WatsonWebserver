# OpenAPI 3.1 and 3.2 Support — Implementation Plan

**Target release:** `v7.2.0` (delivered)
**Owner:** Claude (implementation), Joel Christner (review)
**Status:** Complete — all phases delivered; full suite green (699/699); Release build clean; shipped in 7.2.0.
**Last updated:** 2026-09-15

> **Spec verification (task V3-0) — completed.** The 3.2 constructs were checked against the ratified
> OpenAPI 3.2.0 specification (spec.openapis.org) and the OpenAPI Initiative release notes. One correction
> was applied versus the original plan: **`QUERY` is a first-class `query` path-item field in 3.2, not an
> `additionalOperations` entry** — the implementation emits `QUERY` as `query` and routes every other
> non-standard method through `additionalOperations`. Confirmed present in 3.2 and implemented as written:
> `$self`, `additionalOperations`, server `name`, media `itemSchema`, tag `summary`/`parent`/`kind`, and the
> OAuth2 device authorization flow with `oauth2MetadataUrl`.

Watson's OpenAPI generator emits a single hardcoded document version today. This plan takes it to three:
the existing 3.0 output stays the default so nothing already published shifts, and 3.1 and 3.2 become
opt-in targets selected through settings. Along the way we fix the correctness gaps that exist in the
current generator, and we give the host explicit control over whether `/openapi.json` and `/swagger` sit
in front of or behind authentication.

The work is deliberately phased. Phase 0 cleans up what is already there so the version branching is built
on solid ground rather than layered on top of latent bugs. Everything after that is additive.

---

## How to use this document

Every task is a checkbox with a stable identifier (for example `L1-3`). A developer picks up a task,
flips `- [ ]` to `- [x]`, and appends a short completion note in the form `— done <initials> <YYYY-MM-DD>`
when it helps the next person. Do not delete tasks that turn out to be unnecessary; strike them with
`~~L1-3~~ (skipped: reason)` so the audit trail survives.

Status legend for the phase headers:

- **Not started** — no task in the phase is checked.
- **In progress** — at least one task checked, at least one open.
- **Complete** — every task in the phase checked or explicitly skipped.

Keep the phase-header status line and the top-of-file **Status** field in sync as you go. The file-change
index at the bottom is the fastest way to see whether a given source file has been touched yet.

---

## Why this work

The generator was written against OpenAPI 3.0 and hardcodes `"openapi": "3.0.3"` at
`src/WatsonWebserver/Core/OpenApi/OpenApiDocumentGenerator.cs:62`. The document is assembled by hand into
`Dictionary<string, object>` trees rather than a typed object model, which is actually convenient here:
there is exactly one document root, one `BuildSchema` method, and one `BuildComponents` method, so adding a
second and third target version is contained rather than a rewrite.

Two facts shape the whole plan. First, 3.1 is not a cosmetic version bump — it realigns the Schema Object
with JSON Schema 2020-12, which removes `nullable`, allows `type` to be an array, and changes how binary
payloads and examples are expressed. Those are the changes that actually touch code. Second, 3.2 (ratified
in 2025) is newer than most tooling, so its constructs need to be checked against the ratified specification
before implementation rather than trusted from memory. A verification gate is built into Phase 3 for exactly
that reason.

There is also unfinished business in the current implementation that this plan folds in rather than ignores:
OAuth2 and OpenID Connect security schemes are declared as valid types but silently dropped when the document
is built, the two `UseOpenApi` overloads duplicate their registration logic, dynamic-route regex-to-path
conversion is lossy, and the document endpoints can only be registered ahead of authentication. Those are
Phase 0.

---

## Scope

### Goals

- Emit OpenAPI **3.0**, **3.1**, and **3.2** documents, selected by a single setting, with 3.0 remaining the
  default so existing consumers see byte-compatible output unless they opt in.
- Represent the 3.1 Schema Object correctly against JSON Schema 2020-12 (`nullable` removal, type arrays,
  `contentMediaType` for binary, `examples`).
- Add the 3.1/3.2 document-level surface that Watson can meaningfully populate (`info.summary`,
  `license.identifier`, `webhooks`, optional `paths`, `mutualTLS`, OAuth2 flows, `$self`, tag hierarchy,
  server `name`, `QUERY`/`additionalOperations`, streaming `itemSchema`).
- Let the host register `/openapi.json` and `/swagger` with or without authentication, and dedupe the
  registration path while doing it.
- Fix the correctness defects listed in Phase 0.
- Cover all of the above with shared Touchstone cases — positive and negative — plus the first HTTP-level
  endpoint tests for this feature, all wired into `WatsonTestSuites.All`.
- Update README, add a dedicated `OPENAPI.md` feature guide, update `WEBSERVER_SETTINGS.md`, extend the
  `Test.OpenApi` sample, and write the CHANGELOG entry and version bump.

### Non-goals

- Swagger 2.0 / OpenAPI 2.0 output. The document shape diverges too far for the payoff; explicitly out of scope.
- Replacing the hand-built dictionary generator with a typed third-party model (for example `Microsoft.OpenApi`).
  The dictionary approach stays; it is the reason this is tractable.
- Runtime request/response **validation** against the emitted schema. This plan documents the API; it does not
  enforce it at the wire.
- Bundling Swagger UI assets for offline use. The CDN pin is called out as a known limitation but not solved here.

---

## Version strategy and compatibility

Version selection becomes a first-class setting instead of a constant. A new enum `OpenApiVersionEnum` carries
the three supported major/minor targets, and each maps to a canonical version string. To honor the repository
rule against hardcoding values a developer may later want to change, the emitted patch string is also overridable
through an explicit property, with the enum supplying the default.

| Enum value | Default emitted `openapi` | Notes |
|---|---|---|
| `V3_0` (**default**) | `3.0.3` | Byte-compatible with today's output. Do not change the default. |
| `V3_1` | `3.1.1` | JSON Schema 2020-12 Schema Object. |
| `V3_2` | `3.2.0` | Verify constructs against the ratified 3.2.0 spec first. |

Keeping `V3_0` as the default is the compatibility contract. A consumer who never touches the new setting must
get exactly the document they get today, which is why Phase 5 includes a regression case that locks the 3.0
output (including `"nullable": true` still being emitted).

---

## OpenAPI 3.1 delta, mapped to this codebase

3.1 aligns the Schema Object with JSON Schema 2020-12. The table below is the authoritative list of what
changes and where it lives. Everything in Phase 2 traces back to a row here.

| 3.0 → 3.1 change | Where it lives today | Required behavior under 3.1 |
|---|---|---|
| `nullable` removed | `OpenApiSchemaMetadata.Nullable`; emitted at `OpenApiDocumentGenerator.cs:592` | Fold `null` into the type. `type: ["string","null"]` when a single `Type` is set; `{"anyOf":[{"$ref":...},{"type":"null"}]}` when nullable sits on a `$ref`. |
| `type` may be an array | `OpenApiSchemaMetadata.Type` is a single `string` | Support a multi-type schema. Add `Types` (list) used when present; keep `Type` for the common single-type case. |
| `example` (schema) deprecated for `examples` | emitted at `OpenApiDocumentGenerator.cs:611` | Emit `examples: [value]` in the Schema Object; keep `example` under 3.0. Media-type-level `example`/`examples` are unchanged and valid in both. |
| binary via `format: binary`/`byte` replaced | `BuildContentRouteOperation` at `OpenApiDocumentGenerator.cs:445` | Emit `type: string` with `contentMediaType` (and `contentEncoding` for base64), no `format: binary`. |
| `exclusiveMinimum`/`exclusiveMaximum` become numbers | not currently emitted | When added, emit numeric form under 3.1, boolean+sibling under 3.0. |
| `info.summary` added | `OpenApiInfo` | New `Summary` property, emitted in `BuildInfo`. |
| `license.identifier` (SPDX) added | `OpenApiLicense` (`Name`, `Url` only) | New `Identifier`. Mutually exclusive with `Url` — validate. |
| `webhooks` top-level object added | absent | New `OpenApiSettings.Webhooks`; emitted at document root. 3.1+ only. |
| `paths` now optional | assumed present at `OpenApiDocumentGenerator.cs:64` | Allow a document with `webhooks`/`components` and no `paths`. |
| `jsonSchemaDialect` top-level added | absent | Optional `OpenApiSettings.JsonSchemaDialect`, emitted when set. |
| Reference Object gains `summary`/`description` | `$ref` short-circuit at `OpenApiDocumentGenerator.cs:577` | Allow sibling `summary`/`description` next to `$ref`. |
| `mutualTLS` security type added | `BuildComponents` handles only `apiKey`/`http` | Emit `mutualTLS`. Also fixes the OAuth2/OIDC gap (Phase 0). |

The single biggest item is `nullable`. It is the one change most likely to break a naive port, because a schema
that is valid 3.0 becomes invalid 3.1 if `nullable` is emitted verbatim.

---

## OpenAPI 3.2 delta, mapped to this codebase

3.2 is additive on top of 3.1 but young. **Before writing any 3.2 code, complete task `V3-0` (spec verification).**
The constructs below are the intended surface; confirm each against the ratified 3.2.0 document, because tooling
and secondary sources lagged the release.

| 3.2 construct | Intended Watson surface | Verify |
|---|---|---|
| `openapi: 3.2.0` | `OpenApiVersionEnum.V3_2` | trivial |
| `$self` document identity | `OpenApiSettings.Self` | confirm keyword name/semantics |
| `QUERY` HTTP method | route method mapping + operation emission | confirm path-item slot |
| `additionalOperations` (arbitrary methods) | per-route emission for non-standard methods | confirm object shape |
| Sequential/streaming media `itemSchema` | `OpenApiMediaTypeMetadata.ItemSchema` | confirm for `application/jsonl`, `text/event-stream` |
| Tag hierarchy (`parent`, `kind`, `summary`) | `OpenApiTag` additions | confirm field names |
| Server `name` | `OpenApiServer.Name` | confirm |
| OAuth2 device authorization flow, `oauth2MetadataUrl` | `OpenApiSecurityScheme` flows | confirm flow key names |

Any 3.2-only construct requested while the target version is lower must be a validation error, not silent
emission of an invalid document (see negative tests in Phase 5).

---

## Phase 0 — Cleanup and correctness pass

**Status: Not started**

The existing generator has defects that would compound once versioning is layered on. Fix them first, keeping
each fix independently testable. All new and changed public members in this phase follow the code-style rules:
XML docs on every public member, backing fields with guard clauses where a value needs null/range validation,
`_PascalCase` private fields, one type per file, usings inside the namespace and alphabetized.

- [ ] **C0-1** Dedupe the two `UseOpenApi` overloads in `WebserverExtensions.cs` into one shared private
  registration method. Both currently repeat the route-registration block (`WebserverExtensions.cs:26-38` and
  `:58-70`).
- [ ] **C0-2** Emit OAuth2 and OpenID Connect security schemes in `BuildComponents`
  (`OpenApiDocumentGenerator.cs:186-234`). Today only `apiKey` and `http` are handled, so any `oauth2` /
  `openIdConnect` scheme is declared valid but dropped. Requires `OpenApiSecurityScheme` to gain `Flows`
  (OAuth2) and `OpenIdConnectUrl`.
- [ ] **C0-3** Replace the string-concatenation error branch in `OpenApiRouteHandler.cs:43-48` with serializer-built
  JSON so control characters cannot corrupt the response body.
- [ ] **C0-4** Make the generator stateless and thread-safe: move per-request state (target version, settings)
  into a small internal builder created inside `Generate`, so the shared `OpenApiDocumentGenerator` instance
  carries no request state. Document the thread-safety guarantee in XML docs.
- [ ] **C0-5** Consolidate the duplicated server and external-docs types. `OpenApiServer` (url/description) and
  `OpenApiServerMetadata` (adds `Variables`) diverge, as do `OpenApiExternalDocs` and
  `OpenApiExternalDocsMetadata`. Unify so `OpenApiSettings.Servers` gains variable support and the route-level
  and settings-level types share one representation. One type per file.
- [ ] **C0-6** Content routes: `BuildContentRouteOperation` claims GET+HEAD in comments but emits only GET, and
  hardcodes `application/octet-stream`. Emit HEAD alongside GET and make the media type derivable.
- [ ] **C0-7** Guard `ConvertRegexToPath` (`OpenApiDocumentGenerator.cs:689`). Lossy regex conversion can emit
  invalid or colliding path templates. When conversion is not confident, skip the route and record it through
  the existing events surface rather than emitting a broken path. No `Console.WriteLine`.
- [ ] **C0-8** Add guard clauses and leading-slash validation for `DocumentPath` and `SwaggerUiPath` in the shared
  registration method (throw `ArgumentException` with a contextual message on empty/whitespace).
- [ ] **C0-9** Make the pinned Swagger UI dependency version (`swagger-ui-dist@5.11.0`, `SwaggerUiHandler.cs:49,87,88`)
  a configurable public member with a sensible default instead of an inline constant. Document the CDN/offline
  limitation in XML docs and in `OPENAPI.md`.
- [ ] **C0-10** Handle the `nullable` + `$ref` case under 3.0 correctly (wrap in `allOf`) or, at minimum, document
  the current short-circuit behavior at `OpenApiDocumentGenerator.cs:577` so the 3.1 fix (`P2` anyOf form) has a
  known baseline.

---

## Phase 1 — Version infrastructure

**Status: Not started**

This phase introduces the version selector and the plumbing that later phases hang behavior off. No emitted
output changes yet for the default path.

- [ ] **L1-1** Add `OpenApiVersionEnum` (`V3_0`, `V3_1`, `V3_2`) — one enum, one file, XML-documented values.
- [ ] **L1-2** Add `OpenApiSettings.Version` (`OpenApiVersionEnum`, default `V3_0`) and an optional
  `VersionString` override with backing field and validation. Document default/allowed values in XML per the
  style rule on documenting defaults, minimums, and maximums.
- [ ] **L1-3** Thread the target version through the internal builder from `C0-4` so every `Build*` method can
  branch on it without static state.
- [ ] **L1-4** Emit the selected version string at the document root in place of the `"3.0.3"` constant
  (`OpenApiDocumentGenerator.cs:62`).
- [ ] **L1-5** Add a private `ValidateForVersion` step that rejects constructs unsupported by the selected
  version (feeds the negative tests). Throw `ArgumentException`/a domain exception with a message naming the
  offending construct and the active version.

---

## Phase 2 — OpenAPI 3.1 support

**Status: Not started**

Implement every row from the 3.1 delta table. Each task is a discrete, testable behavior.

- [ ] **P2-1** Schema `nullable`: under 3.1 emit type arrays (`["string","null"]`) and the `anyOf` form for
  nullable `$ref`; under 3.0 keep `"nullable": true`.
- [ ] **P2-2** Add `OpenApiSchemaMetadata.Types` (multi-type list) with backing field; emit as a `type` array
  under 3.1/3.2. Define precedence with the single `Type` and document it.
- [ ] **P2-3** Schema examples: emit `examples` array under 3.1/3.2, `example` under 3.0.
- [ ] **P2-4** Binary content: `contentMediaType`/`contentEncoding` under 3.1/3.2, `format: binary` under 3.0
  (`BuildContentRouteOperation` and any schema carrying a binary/byte format).
- [ ] **P2-5** `info.summary` (`OpenApiInfo.Summary`) and `license.identifier` (`OpenApiLicense.Identifier`,
  mutually exclusive with `Url`, validated).
- [ ] **P2-6** `webhooks`: `OpenApiSettings.Webhooks` emitted at the root; 3.1+ only.
- [ ] **P2-7** Optional `paths`: allow a valid document with no paths when webhooks/components are present.
- [ ] **P2-8** `jsonSchemaDialect`: `OpenApiSettings.JsonSchemaDialect`, emitted when set.
- [ ] **P2-9** Reference Object `summary`/`description` siblings alongside `$ref`.
- [ ] **P2-10** `mutualTLS` security scheme emission (builds on `C0-2`).
- [ ] **P2-11** `exclusiveMinimum`/`exclusiveMaximum` numeric form under 3.1 (add the properties if not present
  from `C0`), boolean form under 3.0.

---

## Phase 3 — OpenAPI 3.2 support

**Status: Not started**

- [ ] **V3-0** **Spec-verification gate.** Read the ratified OpenAPI 3.2.0 specification and confirm each construct
  in the 3.2 delta table (keyword names, object shapes, path-item slots). Record confirmed/adjusted findings inline
  in this document before writing code. No 3.2 task below starts until this is checked.
- [ ] **V3-1** Emit `openapi: 3.2.0` for `V3_2`.
- [ ] **V3-2** `$self` document identity (`OpenApiSettings.Self`).
- [ ] **V3-3** `QUERY` method mapping and operation emission.
- [ ] **V3-4** `additionalOperations` for non-standard methods.
- [ ] **V3-5** Streaming/sequential media `itemSchema` (`OpenApiMediaTypeMetadata.ItemSchema`).
- [ ] **V3-6** Tag hierarchy: `OpenApiTag.Parent`, `Kind`, `Summary`.
- [ ] **V3-7** Server `name` (`OpenApiServer.Name`).
- [ ] **V3-8** OAuth2 device authorization flow and `oauth2MetadataUrl`.
- [ ] **V3-9** Ensure every 3.2-only construct throws through `ValidateForVersion` when the target is `V3_0`/`V3_1`.

---

## Phase 4 — Auto-registration and authentication toggle

**Status: Not started**

Today `UseOpenApi` always registers both endpoints on `PreAuthentication.Static`, so the document is always
public. The health-check feature already models the pattern we want: `UseHealthCheck` registers with
`server.Get(path, handler, auth: settings.RequireAuthentication)` (`WebserverHealthExtensions.cs:50`). Mirror it.

- [ ] **A4-1** Add `OpenApiSettings.RequireAuthentication` (bool, default `false` to preserve current public
  behavior). Document that `false` registers ahead of authentication and `true` registers behind it.
- [ ] **A4-2** In the shared registration method (`C0-1`), register `/openapi.json` and `/swagger` on
  `PostAuthentication.Static` when `RequireAuthentication` is true, otherwise `PreAuthentication.Static`.
- [ ] **A4-3** Ensure the authenticated path does not leak the document: with `RequireAuthentication = true` and
  no/invalid credentials, the endpoint must return the server's standard auth-failure status, not 200.
- [ ] **A4-4** Keep `EnableOpenApi` / `EnableSwaggerUi` as the on/off gates; `RequireAuthentication` only chooses
  the group. Confirm interaction with `IncludePreAuthRoutes` / `IncludePostAuthRoutes` document-collection flags
  is coherent and documented (collection flags govern which routes appear *in* the document; the new flag governs
  where the doc endpoints themselves live).

---

## Phase 5 — Tests (positive and negative)

**Status: Not started**

All cases live in `src/Test.Shared/` and register through `WatsonTestSuites.All` so Test.Automated, Test.XUnit,
and Test.Nunit run them unchanged. The existing OpenAPI cases in `SharedOpenApiCompositionTests.cs` are pure
document-generation unit tests that never start a server; extend that file for generation assertions, and add a
new HTTP-level suite for the endpoint/auth behavior that currently has no coverage at all. HTTP-level tests bind
to `127.0.0.1` (not `localhost`) per the repository loopback rule.

### 5a — Positive generation cases (extend `SharedOpenApiCompositionTests.cs`)

- [ ] **T5-1** `V3_0` default regression: `openapi == "3.0.3"`, and a nullable field still emits `"nullable": true`.
  This is the compatibility lock.
- [ ] **T5-2** `V3_1`: `openapi == "3.1.1"`; nullable field emits `type: ["string","null"]` and no `nullable` key.
- [ ] **T5-3** `V3_1`: nullable `$ref` emits the `anyOf` + `{"type":"null"}` form.
- [ ] **T5-4** `V3_1`: schema example emits an `examples` array.
- [ ] **T5-5** `V3_1`: binary content emits `contentMediaType`, no `format: binary`.
- [ ] **T5-6** `V3_1`: `info.summary` and `license.identifier` present; `license.url` absent when identifier is set.
- [ ] **T5-7** `V3_1`: `webhooks` emitted; a document with webhooks and no paths is still generated and valid.
- [ ] **T5-8** `V3_1`: `oauth2` flows and `openIdConnect` scheme emitted (locks the `C0-2` fix); `mutualTLS` emitted.
- [ ] **T5-9** `V3_2`: `openapi == "3.2.0"`; assert the confirmed 3.2 constructs from `V3-0` (`$self`, `QUERY`,
  `additionalOperations`, `itemSchema`, tag `parent`/`kind`/`summary`, server `name`).

### 5b — Negative / validation cases (extend `SharedOpenApiCompositionTests.cs`)

- [ ] **T5-10** License with both `Url` and `Identifier` under 3.1 → validation error.
- [ ] **T5-11** `webhooks` requested under `V3_0` → validation error.
- [ ] **T5-12** A 3.2-only construct (for example `QUERY` op or `additionalOperations`) requested under `V3_0`/`V3_1`
  → validation error naming the construct and version.
- [ ] **T5-13** Multi-type/type-array requested with neither `Type` nor `Types` populated → validation error.
- [ ] **T5-14** `oauth2` scheme with null `Flows` → validation error.
- [ ] **T5-15** Duplicate `operationId` across operations → validation error.
- [ ] **T5-16** Schema with both `Ref` and `Type` set → defined precedence (`Ref` wins) asserted, or strict-mode
  error; document whichever is chosen.
- [ ] **T5-17** Empty/whitespace `DocumentPath` or `SwaggerUiPath` passed to `UseOpenApi` → `ArgumentException`.

### 5c — HTTP-level endpoint & auth cases (new suite, e.g. `SharedOpenApiEndpointTests.cs`)

- [ ] **T5-18** New suite file added and registered in `WatsonTestSuites.cs` alongside the existing
  `NamedSuite("OpenApi", ...)` entry (`WatsonTestSuites.cs:60-63`).
- [ ] **T5-19** `RequireAuthentication = false`: GET `/openapi.json` returns 200 and valid JSON with no credentials.
- [ ] **T5-20** `RequireAuthentication = false`: GET `/swagger` returns 200 `text/html`.
- [ ] **T5-21** `RequireAuthentication = true`: GET `/openapi.json` without credentials returns the auth-failure
  status and does **not** return the document body; with valid credentials returns 200.
- [ ] **T5-22** Selected version round-trips over HTTP: server configured `V3_1` serves a document whose top-level
  `openapi` is `3.1.1`.

---

## Phase 6 — Documentation

**Status: Not started**

Documentation is part of the deliverable, not an afterthought. Prose sections in the human-facing docs follow the
writing requirements: real explanatory paragraphs around any list, specific owned language, no template-shaped
sections. Code XML docs follow the code-style rules.

- [ ] **D6-1** New root `OPENAPI.md`, modeled on `TELEMETRY.md` (prose intro, version matrix, consumption guide,
  a 3.0→3.1 migration note for consumers, the auth-registration toggle, and a "verify it yourself" section).
- [ ] **D6-2** README: update the `## OpenAPI / Swagger` section (`README.md:943-1053`) with version selection and
  the auth toggle; fix the `OpenAPI 3.0` claim at `README.md:28` to describe 3.0/3.1/3.2 with 3.0 as default.
- [ ] **D6-3** `WEBSERVER_SETTINGS.md`: document every new `OpenApiSettings` member (`Version`, `VersionString`,
  `RequireAuthentication`, `Webhooks`, `JsonSchemaDialect`, `Self`, Swagger UI version, unified server/external-docs).
- [ ] **D6-4** Extend the `Test.OpenApi` sample (`src/Test.OpenApi/Program.cs`) to demonstrate emitting 3.1 and 3.2,
  and an auth-protected documentation endpoint.
- [ ] **D6-5** Confirm XML documentation exists on every new/changed public member (compiler warnings clean).

---

## Phase 7 — Changelog and version bump

**Status: Not started**

- [ ] **R7-1** Bump `<Version>` `7.1.1` → `7.2.0` in `src/WatsonWebserver/WatsonWebserver.csproj:6`, and any
  companion package versions that move in lockstep.
- [ ] **R7-2** Update the `## Current Version` block in `CHANGELOG.md` to `` `v7.2.0` `` and add a `## v7.2.0`
  section (newest first, past-tense bullets matching the existing style). Draft bullets:
  - Added opt-in OpenAPI 3.1 and 3.2 document generation selected via `OpenApiSettings.Version`; 3.0 remains the default so existing documents are unchanged
  - Aligned the 3.1 Schema Object with JSON Schema 2020-12 (removed `nullable` in favor of type arrays, `contentMediaType` for binary payloads, schema-level `examples`)
  - Added 3.1 document surface: `info.summary`, `license.identifier`, `webhooks`, optional `paths`, `jsonSchemaDialect`, reference `summary`/`description`, and the `mutualTLS` security scheme
  - Added 3.2 document surface (`$self`, `QUERY`/`additionalOperations`, streaming `itemSchema`, hierarchical tags, server `name`, OAuth2 device authorization flow)
  - Added `OpenApiSettings.RequireAuthentication` to register `/openapi.json` and `/swagger` behind authentication; unchanged default keeps them public
  - Fixed OAuth2/OpenID Connect security schemes being silently dropped from `components.securitySchemes`
  - Deduplicated the `UseOpenApi` registration path, hardened OpenAPI error responses, and made the Swagger UI asset version configurable
  - Added positive, negative, and first HTTP-level OpenAPI test coverage in `Test.Shared`, wired into all three runners
- [ ] **R7-3** Move any relevant lines out of the CHANGELOG `## Unreleased` section if this ships before them.

---

## Code-style compliance checklist

Applies to every source file created or changed in Phases 0–4. Verify before marking a phase complete.

- [ ] **S-1** Namespace declared first; `using` statements inside the namespace block, Microsoft/System usings
  alphabetized first, then others alphabetized.
- [ ] **S-2** XML documentation on every public member, constructor, and method; none on private members/methods.
- [ ] **S-3** Public members needing null/range validation use explicit get/set over `_PascalCase` backing fields
  with guard clauses; private fields are `_PascalCase`.
- [ ] **S-4** No `var`; no tuples; one class or one enum per file.
- [ ] **S-5** Async methods take a `CancellationToken` (unless the type holds one), use `.ConfigureAwait(false)`,
  and check cancellation at sensible points.
- [ ] **S-6** Specific exception types with contextual messages; `/// <exception>` tags on public methods that throw;
  guard clauses at method entry.
- [ ] **S-7** No `Console.WriteLine` in library code; nullable reference types honored; `.Any()` over `.Count() > 0`.
- [ ] **S-8** XML docs state defaults, minimums, maximums, and value meanings where a member is configurable.
- [ ] **S-9** Build is warning-clean across targets (`dotnet build src\WatsonWebserver.sln -c Debug`).

---

## Definition of done

The feature is complete when a host can select any of the three versions through one setting and receive a valid
document, when the default path is byte-identical to today's 3.0 output, and when the documentation endpoints can
be placed behind authentication. Concretely:

- [ ] **DoD-1** `dotnet build src\WatsonWebserver.sln -c Debug` is clean (no errors, no new warnings).
- [ ] **DoD-2** All three runners pass: `Test.Automated` (exit 0), `Test.XUnit`, `Test.Nunit`.
- [ ] **DoD-3** Every positive and negative case in Phase 5 is present and green, including the `V3_0` regression lock.
- [ ] **DoD-4** Documents emitted for 3.1 and 3.2 validate against an external OpenAPI validator (record the tool
  and result in the PR).
- [ ] **DoD-5** README, `OPENAPI.md`, `WEBSERVER_SETTINGS.md`, the sample, CHANGELOG, and the version bump are all in.
- [ ] **DoD-6** The code-style checklist passes on every touched file.

---

## Risks and open questions

The 3.2 specification is the largest unknown. It is recent enough that field names and object shapes should be
confirmed from the ratified document rather than trusted from memory or from tooling that may have shipped against
a draft; task `V3-0` exists precisely to force that confirmation before code is written. If a construct turns out
to differ from the delta table, adjust the table in place and note the change.

Two smaller questions deserve an explicit decision during Phase 0. The Swagger UI page loads assets from a public
CDN, so a `/swagger` page has no offline story today; `C0-9` makes the pin configurable but does not bundle assets,
and if offline operation matters that becomes follow-on work. The dynamic-route regex-to-path conversion is lossy
by nature, and `C0-7` chooses to skip-and-record rather than emit a questionable path template — confirm that
trade-off is acceptable for hosts that lean on dynamic routes, since the alternative is emitting path templates
that may be wrong.

The `nullable` handling on `$ref` schemas is the change most likely to surprise a consumer mid-migration, because
the 3.0 and 3.1 encodings look nothing alike. The migration note in `OPENAPI.md` (`D6-1`) should show both forms
side by side so a consumer upgrading their client understands why the shape moved.

---

## File-change index

Check a box when the file has been created or modified and its tasks are complete.

**Library — `src/WatsonWebserver/Core/OpenApi/`**

- [ ] `OpenApiVersionEnum.cs` — new (`L1-1`)
- [ ] `OpenApiDocumentGenerator.cs` — version branching, schema/binary/example emission, cleanup (`C0-2..C0-10`, `L1-3..L1-5`, `P2-*`, `V3-*`)
- [ ] `OpenApiSettings.cs` — `Version`, `VersionString`, `RequireAuthentication`, `Webhooks`, `JsonSchemaDialect`, `Self`, Swagger UI version
- [ ] `OpenApiSchemaMetadata.cs` — `Types`, `examples`, exclusive min/max, `contentMediaType`, ref siblings (`P2-1..P2-4`, `P2-9`, `P2-11`)
- [ ] `OpenApiInfo.cs` — `Summary` (`P2-5`)
- [ ] `OpenApiLicense.cs` — `Identifier` (`P2-5`)
- [ ] `OpenApiSecurityScheme.cs` — `Flows`, `OpenIdConnectUrl`, `mutualTLS` support (`C0-2`, `P2-10`, `V3-8`)
- [ ] `OpenApiServer.cs` / `OpenApiServerMetadata.cs` — consolidation, `name`, variables (`C0-5`, `V3-7`)
- [ ] `OpenApiExternalDocs.cs` / `OpenApiExternalDocsMetadata.cs` — consolidation (`C0-5`)
- [ ] `OpenApiTag.cs` — `Parent`, `Kind`, `Summary` (`V3-6`)
- [ ] `OpenApiMediaTypeMetadata.cs` — `ItemSchema` (`V3-5`)
- [ ] `WebserverExtensions.cs` — dedupe + auth toggle registration (`C0-1`, `C0-8`, `A4-1..A4-4`)
- [ ] `OpenApiRouteHandler.cs` — serializer-built error body (`C0-3`)
- [ ] `SwaggerUiHandler.cs` — configurable UI version (`C0-9`)

**Tests — `src/Test.Shared/`**

- [ ] `SharedOpenApiCompositionTests.cs` — positive + negative generation cases (`T5-1..T5-17`)
- [ ] `SharedOpenApiEndpointTests.cs` — new HTTP-level suite (`T5-18..T5-22`)
- [ ] `WatsonTestSuites.cs` — register the new endpoint suite (`T5-18`)

**Sample — `src/Test.OpenApi/`**

- [ ] `Program.cs` — demonstrate 3.1/3.2 and auth-protected docs (`D6-4`)

**Docs & release (root)**

- [ ] `OPENAPI.md` — new feature guide (`D6-1`)
- [ ] `README.md` — OpenAPI section + version claim (`D6-2`)
- [ ] `WEBSERVER_SETTINGS.md` — new settings (`D6-3`)
- [ ] `CHANGELOG.md` — `v7.2.0` entry (`R7-2`)
- [ ] `src/WatsonWebserver/WatsonWebserver.csproj` — version bump (`R7-1`)
