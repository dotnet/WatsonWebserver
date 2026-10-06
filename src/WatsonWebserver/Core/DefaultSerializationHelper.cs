namespace WatsonWebserver.Core
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
#if NET8_0_OR_GREATER
    using System.Diagnostics.CodeAnalysis;
#endif

    /// <summary>
    /// Default serialization helper, backed by System.Text.Json.
    /// <para>
    /// The parameterless constructor uses reflection-based serialization, so any type serializes without
    /// registration. Native AOT and trimmed applications should instead use
    /// <see cref="DefaultSerializationHelper(IJsonTypeInfoResolver)"/> with a source-generated
    /// <see cref="JsonSerializerContext"/> that declares the application's own types. Watson's own types
    /// (API error responses and health-check results) are always available without registration.
    /// </para>
    /// </summary>
    public class DefaultSerializationHelper : ISerializationHelper
    {
        #region Public-Members

        /// <summary>
        /// True when this instance uses reflection-based serialization. False when it resolves types only
        /// through source-generated metadata: Watson's own types plus the resolver supplied to
        /// <see cref="DefaultSerializationHelper(IJsonTypeInfoResolver)"/>, if any.
        /// </summary>
        public bool UsesReflection
        {
            get
            {
                return _UsesReflection;
            }
        }

        #endregion

        #region Private-Members

        private static readonly IJsonTypeInfoResolver _ReflectionResolver = JsonReflectionFallback.CreateResolver();
        private static readonly JsonSerializerOptions _ReflectionPrettyOptions = CreateReflectionOptions(true);
        private static readonly JsonSerializerOptions _ReflectionCompactOptions = CreateReflectionOptions(false);
        private static readonly JsonSerializerOptions _ReflectionDeserializerOptions = CreateDeserializerOptions(_ReflectionResolver);

        private readonly bool _UsesReflection;
        private readonly JsonSerializerOptions _PrettyOptions;
        private readonly JsonSerializerOptions _CompactOptions;
        private readonly JsonSerializerOptions _DeserializerOptions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate using reflection-based serialization, which serializes any type without registration.
        /// When reflection-based serialization is disabled, as it is by default under native AOT and trimming,
        /// the instance falls back to Watson's source-generated metadata and application types cannot be
        /// serialized; use <see cref="DefaultSerializationHelper(IJsonTypeInfoResolver)"/> there instead.
        /// </summary>
#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Reflection-based JSON serialization may need types and members that trimming removes. Use DefaultSerializationHelper(IJsonTypeInfoResolver) with a JsonSerializerContext instead.")]
        [RequiresDynamicCode("Reflection-based JSON serialization may need runtime code generation. Use DefaultSerializationHelper(IJsonTypeInfoResolver) with a JsonSerializerContext instead.")]
#endif
        public DefaultSerializationHelper() : this(null, true)
        {
        }

        /// <summary>
        /// Instantiate using source-generated metadata, which is safe under native AOT and trimming.
        /// Application types are resolved through <paramref name="typeInfoResolver"/>, typically a
        /// <see cref="JsonSerializerContext"/> that declares them with <see cref="JsonSerializableAttribute"/>.
        /// Watson's own types are always available. Reflection is never used, even where it is available,
        /// so an application behaves the same with and without native AOT.
        /// </summary>
        /// <param name="typeInfoResolver">Resolver for application types, for example <c>AppJsonContext.Default</c>. May not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeInfoResolver"/> is null.</exception>
        public DefaultSerializationHelper(IJsonTypeInfoResolver typeInfoResolver)
            : this(typeInfoResolver ?? throw new ArgumentNullException(nameof(typeInfoResolver)), false)
        {
        }

        private DefaultSerializationHelper(IJsonTypeInfoResolver typeInfoResolver, bool preferReflection)
        {
            if (preferReflection && _ReflectionResolver != null)
            {
                _UsesReflection = true;
                _PrettyOptions = _ReflectionPrettyOptions;
                _CompactOptions = _ReflectionCompactOptions;
                _DeserializerOptions = _ReflectionDeserializerOptions;
                return;
            }

            IJsonTypeInfoResolver resolver = WatsonJsonContext.Default;
            if (typeInfoResolver != null) resolver = JsonTypeInfoResolver.Combine(typeInfoResolver, WatsonJsonContext.Default);

            _UsesReflection = false;
            _PrettyOptions = CreateMetadataOptions(resolver, true);
            _CompactOptions = CreateMetadataOptions(resolver, false);
            _DeserializerOptions = CreateDeserializerOptions(resolver);
        }

        /// <summary>
        /// Create the serializer a webserver uses until one is assigned: reflection-based where reflection-based
        /// serialization is enabled, otherwise Watson's source-generated metadata only.
        /// </summary>
        /// <returns>Serialization helper.</returns>
        internal static DefaultSerializationHelper CreateDefault()
        {
            return new DefaultSerializationHelper(null, true);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Deserialize JSON to an instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no serialization metadata is available for <typeparamref name="T"/>.</exception>
        public T DeserializeJson<T>(string json)
        {
            JsonTypeInfo<T> typeInfo = (JsonTypeInfo<T>)GetTypeInfo(_DeserializerOptions, typeof(T));
            return JsonSerializer.Deserialize(json, typeInfo);
        }

        /// <summary>
        /// Serialize object to JSON.
        /// </summary>
        /// <param name="obj">Object.</param>
        /// <param name="pretty">Pretty print.</param>
        /// <returns>JSON.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no serialization metadata is available for the runtime type of <paramref name="obj"/>.</exception>
        public string SerializeJson(object obj, bool pretty = true)
        {
            if (obj == null) return null;

            JsonSerializerOptions options = pretty ? _PrettyOptions : _CompactOptions;
            return JsonSerializer.Serialize(obj, GetTypeInfo(options, obj.GetType()));
        }

        #endregion

        #region Private-Methods

        private static JsonSerializerOptions CreateReflectionOptions(bool pretty)
        {
            if (_ReflectionResolver == null) return null;

            JsonSerializerOptions options = CreateBaseOptions(_ReflectionResolver, pretty);

            // see https://github.com/dotnet/runtime/issues/43026
            options.Converters.Add(new ReflectionExceptionConverter());
            options.Converters.Add(new NameValueCollectionConverter());
            options.Converters.Add(JsonReflectionFallback.CreateStringEnumConverter());
            options.Converters.Add(new DateTimeConverter());
            options.Converters.Add(new IntPtrConverter());
            options.Converters.Add(new IPAddressConverter());
            return options;
        }

        private static JsonSerializerOptions CreateMetadataOptions(IJsonTypeInfoResolver resolver, bool pretty)
        {
            JsonSerializerOptions options = CreateBaseOptions(resolver, pretty);

            // Enum handling comes from the resolver: Watson's metadata writes its enums as strings, and an
            // application context opts in with [JsonSourceGenerationOptions(UseStringEnumConverter = true)].
            options.Converters.Add(new MetadataExceptionConverter());
            options.Converters.Add(new NameValueCollectionConverter());
            options.Converters.Add(new DateTimeConverter());
            options.Converters.Add(new IntPtrConverter());
            options.Converters.Add(new IPAddressConverter());
            return options;
        }

        private static JsonSerializerOptions CreateBaseOptions(IJsonTypeInfoResolver resolver, bool pretty)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.WriteIndented = pretty;
            options.TypeInfoResolver = resolver;
            return options;
        }

        private static JsonSerializerOptions CreateDeserializerOptions(IJsonTypeInfoResolver resolver)
        {
            if (resolver == null) return null;

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.TypeInfoResolver = resolver;
            return options;
        }

        private static JsonTypeInfo GetTypeInfo(JsonSerializerOptions options, Type type)
        {
            JsonTypeInfo typeInfo;
            if (options.TryGetTypeInfo(type, out typeInfo)) return typeInfo;

            throw new InvalidOperationException(
                "No JSON serialization metadata is available for type '" + type.FullName + "'. " +
                "Declare it with [JsonSerializable(typeof(" + type.Name + "))] on a JsonSerializerContext and assign " +
                "new DefaultSerializationHelper(YourJsonContext.Default) to the webserver's Serializer property. " +
                "This is required under native AOT and trimming, where reflection-based serialization is disabled.");
        }

        private static void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            JsonSerializer.Serialize(writer, value, GetTypeInfo(options, value.GetType()));
        }

        #endregion

        #region Private-Embedded-Classes

        /// <summary>
        /// Writes every public property of an exception, including those declared by derived exception types.
        /// Registered only on reflection-based options.
        /// </summary>
        private class ReflectionExceptionConverter : JsonConverter<Exception>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(Exception).IsAssignableFrom(typeToConvert);
            }

            public override Exception Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException("Deserializing exceptions is not allowed");
            }

#if NET8_0_OR_GREATER
            [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Registered only on reflection-based options, which exist only when reflection-based serialization is enabled.")]
#endif
            public override void Write(Utf8JsonWriter writer, Exception value, JsonSerializerOptions options)
            {
                bool skipNulls = options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull;
                List<KeyValuePair<string, object>> properties = new List<KeyValuePair<string, object>>();

                foreach (PropertyInfo property in value.GetType().GetProperties())
                {
                    if (property.Name == nameof(Exception.TargetSite)) continue;

                    object propertyValue = property.GetValue(value);
                    if (propertyValue == null && skipNulls) continue;

                    properties.Add(new KeyValuePair<string, object>(property.Name, propertyValue));
                }

                if (properties.Count == 0)
                {
                    // Nothing to write
                    return;
                }

                writer.WriteStartObject();

                foreach (KeyValuePair<string, object> property in properties)
                {
                    writer.WritePropertyName(property.Key);
                    WriteValue(writer, property.Value, options);
                }

                writer.WriteEndObject();
            }
        }

        /// <summary>
        /// Writes the properties declared by <see cref="Exception"/>, in the order reflection reports them,
        /// without reflection. Properties declared by derived exception types are not written.
        /// Registered on source-generated metadata options.
        /// </summary>
        private class MetadataExceptionConverter : JsonConverter<Exception>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(Exception).IsAssignableFrom(typeToConvert);
            }

            public override Exception Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException("Deserializing exceptions is not allowed");
            }

            public override void Write(Utf8JsonWriter writer, Exception value, JsonSerializerOptions options)
            {
                bool skipNulls = options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull;

                writer.WriteStartObject();

                WriteString(writer, nameof(Exception.Message), value.Message, skipNulls);

                IDictionary data = value.Data;
                if (data != null)
                {
                    writer.WritePropertyName(nameof(Exception.Data));
                    WriteData(writer, data, options);
                }
                else if (!skipNulls)
                {
                    writer.WriteNull(nameof(Exception.Data));
                }

                if (value.InnerException != null)
                {
                    writer.WritePropertyName(nameof(Exception.InnerException));
                    Write(writer, value.InnerException, options);
                }
                else if (!skipNulls)
                {
                    writer.WriteNull(nameof(Exception.InnerException));
                }

                WriteString(writer, nameof(Exception.HelpLink), value.HelpLink, skipNulls);
                WriteString(writer, nameof(Exception.Source), value.Source, skipNulls);
                writer.WriteNumber(nameof(Exception.HResult), value.HResult);
                WriteString(writer, nameof(Exception.StackTrace), value.StackTrace, skipNulls);

                writer.WriteEndObject();
            }

            private static void WriteString(Utf8JsonWriter writer, string name, string value, bool skipNulls)
            {
                if (value != null)
                {
                    writer.WriteString(name, value);
                }
                else if (!skipNulls)
                {
                    writer.WriteNull(name);
                }
            }

            private static void WriteData(Utf8JsonWriter writer, IDictionary data, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach (DictionaryEntry entry in data)
                {
                    writer.WritePropertyName(entry.Key.ToString());

                    if (entry.Value == null)
                    {
                        writer.WriteNullValue();
                        continue;
                    }

                    JsonTypeInfo typeInfo;
                    if (options.TryGetTypeInfo(entry.Value.GetType(), out typeInfo))
                    {
                        JsonSerializer.Serialize(writer, entry.Value, typeInfo);
                    }
                    else
                    {
                        writer.WriteStringValue(entry.Value.ToString());
                    }
                }

                writer.WriteEndObject();
            }
        }

        private class NameValueCollectionConverter : JsonConverter<NameValueCollection>
        {
            public override NameValueCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, NameValueCollection value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach (string key in value.Keys)
                {
                    writer.WriteString(key, String.Join(", ", value.GetValues(key)));
                }

                writer.WriteEndObject();
            }
        }

        private class DateTimeConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(
                        ref Utf8JsonReader reader,
                        Type typeToConvert,
                        JsonSerializerOptions options)
            {
                string str = reader.GetString();

                DateTime val;
                if (DateTime.TryParse(str, out val)) return val;

                throw new FormatException("The JSON value '" + str + "' could not be converted to System.DateTime.");
            }

            public override void Write(
                Utf8JsonWriter writer,
                DateTime dateTimeValue,
                JsonSerializerOptions options)
            {
                writer.WriteStringValue(dateTimeValue.ToString(
                    "yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture));
            }

            private List<string> _AcceptedFormats = new List<string>
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssK",
                "yyyy-MM-dd HH:mm:ss.ffffff",
                "yyyy-MM-ddTHH:mm:ss.ffffff",
                "yyyy-MM-ddTHH:mm:ss.fffffffK",
                "yyyy-MM-dd",
                "MM/dd/yyyy HH:mm",
                "MM/dd/yyyy hh:mm tt",
                "MM/dd/yyyy H:mm",
                "MM/dd/yyyy h:mm tt",
                "MM/dd/yyyy HH:mm:ss"
            };
        }

        private class IntPtrConverter : JsonConverter<IntPtr>
        {
            public override IntPtr Read(
                        ref Utf8JsonReader reader,
                        Type typeToConvert,
                        JsonSerializerOptions options)
            {
                throw new FormatException("IntPtr cannot be deserialized.");
            }

            public override void Write(
                Utf8JsonWriter writer,
                IntPtr intPtrValue,
                JsonSerializerOptions options)
            {
                writer.WriteStringValue(intPtrValue.ToString());
            }
        }

        private class IPAddressConverter : JsonConverter<IPAddress>
        {
            public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string str = reader.GetString();
                return IPAddress.Parse(str);
            }

            public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString());
            }
        }

        #endregion
    }
}
