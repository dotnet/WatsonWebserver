namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;

    /// <summary>
    /// Provides typed access to request parameters from URL paths, query strings, or headers.
    /// Wraps a <see cref="NameValueCollection"/> with convenience methods for type-safe extraction.
    /// </summary>
    public class RequestParameters
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly NameValueCollection _Parameters;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with the supplied name-value collection.
        /// </summary>
        /// <param name="parameters">The underlying name-value collection. If null, an empty collection is used.</param>
        public RequestParameters(NameValueCollection parameters)
        {
            _Parameters = parameters ?? new NameValueCollection();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve a parameter value by name.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <returns>The value, or null if not found.</returns>
        public string this[string name]
        {
            get { return _Parameters[name]; }
        }

        /// <summary>
        /// Retrieve a parameter as an integer.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found or not parseable.</param>
        /// <returns>Integer value.</returns>
        public int GetInt(string name, int defaultValue = 0)
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return defaultValue;
            if (int.TryParse(value, out int result)) return result;
            return defaultValue;
        }

        /// <summary>
        /// Retrieve a parameter as a long integer.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found or not parseable.</param>
        /// <returns>Long value.</returns>
        public long GetLong(string name, long defaultValue = 0)
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return defaultValue;
            if (long.TryParse(value, out long result)) return result;
            return defaultValue;
        }

        /// <summary>
        /// Retrieve a parameter as a double.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found or not parseable.</param>
        /// <returns>Double value.</returns>
        public double GetDouble(string name, double defaultValue = 0.0)
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return defaultValue;
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result)) return result;
            return defaultValue;
        }

        /// <summary>
        /// Retrieve a parameter as a decimal.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found or not parseable.</param>
        /// <returns>Decimal value.</returns>
        public decimal GetDecimal(string name, decimal defaultValue = 0m)
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return defaultValue;
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result)) return result;
            return defaultValue;
        }

        /// <summary>
        /// Retrieve a parameter as a boolean.
        /// Supports true/false, 1/0, yes/no, y/n, on/off.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found or not parseable.</param>
        /// <returns>Boolean value.</returns>
        public bool GetBool(string name, bool defaultValue = false)
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return defaultValue;
            if (bool.TryParse(value, out bool result)) return result;

            value = value.ToLowerInvariant();
            if (value == "1" || value == "yes" || value == "y" || value == "on") return true;
            if (value == "0" || value == "no" || value == "n" || value == "off") return false;

            return defaultValue;
        }

        /// <summary>
        /// Retrieve a parameter as a DateTime.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found or not parseable. Defaults to DateTime.MinValue.</param>
        /// <returns>DateTime value.</returns>
        public DateTime GetDateTime(string name, DateTime? defaultValue = null)
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return defaultValue ?? DateTime.MinValue;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result)) return result;
            return defaultValue ?? DateTime.MinValue;
        }

        /// <summary>
        /// Retrieve a parameter as a TimeSpan.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found or not parseable. Defaults to TimeSpan.Zero.</param>
        /// <returns>TimeSpan value.</returns>
        public TimeSpan GetTimeSpan(string name, TimeSpan? defaultValue = null)
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return defaultValue ?? TimeSpan.Zero;
            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out TimeSpan result)) return result;
            return defaultValue ?? TimeSpan.Zero;
        }

        /// <summary>
        /// Retrieve a parameter as a Guid.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found or not parseable. Defaults to Guid.Empty.</param>
        /// <returns>Guid value.</returns>
        public Guid GetGuid(string name, Guid? defaultValue = null)
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return defaultValue ?? Guid.Empty;
            if (Guid.TryParse(value, out Guid result)) return result;
            return defaultValue ?? Guid.Empty;
        }

        /// <summary>
        /// Retrieve a parameter as an enum value.
        /// </summary>
        /// <typeparam name="T">Enum type.</typeparam>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found or not parseable.</param>
        /// <param name="ignoreCase">Whether to ignore case when parsing. Default is true.</param>
        /// <returns>Enum value.</returns>
        public T GetEnum<T>(string name, T defaultValue, bool ignoreCase = true) where T : struct, Enum
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return defaultValue;
            if (Enum.TryParse(value, ignoreCase, out T result)) return result;
            return defaultValue;
        }

        /// <summary>
        /// Retrieve a parameter as an array of strings by splitting on a separator.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="separator">Separator character. Default is comma.</param>
        /// <returns>String array. Empty array if parameter not found.</returns>
        public string[] GetArray(string name, char separator = ',')
        {
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return Array.Empty<string>();
            return value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Retrieve a parameter with a fallback default value.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="defaultValue">Default value if not found.</param>
        /// <returns>String value.</returns>
        public string GetValueOrDefault(string name, string defaultValue = null)
        {
            string value = _Parameters[name];
            return String.IsNullOrEmpty(value) ? defaultValue : value;
        }

        /// <summary>
        /// Check if a parameter exists.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <returns>True if the parameter exists.</returns>
        public bool Contains(string name)
        {
            return _Parameters[name] != null;
        }

        /// <summary>
        /// Retrieve all parameter keys.
        /// </summary>
        /// <returns>Array of parameter keys.</returns>
        public string[] GetKeys()
        {
            return _Parameters.AllKeys;
        }

        /// <summary>
        /// Try to retrieve a parameter as the specified type.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="name">Parameter name.</param>
        /// <param name="result">The parsed result if successful.</param>
        /// <returns>True if the parameter exists and was successfully converted.</returns>
        public bool TryGetValue<T>(string name, out T result)
        {
            result = default;
            string value = _Parameters[name];
            if (String.IsNullOrEmpty(value)) return false;

            try
            {
                if (typeof(T) == typeof(string))
                {
                    result = (T)(object)value;
                    return true;
                }
                else if (typeof(T) == typeof(int))
                {
                    if (int.TryParse(value, out int intResult))
                    {
                        result = (T)(object)intResult;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(long))
                {
                    if (long.TryParse(value, out long longResult))
                    {
                        result = (T)(object)longResult;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(bool))
                {
                    if (bool.TryParse(value, out bool boolResult))
                    {
                        result = (T)(object)boolResult;
                        return true;
                    }

                    string lower = value.ToLowerInvariant();
                    if (lower == "1" || lower == "yes" || lower == "y" || lower == "on")
                    {
                        result = (T)(object)true;
                        return true;
                    }
                    if (lower == "0" || lower == "no" || lower == "n" || lower == "off")
                    {
                        result = (T)(object)false;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(double))
                {
                    if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleResult))
                    {
                        result = (T)(object)doubleResult;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(decimal))
                {
                    if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal decimalResult))
                    {
                        result = (T)(object)decimalResult;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(DateTime))
                {
                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateResult))
                    {
                        result = (T)(object)dateResult;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(Guid))
                {
                    if (Guid.TryParse(value, out Guid guidResult))
                    {
                        result = (T)(object)guidResult;
                        return true;
                    }
                }
                else if (typeof(T).IsEnum)
                {
                    if (Enum.TryParse(typeof(T), value, true, out object enumResult))
                    {
                        result = (T)enumResult;
                        return true;
                    }
                }
                else
                {
                    result = (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
