namespace WatsonWebserver.Core.OpenApi
{
    /// <summary>
    /// Contact information for the API.
    /// </summary>
    public class OpenApiContact
    {
        /// <summary>
        /// The name of the contact person/organization.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// The URL for the contact.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// The email address of the contact.
        /// </summary>
        public string Email { get; set; } = null;
    }
}
