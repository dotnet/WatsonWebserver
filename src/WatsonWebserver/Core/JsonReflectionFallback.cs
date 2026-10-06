namespace WatsonWebserver.Core
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
#if NET8_0_OR_GREATER
    using System.Diagnostics.CodeAnalysis;
#endif

    /// <summary>
    /// Creates the reflection-based System.Text.Json components Watson uses when reflection-based
    /// serialization is available. Native AOT and trimmed applications disable reflection-based
    /// serialization by default (<see cref="JsonSerializer.IsReflectionEnabledByDefault"/> is false), so
    /// every member returns null there and callers fall back to source-generated metadata.
    /// </summary>
    internal static class JsonReflectionFallback
    {
        #region Internal-Members

        /// <summary>
        /// True when reflection-based serialization is enabled for this application.
        /// </summary>
        internal static bool IsEnabled
        {
            get
            {
                return JsonSerializer.IsReflectionEnabledByDefault;
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Create a reflection-based type info resolver.
        /// </summary>
        /// <returns>Resolver, or null when reflection-based serialization is disabled.</returns>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only reached when JsonSerializer.IsReflectionEnabledByDefault is true, which native AOT and trimmed applications disable by default.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only reached when JsonSerializer.IsReflectionEnabledByDefault is true, which native AOT and trimmed applications disable by default.")]
#endif
        internal static IJsonTypeInfoResolver CreateResolver()
        {
            if (!JsonSerializer.IsReflectionEnabledByDefault) return null;
            return new DefaultJsonTypeInfoResolver();
        }

        /// <summary>
        /// Create the non-generic string enum converter, which writes every enum as its name.
        /// </summary>
        /// <returns>Converter, or null when reflection-based serialization is disabled.</returns>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only reached when JsonSerializer.IsReflectionEnabledByDefault is true, which native AOT and trimmed applications disable by default.")]
#endif
        internal static JsonConverter CreateStringEnumConverter()
        {
            if (!JsonSerializer.IsReflectionEnabledByDefault) return null;
            return new JsonStringEnumConverter();
        }

        #endregion
    }
}
