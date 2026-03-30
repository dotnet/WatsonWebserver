# Migrating From WatsonWebsocket 4.x

This guide maps the old WatsonWebsocket server-side model to the Watson 7 WebSocket surface.

## Scope

This migration guide is for server-side usage patterns that previously depended on WatsonWebsocket 4.x.

Watson 7 WebSocket support is currently:

- HTTP/1.1 only
- route-oriented
- session-oriented
- message-oriented

## Concept Mapping

| WatsonWebsocket 4.x | Watson 7 |
|---|---|
| server-level websocket library | built into `Watson` |
| `ClientConnected` event | route start plus `server.Events.WebSocketSessionStarted` |
| `ClientDisconnected` event | `server.Events.WebSocketSessionEnded` |
| `MessageReceived` event | `session.ReceiveAsync()` / `session.ReadMessagesAsync()` |
| client GUID header | `Settings.WebSockets.ClientGuidHeaderName` |
| `ListClients()` | `ListWebSocketSessions()` |
| `IsClientConnected(Guid)` | `IsWebSocketSessionConnected(Guid)` |
| `DisconnectClient(Guid)` | `DisconnectWebSocketSessionAsync(...)` |

## Major Behavioral Changes

### 1. Routing is first-class

WatsonWebsocket centered the application model around websocket lifecycle events.

Watson 7 centers the model around route handlers:

```csharp
server.WebSocket("/chat", async (ctx, session) =>
{
    await foreach (WebSocketMessage message in session.ReadMessagesAsync(ctx.Token))
    {
        await session.SendTextAsync("echo:" + message.Text, ctx.Token);
    }
});
```

### 2. Per-message global callbacks are not the primary API

In Watson 7, application message handling belongs inside the websocket route handler.

Server-level websocket events are observability-only:

- `WebSocketSessionStarted`
- `WebSocketSessionEnded`
- `WebSocketHandshakeFailed`

### 3. Same-path HTTP and WebSocket routing is supported

You can now use the same path for both HTTP and WebSocket behavior:

```csharp
server.Get("/chat", async req => new { Mode = "http" });

server.WebSocket("/chat", async (ctx, session) =>
{
    await session.SendTextAsync("connected", ctx.Token);
});
```

### 4. Client-supplied GUIDs are opt-in

This is one of the most important changed defaults.

Watson 7 defaults:

- `Settings.WebSockets.AllowClientSuppliedGuid = false`
- `Settings.WebSockets.ClientGuidHeaderName = "x-guid"`

To restore compatibility with client-supplied GUID behavior:

```csharp
server.Settings.WebSockets.Enable = true;
server.Settings.WebSockets.AllowClientSuppliedGuid = true;
server.Settings.WebSockets.ClientGuidHeaderName = "x-guid";
```

### 5. Receive is whole-message and session-owned

Watson 7 preserves whole-message delivery, but receive operations are owned by `WebSocketSession`.

Important rule:

- only one active receive operation is allowed per session

## Before And After

### Connected / disconnected event model

WatsonWebsocket-style:

```csharp
server.ClientConnected += (sender, args) =>
{
    Console.WriteLine(args.Client.Guid);
};

server.ClientDisconnected += (sender, args) =>
{
    Console.WriteLine(args.Client.Guid);
};
```

Watson 7:

```csharp
server.Events.WebSocketSessionStarted += (sender, args) =>
{
    Console.WriteLine(args.Session.Id);
};

server.Events.WebSocketSessionEnded += (sender, args) =>
{
    Console.WriteLine(args.Session.Id);
};
```

### Message handling

WatsonWebsocket-style:

```csharp
server.MessageReceived += async (sender, args) =>
{
    await server.SendAsync(args.Client.Guid, "echo:" + args.DataAsString);
};
```

Watson 7:

```csharp
server.WebSocket("/echo", async (ctx, session) =>
{
    await foreach (WebSocketMessage message in session.ReadMessagesAsync(ctx.Token))
    {
        await session.SendTextAsync("echo:" + message.Text, ctx.Token);
    }
});
```

### Session enumeration and disconnect

WatsonWebsocket-style:

```csharp
var clients = server.ListClients();
bool connected = server.IsClientConnected(guid);
await server.DisconnectClient(guid);
```

Watson 7:

```csharp
IEnumerable<WebSocketSession> sessions = server.ListWebSocketSessions();
bool connected = server.IsWebSocketSessionConnected(guid);
await server.DisconnectWebSocketSessionAsync(
    guid,
    WebSocketCloseStatus.NormalClosure,
    "disconnect");
```

## HTTP Fallback Patterns

In WatsonWebsocket, some applications used raw HTTP fallback patterns next to websocket handling.

In Watson 7, use normal Watson HTTP routes for that behavior:

```csharp
server.Get("/chat", async req => new { Transport = "http" });
server.WebSocket("/chat", HandleSocketAsync);
```

## Unsupported Or Changed Patterns

These patterns are intentionally different in Watson 7:

- no public raw websocket escape hatch
- no primary global per-message callback model
- no HTTP/2 or HTTP/3 websocket runtime yet
- no public subprotocol negotiation surface yet

## Recommended Migration Steps

1. Move websocket endpoint registration into `server.WebSocket(...)`.
2. Move message processing logic into route handlers using `ReceiveAsync()` or `ReadMessagesAsync()`.
3. Replace old client-registry calls with Watson 7 session APIs.
4. Decide whether you need client-supplied GUID compatibility and explicitly enable it if required.
5. Keep observability concerns on `server.Events`, not in the application message path.
