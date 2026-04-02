namespace WatsonWebserver.Core.Settings
{
    /// <summary>
    /// Debug logging settings.
    /// Be sure to set Events.Logger in order to receive debug messages.
    /// </summary>
    public class DebugSettings
    {
        /// <summary>
        /// Enable or disable debug logging of access control.
        /// </summary>
        public bool AccessControl { get; set; } = false;

        /// <summary>
        /// Enable or disable debug logging of routing.
        /// </summary>
        public bool Routing { get; set; } = false;

        /// <summary>
        /// Enable or disable debug logging of requests.
        /// </summary>
        public bool Requests { get; set; } = false;

        /// <summary>
        /// Enable or disable debug logging of responses.
        /// </summary>
        public bool Responses { get; set; } = false;

        /// <summary>
        /// Debug logging settings.
        /// Be sure to set Events.Logger in order to receive debug messages.
        /// </summary>
        public DebugSettings()
        {
        }
    }
}
