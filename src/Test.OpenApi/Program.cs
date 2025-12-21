namespace Test.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using WatsonWebserver.Lite;

    /// <summary>
    /// Test application demonstrating OpenAPI/Swagger support in WatsonWebserver.
    /// </summary>
    public static class Program
    {
        private static bool _UsingLite = false;
        private static string _Hostname = "localhost";
        private static int _Port = 8080;
        private static WebserverBase _Server = null;

        // JSON serializer options - case-insensitive for compatibility with Swagger UI
        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Sample data
        private static List<User> _Users = new List<User>
        {
            new User { Id = 1, Name = "Alice Johnson", Email = "alice@example.com", Active = true },
            new User { Id = 2, Name = "Bob Smith", Email = "bob@example.com", Active = true },
            new User { Id = 3, Name = "Charlie Brown", Email = "charlie@example.com", Active = false }
        };

        private static List<Product> _Products = new List<Product>
        {
            new Product { Id = 1, Name = "Widget", Price = 9.99m, Category = "Tools" },
            new Product { Id = 2, Name = "Gadget", Price = 24.99m, Category = "Electronics" },
            new Product { Id = 3, Name = "Gizmo", Price = 14.99m, Category = "Electronics" }
        };

        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                foreach (string arg in args)
                {
                    if (arg.Equals("-lite", StringComparison.OrdinalIgnoreCase))
                    {
                        _UsingLite = true;
                        break;
                    }
                }
            }

            Console.WriteLine("WatsonWebserver OpenAPI Test Application");
            Console.WriteLine("=========================================");
            Console.WriteLine();

            // Create webserver settings
            WebserverSettings settings = new WebserverSettings(_Hostname, _Port, false);

            // Create webserver with default route
            if (_UsingLite)
            {
                Console.WriteLine("Using WatsonWebserver.Lite");
                _Server = new WebserverLite(settings, DefaultRoute);
            }
            else
            {
                Console.WriteLine("Using WatsonWebserver");
                _Server = new Webserver(settings, DefaultRoute);
            }

            Console.WriteLine();

            // Configure OpenAPI
            _Server.UseOpenApi(openApi =>
            {
                openApi.Info.Title = "Sample Pet Store API";
                openApi.Info.Version = "1.0.0";
                openApi.Info.Description = "A sample API demonstrating WatsonWebserver OpenAPI support. " +
                    "This API provides endpoints for managing users and products.";
                openApi.Info.Contact = new OpenApiContact
                {
                    Name = "API Support",
                    Email = "support@example.com",
                    Url = "https://example.com/support"
                };
                openApi.Info.License = new OpenApiLicense
                {
                    Name = "MIT",
                    Url = "https://opensource.org/licenses/MIT"
                };

                // Add tags for grouping
                openApi.Tags.Add(new OpenApiTag { Name = "Users", Description = "User management operations" });
                openApi.Tags.Add(new OpenApiTag { Name = "Products", Description = "Product catalog operations" });
                openApi.Tags.Add(new OpenApiTag { Name = "System", Description = "System health and info" });

                // Add security scheme
                openApi.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
                {
                    Type = "apiKey",
                    Name = "X-API-Key",
                    In = "header",
                    Description = "API key for authorization"
                };
            });

            // Add documented routes

            // Health check endpoint
            _Server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/health",
                HealthHandler,
                openApiMetadata: OpenApiRouteMetadata.Create("Health check", "System")
                    .WithDescription("Returns the health status of the API")
                    .WithResponse(200, OpenApiResponseMetadata.Json(
                        "API is healthy",
                        new OpenApiSchemaMetadata
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchemaMetadata>
                            {
                                ["status"] = OpenApiSchemaMetadata.String(),
                                ["timestamp"] = OpenApiSchemaMetadata.String("date-time")
                            }
                        })));

            // Get all users
            _Server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/api/users",
                GetUsersHandler,
                openApiMetadata: OpenApiRouteMetadata.Create("Get all users", "Users")
                    .WithDescription("Retrieves a list of all users in the system")
                    .WithParameter(OpenApiParameterMetadata.Query("active", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                    .WithResponse(200, OpenApiResponseMetadata.Json(
                        "List of users",
                        OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.CreateRef("User")))));

            // Get user by ID
            _Server.Routes.PreAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/api/users/{id}",
                GetUserByIdHandler,
                openApiMetadata: OpenApiRouteMetadata.Create("Get user by ID", "Users")
                    .WithDescription("Retrieves a specific user by their unique identifier")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.Integer()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("User found", OpenApiSchemaMetadata.CreateRef("User")))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            // Create user
            _Server.Routes.PreAuthentication.Static.Add(
                HttpMethod.POST,
                "/api/users",
                CreateUserHandler,
                openApiMetadata: OpenApiRouteMetadata.Create("Create user", "Users")
                    .WithDescription("Creates a new user in the system")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        new OpenApiSchemaMetadata
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchemaMetadata>
                            {
                                ["name"] = OpenApiSchemaMetadata.String(),
                                ["email"] = OpenApiSchemaMetadata.String("email")
                            },
                            Required = new List<string> { "name", "email" }
                        },
                        "User data",
                        true))
                    .WithResponse(201, OpenApiResponseMetadata.Created(OpenApiSchemaMetadata.CreateRef("User")))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest()));

            // Delete user
            _Server.Routes.PreAuthentication.Parameter.Add(
                HttpMethod.DELETE,
                "/api/users/{id}",
                DeleteUserHandler,
                openApiMetadata: OpenApiRouteMetadata.Create("Delete user", "Users")
                    .WithDescription("Deletes a user from the system")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.Integer()))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            // Get all products
            _Server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/api/products",
                GetProductsHandler,
                openApiMetadata: OpenApiRouteMetadata.Create("Get all products", "Products")
                    .WithDescription("Retrieves a list of all products in the catalog")
                    .WithParameter(OpenApiParameterMetadata.Query("category", "Filter by category"))
                    .WithParameter(OpenApiParameterMetadata.Query("minPrice", "Minimum price filter", false, OpenApiSchemaMetadata.Number()))
                    .WithParameter(OpenApiParameterMetadata.Query("maxPrice", "Maximum price filter", false, OpenApiSchemaMetadata.Number()))
                    .WithResponse(200, OpenApiResponseMetadata.Json(
                        "List of products",
                        OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.CreateRef("Product")))));

            // Get product by ID
            _Server.Routes.PreAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/api/products/{id}",
                GetProductByIdHandler,
                openApiMetadata: OpenApiRouteMetadata.Create("Get product by ID", "Products")
                    .WithDescription("Retrieves a specific product by its unique identifier")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Product ID", OpenApiSchemaMetadata.Integer()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Product found", OpenApiSchemaMetadata.CreateRef("Product")))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            // Start server
            _Server.Start();

            Console.WriteLine($"Server started on http://{_Hostname}:{_Port}");
            Console.WriteLine();
            Console.WriteLine("Available endpoints:");
            Console.WriteLine($"  OpenAPI JSON: http://{_Hostname}:{_Port}/openapi.json");
            Console.WriteLine($"  Swagger UI:   http://{_Hostname}:{_Port}/swagger");
            Console.WriteLine();
            Console.WriteLine("API Endpoints:");
            Console.WriteLine($"  GET    /health           - Health check");
            Console.WriteLine($"  GET    /api/users        - List all users");
            Console.WriteLine($"  GET    /api/users/{{id}}   - Get user by ID");
            Console.WriteLine($"  POST   /api/users        - Create user");
            Console.WriteLine($"  DELETE /api/users/{{id}}   - Delete user");
            Console.WriteLine($"  GET    /api/products     - List all products");
            Console.WriteLine($"  GET    /api/products/{{id}} - Get product by ID");
            Console.WriteLine();
            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();

            _Server.Stop();
        }

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send("{\"error\": \"Not found\"}", ctx.Token).ConfigureAwait(false);
        }

        private static async Task HealthHandler(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            string json = $"{{\"status\": \"healthy\", \"timestamp\": \"{DateTime.UtcNow:O}\"}}";
            await ctx.Response.Send(json, ctx.Token).ConfigureAwait(false);
        }

        private static async Task GetUsersHandler(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";

            List<User> users = _Users;
            string activeFilter = ctx.Request.Query.Elements["active"];
            if (!String.IsNullOrEmpty(activeFilter) && Boolean.TryParse(activeFilter, out bool active))
            {
                users = users.FindAll(u => u.Active == active);
            }

            string json = JsonSerializer.Serialize(users, _JsonOptions);
            await ctx.Response.Send(json, ctx.Token).ConfigureAwait(false);
        }

        private static async Task GetUserByIdHandler(HttpContextBase ctx)
        {
            string idStr = ctx.Request.Url.Parameters["id"];
            if (!Int32.TryParse(idStr, out int id))
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send("{\"error\": \"Invalid ID format\"}", ctx.Token).ConfigureAwait(false);
                return;
            }

            User user = _Users.Find(u => u.Id == id);
            if (user == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send("{\"error\": \"User not found\"}", ctx.Token).ConfigureAwait(false);
                return;
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            string json = JsonSerializer.Serialize(user, _JsonOptions);
            await ctx.Response.Send(json, ctx.Token).ConfigureAwait(false);
        }

        private static async Task CreateUserHandler(HttpContextBase ctx)
        {
            try
            {
                string body = ctx.Request.DataAsString;
                User newUser = JsonSerializer.Deserialize<User>(body, _JsonOptions);

                if (newUser == null || String.IsNullOrEmpty(newUser.Name) || String.IsNullOrEmpty(newUser.Email))
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.Send("{\"error\": \"Name and email are required\"}", ctx.Token).ConfigureAwait(false);
                    return;
                }

                newUser.Id = _Users.Count + 1;
                newUser.Active = true;
                _Users.Add(newUser);

                ctx.Response.StatusCode = 201;
                ctx.Response.ContentType = "application/json";
                string json = JsonSerializer.Serialize(newUser, _JsonOptions);
                await ctx.Response.Send(json, ctx.Token).ConfigureAwait(false);
            }
            catch
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send("{\"error\": \"Invalid request body\"}", ctx.Token).ConfigureAwait(false);
            }
        }

        private static async Task DeleteUserHandler(HttpContextBase ctx)
        {
            string idStr = ctx.Request.Url.Parameters["id"];
            if (!Int32.TryParse(idStr, out int id))
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send("{\"error\": \"Invalid ID format\"}", ctx.Token).ConfigureAwait(false);
                return;
            }

            User user = _Users.Find(u => u.Id == id);
            if (user == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send("{\"error\": \"User not found\"}", ctx.Token).ConfigureAwait(false);
                return;
            }

            _Users.Remove(user);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
        }

        private static async Task GetProductsHandler(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";

            List<Product> products = new List<Product>(_Products);

            string category = ctx.Request.Query.Elements["category"];
            if (!String.IsNullOrEmpty(category))
            {
                products = products.FindAll(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            string json = JsonSerializer.Serialize(products, _JsonOptions);
            await ctx.Response.Send(json, ctx.Token).ConfigureAwait(false);
        }

        private static async Task GetProductByIdHandler(HttpContextBase ctx)
        {
            string idStr = ctx.Request.Url.Parameters["id"];
            if (!Int32.TryParse(idStr, out int id))
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send("{\"error\": \"Invalid ID format\"}", ctx.Token).ConfigureAwait(false);
                return;
            }

            Product product = _Products.Find(p => p.Id == id);
            if (product == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send("{\"error\": \"Product not found\"}", ctx.Token).ConfigureAwait(false);
                return;
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            string json = JsonSerializer.Serialize(product, _JsonOptions);
            await ctx.Response.Send(json, ctx.Token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// User model.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public bool Active { get; set; }
    }

    /// <summary>
    /// Product model.
    /// </summary>
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; }
    }
}
