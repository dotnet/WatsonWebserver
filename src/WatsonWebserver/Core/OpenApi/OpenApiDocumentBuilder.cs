namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using WatsonWebserver.Core.Routing;

    /// <summary>
    /// Assembles a single OpenAPI document tree from a set of Watson routes and
    /// <see cref="OpenApiSettings"/>. A fresh builder is created per document so the owning
    /// <see cref="OpenApiDocumentGenerator"/> holds no per-request state and is safe to reuse across
    /// concurrent requests. All version-sensitive emission (3.0 versus 3.1 versus 3.2) is decided
    /// here from <see cref="OpenApiSettings.Version"/>.
    /// </summary>
    internal class OpenApiDocumentBuilder
    {
        #region Private-Members

        private static readonly Regex _ParameterRegex = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);
        private static readonly Regex _CleanPathRegex = new Regex(@"^[A-Za-z0-9/_\-.{}*]*$", RegexOptions.Compiled);

        private readonly OpenApiSettings _Settings;
        private readonly OpenApiVersionEnum _Version;
        private readonly HashSet<string> _OperationIds = new HashSet<string>(StringComparer.Ordinal);

        #endregion

        #region Constructors-and-Factories

        internal OpenApiDocumentBuilder(OpenApiSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _Settings = settings;
            _Version = settings.Version;
        }

        #endregion

        #region Internal-Methods

        internal Dictionary<string, object> Build(WebserverRoutes routes)
        {
            if (routes == null) throw new ArgumentNullException(nameof(routes));

            ValidateTopLevel();

            Dictionary<string, object> document = new Dictionary<string, object>();
            document["openapi"] = ResolveVersionString();

            if (AtLeast(OpenApiVersionEnum.V3_2) && !String.IsNullOrEmpty(_Settings.Self))
                document["$self"] = _Settings.Self;

            if (AtLeast(OpenApiVersionEnum.V3_1) && !String.IsNullOrEmpty(_Settings.JsonSchemaDialect))
                document["jsonSchemaDialect"] = _Settings.JsonSchemaDialect;

            document["info"] = BuildInfo();

            Dictionary<string, object> paths = BuildPaths(routes);
            bool hasWebhooks = HasWebhooks();

            if (!AtLeast(OpenApiVersionEnum.V3_1))
            {
                // OpenAPI 3.0 requires the paths object, even when empty.
                document["paths"] = paths;
            }
            else if (paths.Count > 0 || !hasWebhooks)
            {
                // OpenAPI 3.1+ allows paths to be omitted when webhooks are present.
                document["paths"] = paths;
            }

            if (hasWebhooks) document["webhooks"] = BuildWebhooks();

            if (_Settings.Servers != null && _Settings.Servers.Count > 0)
                document["servers"] = BuildServers();

            if (_Settings.Tags != null && _Settings.Tags.Count > 0)
                document["tags"] = BuildTags();

            Dictionary<string, object> components = BuildComponents();
            if (components.Count > 0) document["components"] = components;

            if (_Settings.Security != null && _Settings.Security.Count > 0)
                document["security"] = _Settings.Security;

            if (_Settings.ExternalDocs != null)
                document["externalDocs"] = BuildExternalDocs(_Settings.ExternalDocs);

            return document;
        }

        #endregion

        #region Private-Methods-Validation

        private void ValidateTopLevel()
        {
            if (_Settings.Info != null && _Settings.Info.License != null)
            {
                OpenApiLicense license = _Settings.Info.License;
                bool hasIdentifier = !String.IsNullOrEmpty(license.Identifier);
                bool hasUrl = !String.IsNullOrEmpty(license.Url);

                if (hasIdentifier && hasUrl)
                    throw new OpenApiValidationException("License 'identifier' and 'url' are mutually exclusive; set only one.");

                if (hasIdentifier && !AtLeast(OpenApiVersionEnum.V3_1))
                    throw new OpenApiValidationException("License 'identifier' requires OpenAPI 3.1 or later; the active version is " + Describe() + ".");
            }

            if (HasWebhooks() && !AtLeast(OpenApiVersionEnum.V3_1))
                throw new OpenApiValidationException("Webhooks require OpenAPI 3.1 or later; the active version is " + Describe() + ".");

            if (HasAdditionalOperations() && !AtLeast(OpenApiVersionEnum.V3_2))
                throw new OpenApiValidationException("Additional path operations (for example QUERY) require OpenAPI 3.2 or later; the active version is " + Describe() + ".");

            if (_Settings.SecuritySchemes != null)
            {
                foreach (KeyValuePair<string, OpenApiSecurityScheme> kvp in _Settings.SecuritySchemes)
                {
                    if (kvp.Value == null) continue;
                    OpenApiSecurityScheme scheme = kvp.Value;

                    if (String.Equals(scheme.Type, "oauth2", StringComparison.Ordinal) && scheme.Flows == null)
                        throw new OpenApiValidationException("OAuth2 security scheme '" + kvp.Key + "' requires flows, but none were supplied.");

                    if (String.Equals(scheme.Type, "mutualTLS", StringComparison.Ordinal) && !AtLeast(OpenApiVersionEnum.V3_1))
                        throw new OpenApiValidationException("The 'mutualTLS' security scheme requires OpenAPI 3.1 or later; the active version is " + Describe() + ".");

                    if (scheme.Flows != null && scheme.Flows.DeviceAuthorization != null && !AtLeast(OpenApiVersionEnum.V3_2))
                        throw new OpenApiValidationException("The OAuth2 device authorization flow on scheme '" + kvp.Key + "' requires OpenAPI 3.2 or later; the active version is " + Describe() + ".");

                    if (!String.IsNullOrEmpty(scheme.OAuth2MetadataUrl) && !AtLeast(OpenApiVersionEnum.V3_2))
                        throw new OpenApiValidationException("'OAuth2MetadataUrl' on scheme '" + kvp.Key + "' requires OpenAPI 3.2 or later; the active version is " + Describe() + ".");
                }
            }
        }

        #endregion

        #region Private-Methods-Info-And-TopLevel

        private Dictionary<string, object> BuildInfo()
        {
            Dictionary<string, object> info = new Dictionary<string, object>();

            if (_Settings.Info == null)
            {
                info["title"] = "API Documentation";
                info["version"] = "1.0.0";
                return info;
            }

            info["title"] = _Settings.Info.Title;
            info["version"] = _Settings.Info.Version;

            if (AtLeast(OpenApiVersionEnum.V3_1) && !String.IsNullOrEmpty(_Settings.Info.Summary))
                info["summary"] = _Settings.Info.Summary;

            if (!String.IsNullOrEmpty(_Settings.Info.Description))
                info["description"] = _Settings.Info.Description;

            if (!String.IsNullOrEmpty(_Settings.Info.TermsOfService))
                info["termsOfService"] = _Settings.Info.TermsOfService;

            if (_Settings.Info.Contact != null)
            {
                Dictionary<string, object> contact = new Dictionary<string, object>();
                if (!String.IsNullOrEmpty(_Settings.Info.Contact.Name)) contact["name"] = _Settings.Info.Contact.Name;
                if (!String.IsNullOrEmpty(_Settings.Info.Contact.Email)) contact["email"] = _Settings.Info.Contact.Email;
                if (!String.IsNullOrEmpty(_Settings.Info.Contact.Url)) contact["url"] = _Settings.Info.Contact.Url;
                if (contact.Count > 0) info["contact"] = contact;
            }

            if (_Settings.Info.License != null)
            {
                Dictionary<string, object> license = new Dictionary<string, object>();
                if (!String.IsNullOrEmpty(_Settings.Info.License.Name)) license["name"] = _Settings.Info.License.Name;

                if (AtLeast(OpenApiVersionEnum.V3_1) && !String.IsNullOrEmpty(_Settings.Info.License.Identifier))
                    license["identifier"] = _Settings.Info.License.Identifier;
                else if (!String.IsNullOrEmpty(_Settings.Info.License.Url))
                    license["url"] = _Settings.Info.License.Url;

                if (license.Count > 0) info["license"] = license;
            }

            return info;
        }

        private List<object> BuildServers()
        {
            List<object> servers = new List<object>();
            foreach (OpenApiServer server in _Settings.Servers)
            {
                if (server == null) continue;
                Dictionary<string, object> serverObj = new Dictionary<string, object>();
                serverObj["url"] = server.Url;

                if (AtLeast(OpenApiVersionEnum.V3_2) && !String.IsNullOrEmpty(server.Name))
                    serverObj["name"] = server.Name;

                if (!String.IsNullOrEmpty(server.Description))
                    serverObj["description"] = server.Description;

                if (server.Variables != null && server.Variables.Count > 0)
                    serverObj["variables"] = BuildServerVariables(server.Variables);

                servers.Add(serverObj);
            }
            return servers;
        }

        private List<object> BuildServersFromMetadata(List<OpenApiServerMetadata> metadata)
        {
            List<object> servers = new List<object>();
            foreach (OpenApiServerMetadata server in metadata)
            {
                if (server == null) continue;
                Dictionary<string, object> serverObj = new Dictionary<string, object>();
                serverObj["url"] = server.Url;

                if (AtLeast(OpenApiVersionEnum.V3_2) && !String.IsNullOrEmpty(server.Name))
                    serverObj["name"] = server.Name;

                if (!String.IsNullOrEmpty(server.Description))
                    serverObj["description"] = server.Description;

                if (server.Variables != null && server.Variables.Count > 0)
                    serverObj["variables"] = BuildServerVariables(server.Variables);

                servers.Add(serverObj);
            }
            return servers;
        }

        private Dictionary<string, object> BuildServerVariables(Dictionary<string, OpenApiServerVariableMetadata> variables)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (KeyValuePair<string, OpenApiServerVariableMetadata> kvp in variables)
            {
                if (kvp.Value == null) continue;
                Dictionary<string, object> variable = new Dictionary<string, object>();
                variable["default"] = kvp.Value.Default;
                if (!String.IsNullOrEmpty(kvp.Value.Description)) variable["description"] = kvp.Value.Description;
                if (kvp.Value.Enum != null && kvp.Value.Enum.Count > 0) variable["enum"] = kvp.Value.Enum;
                result[kvp.Key] = variable;
            }
            return result;
        }

        private List<object> BuildTags()
        {
            List<object> tags = new List<object>();
            foreach (OpenApiTag tag in _Settings.Tags)
            {
                if (tag == null) continue;
                Dictionary<string, object> tagObj = new Dictionary<string, object>();
                tagObj["name"] = tag.Name;

                if (AtLeast(OpenApiVersionEnum.V3_2) && !String.IsNullOrEmpty(tag.Summary))
                    tagObj["summary"] = tag.Summary;

                if (!String.IsNullOrEmpty(tag.Description))
                    tagObj["description"] = tag.Description;

                if (AtLeast(OpenApiVersionEnum.V3_2) && !String.IsNullOrEmpty(tag.Parent))
                    tagObj["parent"] = tag.Parent;

                if (AtLeast(OpenApiVersionEnum.V3_2) && !String.IsNullOrEmpty(tag.Kind))
                    tagObj["kind"] = tag.Kind;

                if (tag.ExternalDocs != null)
                    tagObj["externalDocs"] = BuildExternalDocs(tag.ExternalDocs);

                tags.Add(tagObj);
            }
            return tags;
        }

        private Dictionary<string, object> BuildExternalDocs(OpenApiExternalDocs externalDocs)
        {
            Dictionary<string, object> docs = new Dictionary<string, object>();
            docs["url"] = externalDocs.Url;
            if (!String.IsNullOrEmpty(externalDocs.Description)) docs["description"] = externalDocs.Description;
            return docs;
        }

        private Dictionary<string, object> BuildExternalDocsMetadata(OpenApiExternalDocsMetadata externalDocs)
        {
            Dictionary<string, object> docs = new Dictionary<string, object>();
            docs["url"] = externalDocs.Url;
            if (!String.IsNullOrEmpty(externalDocs.Description)) docs["description"] = externalDocs.Description;
            return docs;
        }

        private Dictionary<string, object> BuildWebhooks()
        {
            Dictionary<string, object> webhooks = new Dictionary<string, object>();
            foreach (KeyValuePair<string, OpenApiWebhookMetadata> kvp in _Settings.Webhooks)
            {
                if (kvp.Value == null) continue;
                string method = String.IsNullOrEmpty(kvp.Value.Method) ? "post" : kvp.Value.Method.ToLower();
                Dictionary<string, object> pathItem = new Dictionary<string, object>();
                pathItem[method] = BuildOperation(kvp.Value.Operation, method, kvp.Key, new List<string>());
                webhooks[kvp.Key] = pathItem;
            }
            return webhooks;
        }

        #endregion

        #region Private-Methods-Components

        private Dictionary<string, object> BuildComponents()
        {
            Dictionary<string, object> components = new Dictionary<string, object>();

            if (_Settings.SecuritySchemes != null && _Settings.SecuritySchemes.Count > 0)
            {
                Dictionary<string, object> schemes = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiSecurityScheme> kvp in _Settings.SecuritySchemes)
                {
                    if (kvp.Value == null) continue;
                    schemes[kvp.Key] = BuildSecurityScheme(kvp.Value);
                }
                if (schemes.Count > 0) components["securitySchemes"] = schemes;
            }

            if (_Settings.Schemas != null && _Settings.Schemas.Count > 0)
            {
                Dictionary<string, object> schemas = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiSchemaMetadata> kvp in _Settings.Schemas)
                {
                    if (kvp.Value == null) continue;
                    schemas[kvp.Key] = BuildSchema(kvp.Value);
                }
                if (schemas.Count > 0) components["schemas"] = schemas;
            }

            return components;
        }

        private Dictionary<string, object> BuildSecurityScheme(OpenApiSecurityScheme scheme)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["type"] = scheme.Type;

            if (!String.IsNullOrEmpty(scheme.Description)) result["description"] = scheme.Description;

            if (String.Equals(scheme.Type, "apiKey", StringComparison.Ordinal))
            {
                result["name"] = scheme.Name;
                result["in"] = scheme.In;
            }
            else if (String.Equals(scheme.Type, "http", StringComparison.Ordinal))
            {
                if (!String.IsNullOrEmpty(scheme.Scheme)) result["scheme"] = scheme.Scheme;
                if (!String.IsNullOrEmpty(scheme.BearerFormat)) result["bearerFormat"] = scheme.BearerFormat;
            }
            else if (String.Equals(scheme.Type, "oauth2", StringComparison.Ordinal))
            {
                if (scheme.Flows != null) result["flows"] = BuildOAuthFlows(scheme.Flows);
                if (AtLeast(OpenApiVersionEnum.V3_2) && !String.IsNullOrEmpty(scheme.OAuth2MetadataUrl))
                    result["oauth2MetadataUrl"] = scheme.OAuth2MetadataUrl;
            }
            else if (String.Equals(scheme.Type, "openIdConnect", StringComparison.Ordinal))
            {
                if (!String.IsNullOrEmpty(scheme.OpenIdConnectUrl)) result["openIdConnectUrl"] = scheme.OpenIdConnectUrl;
            }

            return result;
        }

        private Dictionary<string, object> BuildOAuthFlows(OpenApiOAuthFlows flows)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            if (flows.Implicit != null) result["implicit"] = BuildOAuthFlow(flows.Implicit);
            if (flows.Password != null) result["password"] = BuildOAuthFlow(flows.Password);
            if (flows.ClientCredentials != null) result["clientCredentials"] = BuildOAuthFlow(flows.ClientCredentials);
            if (flows.AuthorizationCode != null) result["authorizationCode"] = BuildOAuthFlow(flows.AuthorizationCode);
            if (AtLeast(OpenApiVersionEnum.V3_2) && flows.DeviceAuthorization != null)
                result["deviceAuthorization"] = BuildOAuthFlow(flows.DeviceAuthorization);
            return result;
        }

        private Dictionary<string, object> BuildOAuthFlow(OpenApiOAuthFlow flow)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            if (!String.IsNullOrEmpty(flow.AuthorizationUrl)) result["authorizationUrl"] = flow.AuthorizationUrl;
            if (!String.IsNullOrEmpty(flow.TokenUrl)) result["tokenUrl"] = flow.TokenUrl;
            if (AtLeast(OpenApiVersionEnum.V3_2) && !String.IsNullOrEmpty(flow.DeviceAuthorizationUrl))
                result["deviceAuthorizationUrl"] = flow.DeviceAuthorizationUrl;
            if (!String.IsNullOrEmpty(flow.RefreshUrl)) result["refreshUrl"] = flow.RefreshUrl;
            result["scopes"] = flow.Scopes != null ? flow.Scopes : new Dictionary<string, string>();
            return result;
        }

        #endregion

        #region Private-Methods-Paths

        private Dictionary<string, object> BuildPaths(WebserverRoutes routes)
        {
            Dictionary<string, Dictionary<string, object>> paths = new Dictionary<string, Dictionary<string, object>>();

            if (_Settings.IncludePreAuthRoutes)
                CollectFromRoutingGroup(routes.PreAuthentication, paths);

            if (_Settings.IncludePostAuthRoutes)
                CollectFromRoutingGroup(routes.PostAuthentication, paths);

            MergeAdditionalOperations(paths);

            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (KeyValuePair<string, Dictionary<string, object>> kvp in paths)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        private void CollectFromRoutingGroup(RoutingGroup group, Dictionary<string, Dictionary<string, object>> paths)
        {
            foreach (StaticRoute route in group.Static.GetAll())
            {
                string path = NormalizePath(route.Path);
                string method = route.Method.ToString().ToLower();
                EnsurePath(paths, path)[method] = BuildOperation(route.OpenApiMetadata, method, path, new List<string>());
            }

            foreach (ParameterRoute route in group.Parameter.GetAll())
            {
                string path = NormalizePath(route.Path);
                string method = route.Method.ToString().ToLower();
                List<string> pathParams = ExtractPathParameters(route.Path);
                EnsurePath(paths, path)[method] = BuildOperation(route.OpenApiMetadata, method, path, pathParams);
            }

            foreach (DynamicRoute route in group.Dynamic.GetAll())
            {
                string path;
                if (!TryConvertRegexToPath(route.Path.ToString(), out path))
                {
                    // Lossy regex-to-path conversion would emit an invalid or colliding template.
                    // Skip the route rather than corrupt the document.
                    continue;
                }

                string method = route.Method.ToString().ToLower();
                EnsurePath(paths, path)[method] = BuildOperation(route.OpenApiMetadata, method, path, ExtractPathParameters(path));
            }

            if (_Settings.IncludeContentRoutes)
            {
                foreach (ContentRoute route in group.Content.GetAll())
                {
                    string path = NormalizePath(route.Path);
                    if (route.IsDirectory && !path.EndsWith("/*")) path = path.TrimEnd('/') + "/*";

                    Dictionary<string, object> pathItem = EnsurePath(paths, path);
                    Dictionary<string, object> getOperation = BuildContentRouteOperation(route, path);
                    pathItem["get"] = getOperation;
                    pathItem["head"] = BuildContentRouteOperation(route, path);
                }
            }
        }

        private void MergeAdditionalOperations(Dictionary<string, Dictionary<string, object>> paths)
        {
            if (_Settings.AdditionalOperations == null || _Settings.AdditionalOperations.Count == 0) return;

            foreach (KeyValuePair<string, Dictionary<string, OpenApiRouteMetadata>> pathEntry in _Settings.AdditionalOperations)
            {
                if (pathEntry.Value == null || pathEntry.Value.Count == 0) continue;

                string path = NormalizePath(pathEntry.Key);
                List<string> pathParams = ExtractPathParameters(path);
                Dictionary<string, object> pathItem = EnsurePath(paths, path);

                Dictionary<string, object> additional = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiRouteMetadata> methodEntry in pathEntry.Value)
                {
                    string method = methodEntry.Key.ToUpper();
                    Dictionary<string, object> operation = BuildOperation(methodEntry.Value, method, path, pathParams);

                    if (String.Equals(method, "QUERY", StringComparison.Ordinal))
                    {
                        // In OpenAPI 3.2 QUERY is a first-class path-item field, not an additional operation.
                        pathItem["query"] = operation;
                    }
                    else
                    {
                        additional[method] = operation;
                    }
                }

                if (additional.Count > 0) pathItem["additionalOperations"] = additional;
            }
        }

        private Dictionary<string, object> EnsurePath(Dictionary<string, Dictionary<string, object>> paths, string path)
        {
            if (!paths.ContainsKey(path)) paths[path] = new Dictionary<string, object>();
            return paths[path];
        }

        private Dictionary<string, object> BuildOperation(OpenApiRouteMetadata metadata, string method, string path, List<string> pathParams)
        {
            Dictionary<string, object> operation = new Dictionary<string, object>();

            if (metadata == null)
            {
                operation["summary"] = method.ToUpper() + " " + path;
                operation["responses"] = DefaultResponses();
                List<object> autoParams = BuildAutoPathParameters(pathParams, new HashSet<string>());
                if (autoParams.Count > 0) operation["parameters"] = autoParams;
                return operation;
            }

            if (!String.IsNullOrEmpty(metadata.OperationId))
            {
                if (!_OperationIds.Add(metadata.OperationId))
                    throw new OpenApiValidationException("Duplicate operationId '" + metadata.OperationId + "'. Operation identifiers must be unique across the document.");
                operation["operationId"] = metadata.OperationId;
            }

            if (!String.IsNullOrEmpty(metadata.Summary)) operation["summary"] = metadata.Summary;
            if (!String.IsNullOrEmpty(metadata.Description)) operation["description"] = metadata.Description;
            if (metadata.Tags != null && metadata.Tags.Count > 0) operation["tags"] = metadata.Tags;
            if (metadata.Deprecated) operation["deprecated"] = true;

            List<object> parameters = new List<object>();
            HashSet<string> definedParams = new HashSet<string>(StringComparer.Ordinal);
            if (metadata.Parameters != null)
            {
                foreach (OpenApiParameterMetadata param in metadata.Parameters)
                {
                    if (param == null) continue;
                    parameters.Add(BuildParameter(param));
                    definedParams.Add(param.Name);
                }
            }

            parameters.AddRange(BuildAutoPathParameters(pathParams, definedParams));
            if (parameters.Count > 0) operation["parameters"] = parameters;

            if (metadata.RequestBody != null) operation["requestBody"] = BuildRequestBody(metadata.RequestBody);

            if (metadata.Responses != null && metadata.Responses.Count > 0)
            {
                Dictionary<string, object> responses = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiResponseMetadata> kvp in metadata.Responses)
                {
                    responses[kvp.Key] = BuildResponse(kvp.Value);
                }
                operation["responses"] = responses;
            }
            else
            {
                operation["responses"] = DefaultResponses();
            }

            if (metadata.Security != null && metadata.Security.Count > 0)
            {
                List<object> security = new List<object>();
                foreach (string scheme in metadata.Security)
                {
                    Dictionary<string, List<string>> requirement = new Dictionary<string, List<string>>();
                    requirement[scheme] = new List<string>();
                    security.Add(requirement);
                }
                operation["security"] = security;
            }

            if (metadata.Servers != null && metadata.Servers.Count > 0)
                operation["servers"] = BuildServersFromMetadata(metadata.Servers);

            if (metadata.ExternalDocs != null)
                operation["externalDocs"] = BuildExternalDocsMetadata(metadata.ExternalDocs);

            return operation;
        }

        private List<object> BuildAutoPathParameters(List<string> pathParams, HashSet<string> definedParams)
        {
            List<object> parameters = new List<object>();
            foreach (string paramName in pathParams)
            {
                if (definedParams.Contains(paramName)) continue;
                Dictionary<string, object> parameter = new Dictionary<string, object>();
                parameter["name"] = paramName;
                parameter["in"] = "path";
                parameter["required"] = true;
                parameter["schema"] = new Dictionary<string, object> { ["type"] = "string" };
                parameters.Add(parameter);
            }
            return parameters;
        }

        private Dictionary<string, object> DefaultResponses()
        {
            Dictionary<string, object> responses = new Dictionary<string, object>();
            responses["200"] = new Dictionary<string, object> { ["description"] = "Successful response" };
            return responses;
        }

        private Dictionary<string, object> BuildContentRouteOperation(ContentRoute route, string path)
        {
            Dictionary<string, object> schema = new Dictionary<string, object>();
            schema["type"] = "string";
            if (AtLeast(OpenApiVersionEnum.V3_1)) schema["contentMediaType"] = "application/octet-stream";
            else schema["format"] = "binary";

            Dictionary<string, object> okContent = new Dictionary<string, object>
            {
                ["application/octet-stream"] = new Dictionary<string, object> { ["schema"] = schema }
            };

            Dictionary<string, object> operation = new Dictionary<string, object>();
            operation["summary"] = route.IsDirectory ? "Serve files from " + path : "Serve file at " + path;
            operation["responses"] = new Dictionary<string, object>
            {
                ["200"] = new Dictionary<string, object> { ["description"] = "File content", ["content"] = okContent },
                ["404"] = new Dictionary<string, object> { ["description"] = "File not found" }
            };

            if (route.OpenApiMetadata != null)
            {
                if (!String.IsNullOrEmpty(route.OpenApiMetadata.Summary)) operation["summary"] = route.OpenApiMetadata.Summary;
                if (!String.IsNullOrEmpty(route.OpenApiMetadata.Description)) operation["description"] = route.OpenApiMetadata.Description;
                if (route.OpenApiMetadata.Tags != null && route.OpenApiMetadata.Tags.Count > 0) operation["tags"] = route.OpenApiMetadata.Tags;
            }

            return operation;
        }

        private Dictionary<string, object> BuildParameter(OpenApiParameterMetadata param)
        {
            Dictionary<string, object> parameter = new Dictionary<string, object>();
            parameter["name"] = param.Name;
            parameter["in"] = param.In.ToString().ToLower();

            if (!String.IsNullOrEmpty(param.Description)) parameter["description"] = param.Description;
            if (param.Required || param.In == ParameterLocation.Path) parameter["required"] = true;
            if (param.Deprecated) parameter["deprecated"] = true;
            if (param.AllowEmptyValue && param.In == ParameterLocation.Query) parameter["allowEmptyValue"] = true;

            parameter["schema"] = param.Schema != null ? BuildSchema(param.Schema) : new Dictionary<string, object> { ["type"] = "string" };

            if (param.Example != null) parameter["example"] = param.Example;

            return parameter;
        }

        private Dictionary<string, object> BuildRequestBody(OpenApiRequestBodyMetadata requestBody)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            if (!String.IsNullOrEmpty(requestBody.Description)) body["description"] = requestBody.Description;
            if (requestBody.Required) body["required"] = true;

            if (requestBody.Content != null && requestBody.Content.Count > 0)
                body["content"] = BuildContent(requestBody.Content);

            return body;
        }

        private Dictionary<string, object> BuildResponse(OpenApiResponseMetadata response)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["description"] = response.Description != null ? response.Description : "Response";

            if (response.Content != null && response.Content.Count > 0)
                result["content"] = BuildContent(response.Content);

            if (response.Headers != null && response.Headers.Count > 0)
            {
                Dictionary<string, object> headers = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiHeaderMetadata> kvp in response.Headers)
                {
                    if (kvp.Value == null) continue;
                    Dictionary<string, object> header = new Dictionary<string, object>();
                    if (!String.IsNullOrEmpty(kvp.Value.Description)) header["description"] = kvp.Value.Description;
                    if (kvp.Value.Required) header["required"] = true;
                    if (kvp.Value.Deprecated) header["deprecated"] = true;
                    if (kvp.Value.Schema != null) header["schema"] = BuildSchema(kvp.Value.Schema);
                    headers[kvp.Key] = header;
                }
                result["headers"] = headers;
            }

            return result;
        }

        private Dictionary<string, object> BuildContent(Dictionary<string, OpenApiMediaTypeMetadata> content)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (KeyValuePair<string, OpenApiMediaTypeMetadata> kvp in content)
            {
                if (kvp.Value == null) continue;
                Dictionary<string, object> mediaType = new Dictionary<string, object>();
                if (kvp.Value.Schema != null) mediaType["schema"] = BuildSchema(kvp.Value.Schema);
                if (kvp.Value.Example != null) mediaType["example"] = kvp.Value.Example;
                if (kvp.Value.Examples != null && kvp.Value.Examples.Count > 0) mediaType["examples"] = BuildExamples(kvp.Value.Examples);
                if (AtLeast(OpenApiVersionEnum.V3_2) && kvp.Value.ItemSchema != null) mediaType["itemSchema"] = BuildSchema(kvp.Value.ItemSchema);
                result[kvp.Key] = mediaType;
            }
            return result;
        }

        private Dictionary<string, object> BuildExamples(Dictionary<string, OpenApiExampleMetadata> examples)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (KeyValuePair<string, OpenApiExampleMetadata> kvp in examples)
            {
                if (kvp.Value == null) continue;
                Dictionary<string, object> example = new Dictionary<string, object>();
                if (!String.IsNullOrEmpty(kvp.Value.Summary)) example["summary"] = kvp.Value.Summary;
                if (!String.IsNullOrEmpty(kvp.Value.Description)) example["description"] = kvp.Value.Description;
                if (kvp.Value.Value != null) example["value"] = kvp.Value.Value;
                if (!String.IsNullOrEmpty(kvp.Value.ExternalValue)) example["externalValue"] = kvp.Value.ExternalValue;
                result[kvp.Key] = example;
            }
            return result;
        }

        #endregion

        #region Private-Methods-Schema

        private Dictionary<string, object> BuildSchema(OpenApiSchemaMetadata schema)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            if (schema == null) return result;

            if (!String.IsNullOrEmpty(schema.Ref))
            {
                return BuildRefSchema(schema);
            }

            EmitTypes(result, schema);
            EmitFormatAndContent(result, schema);

            if (!String.IsNullOrEmpty(schema.Summary) && AtLeast(OpenApiVersionEnum.V3_1)) result["summary"] = schema.Summary;
            if (!String.IsNullOrEmpty(schema.Description)) result["description"] = schema.Description;

            if (schema.Items != null) result["items"] = BuildSchema(schema.Items);

            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                Dictionary<string, object> props = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiSchemaMetadata> kvp in schema.Properties)
                {
                    props[kvp.Key] = BuildSchema(kvp.Value);
                }
                result["properties"] = props;
            }

            if (schema.Required != null && schema.Required.Count > 0) result["required"] = schema.Required;

            EmitExample(result, schema);

            if (schema.Default != null) result["default"] = schema.Default;
            if (schema.Enum != null && schema.Enum.Count > 0) result["enum"] = schema.Enum;

            EmitNumericBounds(result, schema);

            if (schema.MinLength.HasValue) result["minLength"] = schema.MinLength.Value;
            if (schema.MaxLength.HasValue) result["maxLength"] = schema.MaxLength.Value;
            if (!String.IsNullOrEmpty(schema.Pattern)) result["pattern"] = schema.Pattern;

            if (schema.OneOf != null && schema.OneOf.Count > 0)
            {
                List<object> branches = new List<object>();
                foreach (OpenApiSchemaMetadata branch in schema.OneOf)
                {
                    if (branch == null) continue;
                    branches.Add(BuildSchema(branch));
                }
                if (branches.Count > 0) result["oneOf"] = branches;
            }

            if (schema.Discriminator != null && !String.IsNullOrEmpty(schema.Discriminator.PropertyName))
            {
                Dictionary<string, object> discriminator = new Dictionary<string, object>();
                discriminator["propertyName"] = schema.Discriminator.PropertyName;
                if (schema.Discriminator.Mapping != null && schema.Discriminator.Mapping.Count > 0)
                    discriminator["mapping"] = schema.Discriminator.Mapping;
                result["discriminator"] = discriminator;
            }

            return result;
        }

        private Dictionary<string, object> BuildRefSchema(OpenApiSchemaMetadata schema)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!AtLeast(OpenApiVersionEnum.V3_1))
            {
                // OpenAPI 3.0: a $ref is a pure reference; sibling keywords are ignored.
                result["$ref"] = schema.Ref;
                return result;
            }

            if (schema.Nullable)
            {
                List<object> anyOf = new List<object>();
                anyOf.Add(new Dictionary<string, object> { ["$ref"] = schema.Ref });
                anyOf.Add(new Dictionary<string, object> { ["type"] = "null" });
                result["anyOf"] = anyOf;
            }
            else
            {
                result["$ref"] = schema.Ref;
            }

            if (!String.IsNullOrEmpty(schema.Summary)) result["summary"] = schema.Summary;
            if (!String.IsNullOrEmpty(schema.Description)) result["description"] = schema.Description;

            return result;
        }

        private void EmitTypes(Dictionary<string, object> result, OpenApiSchemaMetadata schema)
        {
            List<string> types = new List<string>();

            if (schema.Types != null && schema.Types.Count > 0)
            {
                if (!AtLeast(OpenApiVersionEnum.V3_1))
                    throw new OpenApiValidationException("Multi-valued schema 'type' arrays require OpenAPI 3.1 or later; the active version is " + Describe() + ".");

                foreach (string type in schema.Types)
                {
                    if (!String.IsNullOrEmpty(type)) types.Add(type);
                }
            }
            else if (!String.IsNullOrEmpty(schema.Type))
            {
                types.Add(schema.Type);
            }

            if (schema.Nullable && AtLeast(OpenApiVersionEnum.V3_1))
            {
                if (types.Count == 0)
                    throw new OpenApiValidationException("A nullable schema requires a base 'type' (or 'types') when targeting OpenAPI 3.1 or later.");
                if (!types.Contains("null")) types.Add("null");
            }

            if (types.Count == 1)
            {
                result["type"] = types[0];
            }
            else if (types.Count > 1)
            {
                List<object> typeArray = new List<object>();
                foreach (string type in types) typeArray.Add(type);
                result["type"] = typeArray;
            }

            if (schema.Nullable && !AtLeast(OpenApiVersionEnum.V3_1))
            {
                result["nullable"] = true;
            }
        }

        private void EmitFormatAndContent(Dictionary<string, object> result, OpenApiSchemaMetadata schema)
        {
            string format = schema.Format;

            if (AtLeast(OpenApiVersionEnum.V3_1) && !String.IsNullOrEmpty(format))
            {
                if (String.Equals(format, "binary", StringComparison.Ordinal))
                {
                    result["contentMediaType"] = String.IsNullOrEmpty(schema.ContentMediaType) ? "application/octet-stream" : schema.ContentMediaType;
                    if (!String.IsNullOrEmpty(schema.ContentEncoding)) result["contentEncoding"] = schema.ContentEncoding;
                    format = null;
                }
                else if (String.Equals(format, "byte", StringComparison.Ordinal))
                {
                    result["contentEncoding"] = String.IsNullOrEmpty(schema.ContentEncoding) ? "base64" : schema.ContentEncoding;
                    if (!String.IsNullOrEmpty(schema.ContentMediaType)) result["contentMediaType"] = schema.ContentMediaType;
                    format = null;
                }
            }

            if (!String.IsNullOrEmpty(format)) result["format"] = format;

            if (AtLeast(OpenApiVersionEnum.V3_1))
            {
                if (!result.ContainsKey("contentMediaType") && !String.IsNullOrEmpty(schema.ContentMediaType))
                    result["contentMediaType"] = schema.ContentMediaType;
                if (!result.ContainsKey("contentEncoding") && !String.IsNullOrEmpty(schema.ContentEncoding))
                    result["contentEncoding"] = schema.ContentEncoding;
            }
        }

        private void EmitExample(Dictionary<string, object> result, OpenApiSchemaMetadata schema)
        {
            if (schema.Example == null) return;

            if (AtLeast(OpenApiVersionEnum.V3_1))
            {
                result["examples"] = new List<object> { schema.Example };
            }
            else
            {
                result["example"] = schema.Example;
            }
        }

        private void EmitNumericBounds(Dictionary<string, object> result, OpenApiSchemaMetadata schema)
        {
            if (schema.Minimum.HasValue) result["minimum"] = schema.Minimum.Value;
            if (schema.Maximum.HasValue) result["maximum"] = schema.Maximum.Value;

            if (schema.ExclusiveMinimum.HasValue)
            {
                if (AtLeast(OpenApiVersionEnum.V3_1))
                {
                    result["exclusiveMinimum"] = schema.ExclusiveMinimum.Value;
                }
                else
                {
                    if (!result.ContainsKey("minimum")) result["minimum"] = schema.ExclusiveMinimum.Value;
                    result["exclusiveMinimum"] = true;
                }
            }

            if (schema.ExclusiveMaximum.HasValue)
            {
                if (AtLeast(OpenApiVersionEnum.V3_1))
                {
                    result["exclusiveMaximum"] = schema.ExclusiveMaximum.Value;
                }
                else
                {
                    if (!result.ContainsKey("maximum")) result["maximum"] = schema.ExclusiveMaximum.Value;
                    result["exclusiveMaximum"] = true;
                }
            }
        }

        #endregion

        #region Private-Methods-Helpers

        private bool AtLeast(OpenApiVersionEnum version)
        {
            return (int)_Version >= (int)version;
        }

        private string ResolveVersionString()
        {
            if (!String.IsNullOrEmpty(_Settings.VersionString)) return _Settings.VersionString;

            switch (_Version)
            {
                case OpenApiVersionEnum.V3_1:
                    return "3.1.1";
                case OpenApiVersionEnum.V3_2:
                    return "3.2.0";
                default:
                    return "3.0.3";
            }
        }

        private string Describe()
        {
            switch (_Version)
            {
                case OpenApiVersionEnum.V3_1:
                    return "OpenAPI 3.1";
                case OpenApiVersionEnum.V3_2:
                    return "OpenAPI 3.2";
                default:
                    return "OpenAPI 3.0";
            }
        }

        private bool HasWebhooks()
        {
            return _Settings.Webhooks != null && _Settings.Webhooks.Count > 0;
        }

        private bool HasAdditionalOperations()
        {
            if (_Settings.AdditionalOperations == null || _Settings.AdditionalOperations.Count == 0) return false;

            foreach (KeyValuePair<string, Dictionary<string, OpenApiRouteMetadata>> kvp in _Settings.AdditionalOperations)
            {
                if (kvp.Value != null && kvp.Value.Count > 0) return true;
            }
            return false;
        }

        private string NormalizePath(string path)
        {
            if (String.IsNullOrEmpty(path)) return "/";
            if (path.Length > 1 && path.EndsWith("/")) path = path.TrimEnd('/');
            if (!path.StartsWith("/")) path = "/" + path;
            return path;
        }

        private List<string> ExtractPathParameters(string path)
        {
            List<string> parameters = new List<string>();
            MatchCollection matches = _ParameterRegex.Matches(path);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1) parameters.Add(match.Groups[1].Value);
            }
            return parameters;
        }

        private bool TryConvertRegexToPath(string regexPattern, out string path)
        {
            path = null;
            if (String.IsNullOrEmpty(regexPattern)) return false;

            string candidate = regexPattern.Replace("^", "").Replace("$", "");
            candidate = Regex.Replace(candidate, @"\(\?<(\w+)>[^)]+\)", "{$1}");
            candidate = Regex.Replace(candidate, @"\\.", ".");

            if (!candidate.StartsWith("/")) candidate = "/" + candidate;

            // Only accept the conversion when the result is a clean, unambiguous path template.
            // Anything still carrying regex metacharacters is skipped rather than emitted as a
            // broken or colliding path.
            if (!_CleanPathRegex.IsMatch(candidate)) return false;

            path = candidate;
            return true;
        }

        #endregion
    }
}
