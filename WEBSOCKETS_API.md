# WebSockets API

This document describes the current Watson 7 WebSocket surface.

## Scope

Current implementation scope:

- HTTP/1.1 WebSockets only
- Watson-owned handshake and routing
- Watson-owned `WebSocketSession`
- whole-message receive semantics
- session enumeration and disconnect APIs

Not currently implemented:

- HTTP/2 WebSockets
- HTTP/3 WebSockets
- public raw-socket access
- public subprotocol negotiation configuration

## Route Registration

Enable WebSockets through `WebserverSettings.WebSockets.Enable`, then register routes with `server.WebSocket(...)`.

```csharp
using WatsonWebserver;
using WatsonWebserver.Core;
using WatsonWebserver.Core.WebSockets;

WebserverSettings settings = new WebserverSettings("127.0.0.1", 9000);
settings.WebSockets.Enable = true;

Webserver server = new Webserver(settings, DefaultRoute);

server.WebSocket("/chat", HandleSocketAsync);
server.WebSocket("/chat/{room}", HandleSocketAsync);
```

Handler shape:

```csharp
Task HandleSocketAsync(HttpContextBase context, WebSocketSession session)
```

## Same-Path HTTP And WebSocket Routing

Watson allows the same path to be registered for both HTTP and WebSocket handling.

```csharp
server.Get("/chat", async req => new { Mode = "http" });
server.WebSocket("/chat", HandleSocketAsync);
```

Dispatch rule:

- ordinary HTTP requests follow normal HTTP route matching
- WebSocket upgrade requests are matched against websocket routes first

## WebSocketSession

`WebSocketSession` is the Watson-owned session abstraction.

Useful members:

- `Guid Id`
- `bool IsConnected`
- `WebSocketState State`
- `WebSocketCloseStatus? CloseStatus`
- `string CloseStatusDescription`
- `string Subprotocol`
- `string RemoteIp`
- `int RemotePort`
- `WebSocketRequestDescriptor Request`
- `object Metadata`
- `WebSocketSessionStatistics Statistics`

### Request metadata

`session.Request` retains reduced immutable handshake data:

- `Path`
- `NormalizedPath`
- `Query`
- `Headers`
- `RequestedVersion`
- `RequestedSubprotocols`
- `RemoteIp`
- `RemotePort`

## Receive Semantics

Use `ReceiveAsync()` to receive a single whole message:

```csharp
WebSocketMessage message = await session.ReceiveAsync(ctx.Token);
if (message != null && message.MessageType == WebSocketMessageType.Text)
{
    await session.SendTextAsync("echo:" + message.Text, ctx.Token);
}
```

Use `ReadMessagesAsync()` for continuous consumption:

```csharp
await foreach (WebSocketMessage message in session.ReadMessagesAsync(ctx.Token))
{
    if (message.MessageType == WebSocketMessageType.Text)
    {
        await session.SendTextAsync("echo:" + message.Text, ctx.Token);
    }
}
```

Behavior:

- receive is whole-message, not frame-level
- fragmented frames are reassembled before delivery
- only one active receive operation is allowed per session
- oversized messages are rejected according to `MaxMessageSize`

## Sending

Text:

```csharp
await session.SendTextAsync("hello", ctx.Token);
```

Binary:

```csharp
await session.SendBinaryAsync(bytes, ctx.Token);
await session.SendBinaryAsync(new ArraySegment<byte>(bytes), ctx.Token);
```

Behavior:

- sends are serialized per session
- counters are updated on successful send

## Closing

```csharp
await session.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", ctx.Token);
```

Watson also closes sessions when:

- the route completes while still connected
- the server stops
- a route handler throws
- an oversized message is received

## Server-Level Session APIs

List sessions:

```csharp
IEnumerable<WebSocketSession> sessions = server.ListWebSocketSessions();
```

Check connectivity:

```csharp
bool connected = server.IsWebSocketSessionConnected(guid);
```

Disconnect by id:

```csharp
await server.DisconnectWebSocketSessionAsync(
    guid,
    WebSocketCloseStatus.NormalClosure,
    "disconnect");
```

## Lifecycle Events

Observability events are exposed on `server.Events`:

- `WebSocketSessionStarted`
- `WebSocketSessionEnded`
- `WebSocketHandshakeFailed`

Example:

```csharp
server.Events.WebSocketSessionStarted += (sender, args) =>
{
    Console.WriteLine("WS started " + args.Session.Id + " " + args.Session.Request.Path);
};
```

## Settings

WebSocket settings live under `WebserverSettings.WebSockets`.

Common settings:

- `Enable`
- `MaxMessageSize`
- `ReceiveBufferSize`
- `CloseHandshakeTimeoutMs`
- `AllowClientSuppliedGuid`
- `ClientGuidHeaderName`
- `SupportedVersions`
- `EnableHttp1`

Current defaults:

- `Enable = false`
- `MaxMessageSize = 16777216`
- `ReceiveBufferSize = 65536`
- `CloseHandshakeTimeoutMs = 5000`
- `AllowClientSuppliedGuid = false`
- `ClientGuidHeaderName = "x-guid"`
- `SupportedVersions = ["13"]`
- `EnableHttp1 = true`

## Limitations

- Current support is HTTP/1.1 only
- HTTP/2 and HTTP/3 WebSockets are planned follow-up work
- Public subprotocol negotiation support is not yet available
- No public raw `System.Net.WebSockets.WebSocket` escape hatch exists
