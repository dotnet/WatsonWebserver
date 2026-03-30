namespace WatsonWebserver.Core.Http3
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Detects QUIC runtime availability for HTTP/3.
    /// </summary>
    public static class Http3RuntimeDetector
    {
        /// <summary>
        /// Detect HTTP/3 QUIC runtime availability.
        /// </summary>
        /// <returns>Availability details.</returns>
        public static Http3RuntimeAvailability Detect()
        {
            Http3RuntimeAvailability availability = new Http3RuntimeAvailability();

            try
            {
                Type listenerType = Type.GetType("System.Net.Quic.QuicListener, System.Net.Quic");
                if (listenerType == null)
                {
                    availability.AssemblyPresent = false;
                    availability.IsAvailable = false;
                    availability.Message = "System.Net.Quic is not available in the current runtime.";
                    return availability;
                }

                availability.AssemblyPresent = true;

                PropertyInfo isSupportedProperty = listenerType.GetProperty("IsSupported", BindingFlags.Public | BindingFlags.Static);
                if (isSupportedProperty == null)
                {
                    availability.IsAvailable = false;
                    availability.Message = "System.Net.Quic.QuicListener.IsSupported is not available in the current runtime.";
                    return availability;
                }

                object rawValue = isSupportedProperty.GetValue(null);
                if (!(rawValue is bool))
                {
                    availability.IsAvailable = false;
                    availability.Message = "System.Net.Quic.QuicListener.IsSupported returned an unexpected value.";
                    return availability;
                }

                availability.IsAvailable = (bool)rawValue;
                availability.Message = availability.IsAvailable
                    ? "System.Net.Quic is available."
                    : "System.Net.Quic is present but the QUIC runtime is unavailable on this platform.";
                return availability;
            }
            catch (Exception e)
            {
                availability.AssemblyPresent = false;
                availability.IsAvailable = false;
                availability.Message = "QUIC runtime detection failed: " + e.Message;
                return availability;
            }
        }
    }
}
