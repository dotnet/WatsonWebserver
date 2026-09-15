namespace WatsonWebserver.Core.OpenApi
{
    using System;

    /// <summary>
    /// Describes a single webhook entry emitted under the document-level <c>webhooks</c> object.
    /// Webhooks are an OpenAPI 3.1 addition and are rejected when targeting OpenAPI 3.0. Each entry
    /// pairs an HTTP method with the operation metadata that documents the callback the API sends
    /// to the consumer.
    /// </summary>
    public class OpenApiWebhookMetadata
    {
        #region Public-Members

        /// <summary>
        /// The HTTP method the webhook uses, lower-cased when emitted (for example <c>post</c>).
        /// Defaults to <c>post</c>, the most common webhook verb. May not be null or empty.
        /// </summary>
        public string Method
        {
            get
            {
                return _Method;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Method));
                _Method = value;
            }
        }

        /// <summary>
        /// The operation metadata describing the webhook request. May not be null.
        /// </summary>
        public OpenApiRouteMetadata Operation
        {
            get
            {
                return _Operation;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Operation));
                _Operation = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Method = "post";
        private OpenApiRouteMetadata _Operation = new OpenApiRouteMetadata();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiWebhookMetadata()
        {
        }

        /// <summary>
        /// Instantiate the object with a method and operation.
        /// </summary>
        /// <param name="method">HTTP method (for example <c>post</c>). May not be null or empty.</param>
        /// <param name="operation">Operation metadata describing the webhook. May not be null.</param>
        public OpenApiWebhookMetadata(string method, OpenApiRouteMetadata operation)
        {
            Method = method;
            Operation = operation;
        }

        #endregion
    }
}
