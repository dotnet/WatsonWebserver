# WebserverSettings

`WebserverSettings` is the primary configuration object for Watson 7. It controls host binding, protocol enablement, I/O behavior, TLS, default headers, access control, debug logging, Alt-Svc, and API-route timeouts.

Source of truth:

- `src/WatsonWebserver/Core/WebserverSettings.cs`
- `src/WatsonWebserver/Core/Settings/ProtocolSettings.cs`
- `src/WatsonWebserver/Core/Settings/AltSvcSettings.cs`
- `src/WatsonWebserver/Core/Settings/TimeoutSettings.cs`
- `src/WatsonWebserver/Core/Settings/*`

## Basic Usage

```csharp
using WatsonWebserver;
using WatsonWebserver.Core;

WebserverSettings settings = new WebserverSettings("127.0.0.1", 9000, false);

settings.Protocols.EnableHttp1 = true;
settings.Protocols.EnableHttp2 = false;
settings.Protocols.EnableHttp3 = false;

Webserver server = new Webserver(settings, DefaultRoute);
```

## Top-Level Properties

### `Hostname`

The hostname or bind target Watson should listen on.

Default:

- `"localhost"`

Validation:

- throws if set to `null` or empty

Notes:

- `"*"` and `"+"` force `UseMachineHostname = true`
- the constructor normalizes `"::"` to `"[::]"`

### `Port`

The TCP port to listen on.

Default:

- `8000`

Validation:

- throws if less than `0`

Practical note:

- use normal TCP port ranges; `0` is not rejected by the property, but typical consumer code should use an explicit port

### `Prefix`

Computed property returning:

- `http://[hostname]:[port]/`
- or `https://[hostname]:[port]/` when `Ssl.Enable` is `true`

This is derived from `Hostname`, `Port`, and `Ssl.Enable`.

### `Protocols`

Nested `ProtocolSettings` controlling HTTP/1.1, HTTP/2, and HTTP/3 behavior.

Default:

- non-null `ProtocolSettings` instance

Validation:

- throws if set to `null`

### `AltSvc`

Nested `AltSvcSettings` controlling HTTP/3 advertisement.

Default:

- non-null `AltSvcSettings` instance

Validation:

- throws if set to `null`

### `IO`

Nested `IOSettings` controlling stream buffer sizing, body/header limits, keep-alive, and HTTP/1.1-specific pooling/caching.

Default:

- non-null `IOSettings` instance

Validation:

- throws if set to `null`

### `Ssl`

Nested `SslSettings` controlling TLS enablement and certificate loading.

Default:

- non-null `SslSettings` instance

Validation:

- throws if set to `null`

### `Headers`

Nested `HeaderSettings` controlling default response headers.

Default:

- non-null `HeaderSettings` instance

Validation:

- throws if set to `null`

### `AccessControl`

`AccessControlManager` controlling default permit/deny behavior and allow/deny matchers.

Default:

- `DefaultPermit`

Validation:

- throws if set to `null`

### `Debug`

Nested `DebugSettings` controlling debug logging categories.

Default:

- non-null `DebugSettings` instance

Validation:

- throws if set to `null`

### `Timeout`

Nested `TimeoutSettings` controlling API-route request timeouts.

Default:

- non-null `TimeoutSettings` instance

Validation:

- throws if set to `null`

### `UseMachineHostname`

When `true`, Watson uses the machine hostname instead of the literal `Hostname` value for relevant host metadata behavior.

Default:

- `false`

Special behavior:

- if `Hostname` is `"*"` or `"+"`, this behaves as `true` regardless of the assigned value

## Constructor

### `new WebserverSettings()`

Creates a settings object with the built-in defaults.

### `new WebserverSettings(string hostname, int port, bool ssl = false)`

Common convenience constructor.

Behavior:

- null or empty `hostname` becomes `"localhost"`
- `port < 0` throws
- `"::"` becomes `"[::]"`
- `Ssl.Enable` is set from `ssl`

## ProtocolSettings

File:

- `src/WatsonWebserver/Core/Settings/ProtocolSettings.cs`

### `EnableHttp1`

Default:

- `true`

### `EnableHttp2`

Default:

- `false`

### `EnableHttp3`

Default:

- `false`

### `EnableHttp2Cleartext`

Enables h2c prior-knowledge mode.

Default:

- `false`

Important:

- this is explicit opt-in
- Watson validates unsupported combinations at startup

### `MaxConcurrentStreams`

Maximum concurrent streams per connection.

Default:

- `100`

Validation:

- throws if less than `1`

Backed by:

- `Protocols.Http2.MaxConcurrentStreams`

### `Http2`

Nested `Http2Settings`.

Validation:

- throws if set to `null`

### `Http3`

Nested `Http3Settings`.

Validation:

- throws if set to `null`

### `IdleTimeoutMs`

Idle connection timeout in milliseconds.

Default:

- `120000`

Validation:

- throws if less than `1000`

## Http2Settings

File:

- `src/WatsonWebserver/Core/Http2/Http2Settings.cs`

### `HeaderTableSize`

HPACK dynamic table size.

Default:

- `Http2Constants.DefaultHeaderTableSize`

### `EnablePush`

Default:

- `false`

### `MaxConcurrentStreams`

Default:

- `100`

### `InitialWindowSize`

Default:

- `Http2Constants.DefaultInitialWindowSize`

Validation:

- throws if less than `0`
- throws if greater than `Http2Constants.MaxInitialWindowSize`

### `MaxFrameSize`

Default:

- `Http2Constants.DefaultMaxFrameSize`

Validation:

- throws if outside the HTTP/2 min/max frame size bounds

### `MaxHeaderListSize`

Default:

- `Http2Constants.DefaultMaxHeaderListSize`

## Http3Settings

File:

- `src/WatsonWebserver/Core/Http3/Http3Settings.cs`

### `MaxFieldSectionSize`

Default:

- `0`

Validation:

- throws if less than `0`

### `QpackMaxTableCapacity`

Default:

- `0`

Validation:

- throws if less than `0`

### `QpackBlockedStreams`

Default:

- `0`

Validation:

- throws if less than `0`

### `EnableDatagram`

Whether RFC 9220 datagrams are advertised.

Default:

- `false`

## AltSvcSettings

File:

- `src/WatsonWebserver/Core/Settings/AltSvcSettings.cs`

### `Enabled`

Default:

- `false`

Important:

- only meaningful when HTTP/3 is enabled and usable

### `Authority`

Optional advertised authority override.

Default:

- `null`

### `Port`

Advertised Alt-Svc port.

Default:

- `0`

Meaning:

- `0` means use the primary server port

Validation:

- throws if less than `0`
- throws if greater than `65535`

### `Http3Alpn`

Default:

- `"h3"`

### `MaxAgeSeconds`

Default:

- `86400`

Validation:

- throws if less than `0`

## IOSettings

File:

- `src/WatsonWebserver/Core/Settings/IOSettings.cs`

### `Http1`

Nested `Http1IOSettings`.

Validation:

- throws if set to `null`

### `StreamBufferSize`

Default:

- `65536`

Validation:

- throws if less than `1`

### `MaxRequests`

Maximum concurrent requests.

Default:

- `1024`

Validation:

- throws if less than `1`

### `ReadTimeoutMs`

Inbound socket read timeout in milliseconds.

Default:

- `10000`

Validation:

- throws if less than `1`

### `MaxIncomingHeadersSize`

Maximum incoming header size in bytes.

Default:

- `65536`

Validation:

- throws if less than `1`

### `EnableKeepAlive`

Default:

- `false`

### `MaxRequestBodySize`

Maximum request body size in bytes.

Default:

- `0`

Meaning:

- `0` or less disables this check

Note:

- enforced against declared request body sizes before reading

### `MaxHeaderCount`

Maximum number of request headers.

Default:

- `64`

Meaning:

- `0` or less disables this check

## Http1IOSettings

File:

- `src/WatsonWebserver/Core/Settings/Http1IOSettings.cs`

These settings exist to control HTTP/1.1-specific retention and cache behavior without cluttering `Settings.IO`.

### `PoolMaxRetainedPerType`

Maximum retained pooled objects per HTTP/1.1 pooled type.

Default:

- `256`

Clamp:

- minimum `0`
- maximum `4096`

Meaning:

- `0` disables HTTP/1.1 pooled retention

### `ResponseHeaderTemplateCacheSize`

Maximum cached HTTP/1.1 response-header template entries.

Default:

- `256`

Clamp:

- minimum `0`
- maximum `2048`

Meaning:

- `0` disables the cache

### `StatusLineCacheSize`

Maximum cached HTTP/1.1 status-line entries.

Default:

- `64`

Clamp:

- minimum `0`
- maximum `256`

Meaning:

- `0` disables the cache

## SslSettings

File:

- `src/WatsonWebserver/Core/Settings/SslSettings.cs`

### `Enable`

Default:

- `false`

### `SslCertificate`

Directly assigned `X509Certificate2`.

Behavior:

- if not explicitly set, Watson attempts to load from `PfxCertificateFile`
- loading occurs lazily when the property is accessed

### `PfxCertificateFile`

Default:

- `null`

### `PfxCertificatePassword`

Default:

- `null`

### `MutuallyAuthenticate`

Default:

- `false`

### `AcceptInvalidAcertificates`

Default:

- `true`

Note:

- the property name is currently spelled `AcceptInvalidAcertificates` in code

## HeaderSettings

File:

- `src/WatsonWebserver/Core/Settings/HeaderSettings.cs`

### `IncludeContentLength`

Default:

- `true`

### `DefaultHeaders`

Headers added to responses unless already set.

Default keys:

- `Access-Control-Allow-Origin: *`
- `Access-Control-Allow-Methods: OPTIONS, HEAD, GET, PUT, POST, DELETE, PATCH`
- `Access-Control-Allow-Headers: *`
- `Access-Control-Expose-Headers:`
- `Accept: */*`
- `Accept-Language: en-US, en`
- `Accept-Charset: ISO-8859-1, utf-8`
- `Cache-Control: no-cache`
- `Connection: close`
- `Host: localhost:8000`

Setter behavior:

- assigning `null` restores the built-in defaults

## DebugSettings

File:

- `src/WatsonWebserver/Core/Settings/DebugSettings.cs`

All debug flags default to `false`.

### `AccessControl`

Logs access-control behavior.

### `Routing`

Logs routing behavior.

### `Requests`

Logs request handling.

### `Responses`

Logs response handling.

Important:

- set `server.Events.Logger` if you want to receive debug output

## AccessControlManager

Files:

- `src/WatsonWebserver/Core/AccessControlManager.cs`
- `src/WatsonWebserver/Core/AccessControlMode.cs`

### `Mode`

Default:

- `AccessControlMode.DefaultPermit`

Modes:

- `DefaultPermit`: allow unless explicitly denied
- `DefaultDeny`: deny unless explicitly permitted

### `PermitList`

`IpMatcher.Matcher` used when `Mode` is `DefaultDeny`.

Setter behavior:

- assigning `null` replaces it with an empty matcher

### `DenyList`

`IpMatcher.Matcher` used when `Mode` is `DefaultPermit`.

Setter behavior:

- assigning `null` replaces it with an empty matcher

### `Permit(string ip)`

Evaluates an IP address against the configured mode and matchers.

Validation:

- throws if `ip` is `null` or empty

## TimeoutSettings

File:

- `src/WatsonWebserver/Core/Settings/TimeoutSettings.cs`

### `DefaultTimeout`

Default:

- `TimeSpan.Zero`

Meaning:

- `TimeSpan.Zero` disables API-route request timeouts
- positive values enable timeout cancellation and 408 behavior on API routes

Validation:

- throws if less than `TimeSpan.Zero`

## Common Examples

### Basic HTTP/1.1 server

```csharp
WebserverSettings settings = new WebserverSettings("127.0.0.1", 8080);
settings.Protocols.EnableHttp1 = true;
settings.Protocols.EnableHttp2 = false;
settings.Protocols.EnableHttp3 = false;
```

### HTTP/2 over TLS

```csharp
WebserverSettings settings = new WebserverSettings("localhost", 8443, true);
settings.Ssl.PfxCertificateFile = "server.pfx";
settings.Ssl.PfxCertificatePassword = "password";
settings.Protocols.EnableHttp1 = true;
settings.Protocols.EnableHttp2 = true;
```

### HTTP/3 with Alt-Svc

```csharp
WebserverSettings settings = new WebserverSettings("localhost", 8443, true);
settings.Ssl.PfxCertificateFile = "server.pfx";
settings.Ssl.PfxCertificatePassword = "password";

settings.Protocols.EnableHttp1 = true;
settings.Protocols.EnableHttp2 = true;
settings.Protocols.EnableHttp3 = true;

settings.AltSvc.Enabled = true;
settings.AltSvc.Http3Alpn = "h3";
settings.AltSvc.MaxAgeSeconds = 86400;
```

### API-route timeout

```csharp
WebserverSettings settings = new WebserverSettings("127.0.0.1", 8080);
settings.Timeout.DefaultTimeout = TimeSpan.FromSeconds(30);
```

### HTTP/1.1 pool and cache tuning

```csharp
WebserverSettings settings = new WebserverSettings("127.0.0.1", 8080);
settings.IO.Http1.PoolMaxRetainedPerType = 256;
settings.IO.Http1.ResponseHeaderTemplateCacheSize = 256;
settings.IO.Http1.StatusLineCacheSize = 64;
```

### Default-deny access control

```csharp
WebserverSettings settings = new WebserverSettings("0.0.0.0", 8080);
settings.AccessControl.Mode = AccessControlMode.DefaultDeny;
settings.AccessControl.PermitList.Add("192.168.1.0", "255.255.255.0");
```

## Notes

- `WebserverSettings` is validated at startup; not every invalid protocol combination is rejected at property-assignment time
- HTTP/3 requires TLS and runtime QUIC support
- HTTP/2 cleartext requires explicit prior-knowledge enablement
- wildcard hosts (`*` and `+`) force `UseMachineHostname`
- `Watson` consumers configure `WebserverSettings` through the `Watson` package; the `WatsonWebserver.Core` namespace contains the shared settings types and abstractions
