namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Generates OpenAPI 3.0 specification documents from WatsonWebserver routes.
    /// </summary>
    public class OpenApiDocumentGenerator
    {
        #region Public-Members

        /// <summary>
        /// JSON serializer options used for generating the OpenAPI document.
        /// </summary>
        public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region Private-Members

        private static readonly Regex _ParameterRegex = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiDocumentGenerator()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate an OpenAPI JSON document from the webserver routes.
        /// </summary>
        /// <param name="routes">The webserver routes.</param>
        /// <param name="settings">OpenAPI settings.</param>
        /// <returns>OpenAPI JSON string.</returns>
        public string Generate(WebserverRoutes routes, OpenApiSettings settings)
        {
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Dictionary<string, object> document = new Dictionary<string, object>
            {
                ["openapi"] = "3.0.3",
                ["info"] = BuildInfo(settings),
                ["paths"] = BuildPaths(routes, settings)
            };

            if (settings.Servers != null && settings.Servers.Count > 0)
            {
                document["servers"] = BuildServers(settings);
            }

            if (settings.Tags != null && settings.Tags.Count > 0)
            {
                document["tags"] = BuildTags(settings);
            }

            Dictionary<string, object> components = BuildComponents(settings);
            if (components.Count > 0)
            {
                document["components"] = components;
            }

            if (settings.Security != null && settings.Security.Count > 0)
            {
                document["security"] = settings.Security;
            }

            if (settings.ExternalDocs != null)
            {
                document["externalDocs"] = BuildExternalDocs(settings.ExternalDocs);
            }

            return JsonSerializer.Serialize(document, SerializerOptions);
        }

        #endregion

        #region Private-Methods

        private Dictionary<string, object> BuildInfo(OpenApiSettings settings)
        {
            Dictionary<string, object> info = new Dictionary<string, object>
            {
                ["title"] = settings.Info.Title,
                ["version"] = settings.Info.Version
            };

            if (!String.IsNullOrEmpty(settings.Info.Description))
                info["description"] = settings.Info.Description;

            if (!String.IsNullOrEmpty(settings.Info.TermsOfService))
                info["termsOfService"] = settings.Info.TermsOfService;

            if (settings.Info.Contact != null)
            {
                Dictionary<string, object> contact = new Dictionary<string, object>();
                if (!String.IsNullOrEmpty(settings.Info.Contact.Name))
                    contact["name"] = settings.Info.Contact.Name;
                if (!String.IsNullOrEmpty(settings.Info.Contact.Email))
                    contact["email"] = settings.Info.Contact.Email;
                if (!String.IsNullOrEmpty(settings.Info.Contact.Url))
                    contact["url"] = settings.Info.Contact.Url;
                if (contact.Count > 0)
                    info["contact"] = contact;
            }

            if (settings.Info.License != null)
            {
                Dictionary<string, object> license = new Dictionary<string, object>();
                if (!String.IsNullOrEmpty(settings.Info.License.Name))
                    license["name"] = settings.Info.License.Name;
                if (!String.IsNullOrEmpty(settings.Info.License.Url))
                    license["url"] = settings.Info.License.Url;
                if (license.Count > 0)
                    info["license"] = license;
            }

            return info;
        }

        private List<object> BuildServers(OpenApiSettings settings)
        {
            List<object> servers = new List<object>();
            foreach (OpenApiServer server in settings.Servers)
            {
                Dictionary<string, object> serverObj = new Dictionary<string, object>
                {
                    ["url"] = server.Url
                };
                if (!String.IsNullOrEmpty(server.Description))
                    serverObj["description"] = server.Description;
                servers.Add(serverObj);
            }
            return servers;
        }

        private List<object> BuildTags(OpenApiSettings settings)
        {
            List<object> tags = new List<object>();
            foreach (OpenApiTag tag in settings.Tags)
            {
                Dictionary<string, object> tagObj = new Dictionary<string, object>
                {
                    ["name"] = tag.Name
                };
                if (!String.IsNullOrEmpty(tag.Description))
                    tagObj["description"] = tag.Description;
                if (tag.ExternalDocs != null)
                    tagObj["externalDocs"] = BuildExternalDocs(tag.ExternalDocs);
                tags.Add(tagObj);
            }
            return tags;
        }

        private Dictionary<string, object> BuildExternalDocs(OpenApiExternalDocs externalDocs)
        {
            Dictionary<string, object> docs = new Dictionary<string, object>
            {
                ["url"] = externalDocs.Url
            };
            if (!String.IsNullOrEmpty(externalDocs.Description))
                docs["description"] = externalDocs.Description;
            return docs;
        }

        private Dictionary<string, object> BuildComponents(OpenApiSettings settings)
        {
            Dictionary<string, object> components = new Dictionary<string, object>();

            if (settings.SecuritySchemes != null && settings.SecuritySchemes.Count > 0)
            {
                Dictionary<string, object> schemes = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiSecurityScheme> kvp in settings.SecuritySchemes)
                {
                    Dictionary<string, object> scheme = new Dictionary<string, object>
                    {
                        ["type"] = kvp.Value.Type
                    };

                    if (!String.IsNullOrEmpty(kvp.Value.Description))
                        scheme["description"] = kvp.Value.Description;

                    if (kvp.Value.Type == "apiKey")
                    {
                        scheme["name"] = kvp.Value.Name;
                        scheme["in"] = kvp.Value.In;
                    }
                    else if (kvp.Value.Type == "http")
                    {
                        if (!String.IsNullOrEmpty(kvp.Value.Scheme))
                            scheme["scheme"] = kvp.Value.Scheme;
                        if (!String.IsNullOrEmpty(kvp.Value.BearerFormat))
                            scheme["bearerFormat"] = kvp.Value.BearerFormat;
                    }

                    schemes[kvp.Key] = scheme;
                }
                components["securitySchemes"] = schemes;
            }

            return components;
        }

        private Dictionary<string, object> BuildPaths(WebserverRoutes routes, OpenApiSettings settings)
        {
            Dictionary<string, Dictionary<string, object>> paths = new Dictionary<string, Dictionary<string, object>>();

            if (settings.IncludePreAuthRoutes)
            {
                CollectFromRoutingGroup(routes.PreAuthentication, paths, false, settings);
            }

            if (settings.IncludePostAuthRoutes)
            {
                CollectFromRoutingGroup(routes.PostAuthentication, paths, true, settings);
            }

            return paths.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
        }

        private void CollectFromRoutingGroup(RoutingGroup group, Dictionary<string, Dictionary<string, object>> paths, bool requiresAuth, OpenApiSettings settings)
        {
            // Collect static routes
            foreach (StaticRoute route in group.Static.GetAll())
            {
                string path = NormalizePath(route.Path);
                string method = route.Method.ToString().ToLower();

                if (!paths.ContainsKey(path))
                    paths[path] = new Dictionary<string, object>();

                paths[path][method] = BuildOperation(route.OpenApiMetadata, method, path, requiresAuth, new List<string>());
            }

            // Collect parameter routes
            foreach (ParameterRoute route in group.Parameter.GetAll())
            {
                string path = NormalizePath(route.Path);
                string method = route.Method.ToString().ToLower();

                List<string> pathParams = ExtractPathParameters(route.Path);

                if (!paths.ContainsKey(path))
                    paths[path] = new Dictionary<string, object>();

                paths[path][method] = BuildOperation(route.OpenApiMetadata, method, path, requiresAuth, pathParams);
            }

            // Collect dynamic routes (convert regex to approximate path if possible)
            foreach (DynamicRoute route in group.Dynamic.GetAll())
            {
                string regexStr = route.Path.ToString();
                string path = ConvertRegexToPath(regexStr);
                string method = route.Method.ToString().ToLower();

                if (!paths.ContainsKey(path))
                    paths[path] = new Dictionary<string, object>();

                paths[path][method] = BuildOperation(route.OpenApiMetadata, method, path, requiresAuth, new List<string>());
            }

            // Collect content routes if enabled
            if (settings.IncludeContentRoutes)
            {
                foreach (ContentRoute route in group.Content.GetAll())
                {
                    string path = NormalizePath(route.Path);
                    if (route.IsDirectory && !path.EndsWith("/*"))
                        path = path.TrimEnd('/') + "/*";

                    if (!paths.ContainsKey(path))
                        paths[path] = new Dictionary<string, object>();

                    // Content routes support GET and HEAD
                    paths[path]["get"] = BuildContentRouteOperation(route, path);
                }
            }
        }

        private Dictionary<string, object> BuildOperation(OpenApiRouteMetadata metadata, string method, string path, bool requiresAuth, List<string> pathParams)
        {
            Dictionary<string, object> operation = new Dictionary<string, object>();

            if (metadata != null)
            {
                if (!String.IsNullOrEmpty(metadata.OperationId))
                    operation["operationId"] = metadata.OperationId;

                if (!String.IsNullOrEmpty(metadata.Summary))
                    operation["summary"] = metadata.Summary;

                if (!String.IsNullOrEmpty(metadata.Description))
                    operation["description"] = metadata.Description;

                if (metadata.Tags != null && metadata.Tags.Count > 0)
                    operation["tags"] = metadata.Tags;

                if (metadata.Deprecated)
                    operation["deprecated"] = true;

                // Build parameters
                List<object> parameters = new List<object>();

                // Add path parameters that are not already defined
                HashSet<string> definedParams = new HashSet<string>();
                if (metadata.Parameters != null)
                {
                    foreach (OpenApiParameterMetadata param in metadata.Parameters)
                    {
                        parameters.Add(BuildParameter(param));
                        definedParams.Add(param.Name);
                    }
                }

                // Auto-add missing path parameters
                foreach (string paramName in pathParams)
                {
                    if (!definedParams.Contains(paramName))
                    {
                        parameters.Add(new Dictionary<string, object>
                        {
                            ["name"] = paramName,
                            ["in"] = "path",
                            ["required"] = true,
                            ["schema"] = new Dictionary<string, object> { ["type"] = "string" }
                        });
                    }
                }

                if (parameters.Count > 0)
                    operation["parameters"] = parameters;

                // Build request body
                if (metadata.RequestBody != null)
                {
                    operation["requestBody"] = BuildRequestBody(metadata.RequestBody);
                }

                // Build responses
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
                    // Default response
                    operation["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Successful response" }
                    };
                }

                // Security
                if (metadata.Security != null && metadata.Security.Count > 0)
                {
                    List<object> security = new List<object>();
                    foreach (string scheme in metadata.Security)
                    {
                        security.Add(new Dictionary<string, List<string>> { [scheme] = new List<string>() });
                    }
                    operation["security"] = security;
                }
            }
            else
            {
                // Generate minimal documentation for undocumented routes
                operation["summary"] = $"{method.ToUpper()} {path}";
                operation["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object> { ["description"] = "Successful response" }
                };

                // Auto-add path parameters
                if (pathParams.Count > 0)
                {
                    List<object> parameters = new List<object>();
                    foreach (string paramName in pathParams)
                    {
                        parameters.Add(new Dictionary<string, object>
                        {
                            ["name"] = paramName,
                            ["in"] = "path",
                            ["required"] = true,
                            ["schema"] = new Dictionary<string, object> { ["type"] = "string" }
                        });
                    }
                    operation["parameters"] = parameters;
                }
            }

            return operation;
        }

        private Dictionary<string, object> BuildContentRouteOperation(ContentRoute route, string path)
        {
            Dictionary<string, object> operation = new Dictionary<string, object>
            {
                ["summary"] = route.IsDirectory ? $"Serve files from {path}" : $"Serve file at {path}",
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "File content",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/octet-stream"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object>
                                {
                                    ["type"] = "string",
                                    ["format"] = "binary"
                                }
                            }
                        }
                    },
                    ["404"] = new Dictionary<string, object>
                    {
                        ["description"] = "File not found"
                    }
                }
            };

            if (route.OpenApiMetadata != null)
            {
                if (!String.IsNullOrEmpty(route.OpenApiMetadata.Summary))
                    operation["summary"] = route.OpenApiMetadata.Summary;
                if (!String.IsNullOrEmpty(route.OpenApiMetadata.Description))
                    operation["description"] = route.OpenApiMetadata.Description;
                if (route.OpenApiMetadata.Tags != null && route.OpenApiMetadata.Tags.Count > 0)
                    operation["tags"] = route.OpenApiMetadata.Tags;
            }

            return operation;
        }

        private Dictionary<string, object> BuildParameter(OpenApiParameterMetadata param)
        {
            Dictionary<string, object> parameter = new Dictionary<string, object>
            {
                ["name"] = param.Name,
                ["in"] = param.In.ToString().ToLower()
            };

            if (!String.IsNullOrEmpty(param.Description))
                parameter["description"] = param.Description;

            if (param.Required || param.In == ParameterLocation.Path)
                parameter["required"] = true;

            if (param.Deprecated)
                parameter["deprecated"] = true;

            if (param.Schema != null)
            {
                parameter["schema"] = BuildSchema(param.Schema);
            }
            else
            {
                parameter["schema"] = new Dictionary<string, object> { ["type"] = "string" };
            }

            if (param.Example != null)
                parameter["example"] = param.Example;

            return parameter;
        }

        private Dictionary<string, object> BuildRequestBody(OpenApiRequestBodyMetadata requestBody)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();

            if (!String.IsNullOrEmpty(requestBody.Description))
                body["description"] = requestBody.Description;

            if (requestBody.Required)
                body["required"] = true;

            if (requestBody.Content != null && requestBody.Content.Count > 0)
            {
                Dictionary<string, object> content = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiMediaTypeMetadata> kvp in requestBody.Content)
                {
                    Dictionary<string, object> mediaType = new Dictionary<string, object>();
                    if (kvp.Value.Schema != null)
                        mediaType["schema"] = BuildSchema(kvp.Value.Schema);
                    if (kvp.Value.Example != null)
                        mediaType["example"] = kvp.Value.Example;
                    content[kvp.Key] = mediaType;
                }
                body["content"] = content;
            }

            return body;
        }

        private Dictionary<string, object> BuildResponse(OpenApiResponseMetadata response)
        {
            Dictionary<string, object> resp = new Dictionary<string, object>
            {
                ["description"] = response.Description ?? "Response"
            };

            if (response.Content != null && response.Content.Count > 0)
            {
                Dictionary<string, object> content = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiMediaTypeMetadata> kvp in response.Content)
                {
                    Dictionary<string, object> mediaType = new Dictionary<string, object>();
                    if (kvp.Value.Schema != null)
                        mediaType["schema"] = BuildSchema(kvp.Value.Schema);
                    if (kvp.Value.Example != null)
                        mediaType["example"] = kvp.Value.Example;
                    content[kvp.Key] = mediaType;
                }
                resp["content"] = content;
            }

            if (response.Headers != null && response.Headers.Count > 0)
            {
                Dictionary<string, object> headers = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiHeaderMetadata> kvp in response.Headers)
                {
                    Dictionary<string, object> header = new Dictionary<string, object>();
                    if (!String.IsNullOrEmpty(kvp.Value.Description))
                        header["description"] = kvp.Value.Description;
                    if (kvp.Value.Schema != null)
                        header["schema"] = BuildSchema(kvp.Value.Schema);
                    headers[kvp.Key] = header;
                }
                resp["headers"] = headers;
            }

            return resp;
        }

        private Dictionary<string, object> BuildSchema(OpenApiSchemaMetadata schema)
        {
            Dictionary<string, object> schemaObj = new Dictionary<string, object>();

            if (!String.IsNullOrEmpty(schema.Ref))
            {
                schemaObj["$ref"] = schema.Ref;
                return schemaObj;
            }

            if (!String.IsNullOrEmpty(schema.Type))
                schemaObj["type"] = schema.Type;

            if (!String.IsNullOrEmpty(schema.Format))
                schemaObj["format"] = schema.Format;

            if (!String.IsNullOrEmpty(schema.Description))
                schemaObj["description"] = schema.Description;

            if (schema.Nullable)
                schemaObj["nullable"] = true;

            if (schema.Items != null)
                schemaObj["items"] = BuildSchema(schema.Items);

            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                Dictionary<string, object> props = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiSchemaMetadata> kvp in schema.Properties)
                {
                    props[kvp.Key] = BuildSchema(kvp.Value);
                }
                schemaObj["properties"] = props;
            }

            if (schema.Required != null && schema.Required.Count > 0)
                schemaObj["required"] = schema.Required;

            if (schema.Example != null)
                schemaObj["example"] = schema.Example;

            if (schema.Default != null)
                schemaObj["default"] = schema.Default;

            if (schema.Enum != null && schema.Enum.Count > 0)
                schemaObj["enum"] = schema.Enum;

            if (schema.Minimum.HasValue)
                schemaObj["minimum"] = schema.Minimum.Value;

            if (schema.Maximum.HasValue)
                schemaObj["maximum"] = schema.Maximum.Value;

            if (schema.MinLength.HasValue)
                schemaObj["minLength"] = schema.MinLength.Value;

            if (schema.MaxLength.HasValue)
                schemaObj["maxLength"] = schema.MaxLength.Value;

            if (!String.IsNullOrEmpty(schema.Pattern))
                schemaObj["pattern"] = schema.Pattern;

            return schemaObj;
        }

        private string NormalizePath(string path)
        {
            if (String.IsNullOrEmpty(path)) return "/";

            // Remove trailing slash for OpenAPI (except root)
            if (path.Length > 1 && path.EndsWith("/"))
                path = path.TrimEnd('/');

            // Ensure leading slash
            if (!path.StartsWith("/"))
                path = "/" + path;

            return path;
        }

        private List<string> ExtractPathParameters(string path)
        {
            List<string> parameters = new List<string>();
            MatchCollection matches = _ParameterRegex.Matches(path);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                    parameters.Add(match.Groups[1].Value);
            }
            return parameters;
        }

        private string ConvertRegexToPath(string regexPattern)
        {
            // Remove common regex anchors
            string path = regexPattern
                .Replace("^", "")
                .Replace("$", "");

            // Convert common patterns to OpenAPI path format
            path = Regex.Replace(path, @"\(\?<(\w+)>[^)]+\)", "{$1}");
            path = Regex.Replace(path, @"\([^)]+\)", "{param}");
            path = Regex.Replace(path, @"\\d\+", "{id}");
            path = Regex.Replace(path, @"\.\*", "*");
            path = Regex.Replace(path, @"\\.", ".");

            // Ensure leading slash
            if (!path.StartsWith("/"))
                path = "/" + path;

            return path;
        }

        #endregion
    }
}
