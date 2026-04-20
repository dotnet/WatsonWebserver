namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI discriminator metadata used to indicate the property that
    /// distinguishes between sibling schemas in a polymorphic composition
    /// such as <c>oneOf</c>, <c>anyOf</c>, or <c>allOf</c>.
    /// </summary>
    public class OpenApiDiscriminatorMetadata
    {
        #region Public-Members

        /// <summary>
        /// Name of the JSON property used as the discriminator.
        /// Required when a discriminator is present.
        /// </summary>
        [JsonPropertyName("propertyName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string PropertyName
        {
            get => _PropertyName;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(PropertyName));
                _PropertyName = value;
            }
        }

        /// <summary>
        /// Optional mapping from discriminator values to schema references.
        /// Keys are the literal values that may appear in <see cref="PropertyName"/>;
        /// values are component schema references such as
        /// <c>#/components/schemas/Cat</c>.
        /// </summary>
        [JsonPropertyName("mapping")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string> Mapping { get; set; } = null;

        #endregion

        #region Private-Members

        private string _PropertyName = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiDiscriminatorMetadata()
        {
        }

        /// <summary>
        /// Instantiate the object with a property name and an optional mapping.
        /// </summary>
        /// <param name="propertyName">Discriminator property name.</param>
        /// <param name="mapping">Optional mapping from discriminator values to schema references.</param>
        public OpenApiDiscriminatorMetadata(string propertyName, Dictionary<string, string> mapping = null)
        {
            PropertyName = propertyName;
            Mapping = mapping;
        }

        #endregion
    }
}
