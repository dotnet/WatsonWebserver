namespace WatsonWebserver.Core.WebSockets
{
    using System;
    using System.Net.WebSockets;
    using System.Text;

    /// <summary>
    /// Whole-message WebSocket payload.
    /// </summary>
    public class WebSocketMessage
    {
        /// <summary>
        /// Message type.
        /// </summary>
        public WebSocketMessageType MessageType { get; }

        /// <summary>
        /// Message payload.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// UTF-8 text payload when the message type is text.
        /// </summary>
        public string Text
        {
            get
            {
                if (MessageType != WebSocketMessageType.Text) return null;
                return Encoding.UTF8.GetString(Data);
            }
        }

        /// <summary>
        /// Payload length, in bytes.
        /// </summary>
        public int Length
        {
            get
            {
                return Data?.Length ?? 0;
            }
        }

        /// <summary>
        /// Instantiate the message.
        /// </summary>
        /// <param name="messageType">Message type.</param>
        /// <param name="data">Payload.</param>
        public WebSocketMessage(WebSocketMessageType messageType, byte[] data)
        {
            if (messageType != WebSocketMessageType.Text && messageType != WebSocketMessageType.Binary)
            {
                throw new ArgumentException("Only text and binary whole-message payloads are supported.", nameof(messageType));
            }

            MessageType = messageType;
            Data = data ?? Array.Empty<byte>();
        }

        /// <summary>
        /// Create a text message.
        /// </summary>
        /// <param name="data">UTF-8 text payload.</param>
        /// <returns>Message.</returns>
        public static WebSocketMessage FromText(string data)
        {
            return new WebSocketMessage(WebSocketMessageType.Text, data == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Create a binary message.
        /// </summary>
        /// <param name="data">Binary payload.</param>
        /// <returns>Message.</returns>
        public static WebSocketMessage FromBinary(byte[] data)
        {
            return new WebSocketMessage(WebSocketMessageType.Binary, data);
        }
    }
}
