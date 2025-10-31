namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Server sent event.
    /// </summary>
    public class ServerSentEvent
    {
        /// <summary>
        /// ID.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Event.
        /// </summary>
        public string Event { get; set; } = null;

        /// <summary>
        /// Data.
        /// </summary>
        public string Data { get; set; } = null;

        /// <summary>
        /// Retry.
        /// </summary>
        public string Retry { get; set; } = null;

        /// <summary>
        /// Server sent event.
        /// </summary>
        public ServerSentEvent()
        {

        }

        /// <summary>
        /// Create a string representation useful for transmission.
        /// </summary>
        /// <returns>String.</returns>
        public string ToEventString()
        {
            if (string.IsNullOrEmpty(Id) &&
                string.IsNullOrEmpty(Event) &&
                string.IsNullOrEmpty(Data) &&
                string.IsNullOrEmpty(Retry))
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Id))
            {
                if (Id.Contains("\n") || Id.Contains("\r"))
                    throw new ArgumentException("Server sent event Id property cannot contain newline characters.");
                sb.Append($"id: {Id}\n");
            }

            if (!string.IsNullOrEmpty(Event))
            {
                if (Event.Contains("\n") || Event.Contains("\r"))
                    throw new ArgumentException("Server sent event Event property cannot contain newline characters.");
                sb.Append($"event: {Event}\n");
            }

            if (!string.IsNullOrEmpty(Data))
            {
                // Handle multiline data by prefixing each line with "data: "
                string[] lines = Data.Split('\n');
                foreach (string line in lines)
                {
                    string cleanLine = line.Replace("\r", "");
                    sb.Append($"data: {cleanLine}\n");
                }
            }

            if (!string.IsNullOrEmpty(Retry))
            {
                if (!int.TryParse(Retry, out int retryValue) || retryValue < 0)
                    throw new ArgumentException("Server-sent event Retry property must be a non-negative integer.");
                sb.Append($"retry: {Retry}\n");
            }

            // Add the final newline to separate this event from the next
            sb.Append("\n");

            return sb.ToString();
        }
    }
}
