namespace Test.RestApi
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Health;
    using WatsonWebserver.Core.OpenApi;

    internal static class Program
    {
        private static string _Hostname = "127.0.0.1";
        private static int _Port = 8080;
        private static ConcurrentDictionary<Guid, Product> _Products = new ConcurrentDictionary<Guid, Product>();

        private static async Task Main(string[] args)
        {
            SeedProducts();

            WebserverSettings settings = CreateSettings();
            Webserver server = new Webserver(settings, DefaultRoute);

            ConfigureServer(server);
            WriteStartupBanner();

            await server.StartAsync();
            Console.ReadLine();
        }

        private static WebserverSettings CreateSettings()
        {
            WebserverSettings settings = new WebserverSettings(_Hostname, _Port, false);
            settings.Debug.Routing = true;
            settings.Debug.Requests = true;
            settings.Debug.Responses = true;
            settings.Timeout.DefaultTimeout = TimeSpan.FromSeconds(30);
            return settings;
        }

        private static void ConfigureServer(Webserver server)
        {
            server.Events.Logger = Console.WriteLine;
            ConfigureMiddleware(server);
            ConfigureHealthCheck(server);
            ConfigureOpenApi(server);
            ConfigureAuthentication(server);
            ConfigureRoutes(server);
        }

        private static void ConfigureMiddleware(Webserver server)
        {
            server.Middleware.Add(async (ctx, next, token) =>
            {
                DateTime start = DateTime.UtcNow;
                await next();
                double ms = (DateTime.UtcNow - start).TotalMilliseconds;
                Console.WriteLine($"[Middleware] {ctx.Request.Method} {ctx.Request.Url.RawWithQuery} -> {ctx.Response.StatusCode} ({ms:F1}ms)");
            });
        }

        private static void ConfigureHealthCheck(Webserver server)
        {
            server.UseHealthCheck(health =>
            {
                health.Path = "/health";
                health.CustomCheck = async (token) =>
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatusEnum.Healthy,
                        Description = "All systems operational",
                        Data = new Dictionary<string, object>
                        {
                            { "products", _Products.Count },
                            { "uptime_ms", Environment.TickCount64 }
                        }
                    };
                };
            });
        }

        private static void ConfigureOpenApi(Webserver server)
        {
            server.UseOpenApi(api =>
            {
                api.Info.Title = "Test REST API";
                api.Info.Version = "7.0.0";
                api.Info.Description = "Interactive test server for Watson Webserver 7.0 API route integration";
                api.Tags.Add(new OpenApiTag { Name = "Products", Description = "Product management endpoints" });
                api.Tags.Add(new OpenApiTag { Name = "Auth", Description = "Authentication endpoints" });
                api.Tags.Add(new OpenApiTag { Name = "Misc", Description = "Miscellaneous endpoints" });
            });
        }

        private static void ConfigureAuthentication(Webserver server)
        {
            server.Routes.AuthenticateApiRequest = async (ctx) =>
            {
                string authHeader = ctx.Request.RetrieveHeaderValue("Authorization");
                if (!String.IsNullOrEmpty(authHeader) && authHeader == "Bearer test-token-123")
                {
                    return new AuthResult
                    {
                        AuthenticationResult = AuthenticationResultEnum.Success,
                        AuthorizationResult = AuthorizationResultEnum.Permitted,
                        Metadata = new { UserId = 1, Role = "Admin" }
                    };
                }

                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.NotFound,
                    AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
                };
            };
        }

        private static void ConfigureRoutes(Webserver server)
        {
            server.Get("/", async (req) => new { Message = "Hello, World!", Version = "7.0.0" });

            server.Get("/products", async (req) =>
            {
                int page = req.Query.GetInt("page", 1);
                int size = req.Query.GetInt("size", 10);
                List<Product> all = _Products.Values.ToList();
                List<Product> paged = all.Skip((page - 1) * size).Take(size).ToList();
                return new { Total = all.Count, Page = page, Size = size, Products = paged };
            });

            server.Get("/products/{id}", async (req) =>
            {
                Guid id = req.Parameters.GetGuid("id");
                if (_Products.TryGetValue(id, out Product product))
                {
                    return product;
                }

                throw new WebserverException(ApiResultEnum.NotFound, "Product not found");
            });

            server.Post<CreateProductRequest>("/products", async (req) =>
            {
                CreateProductRequest body = req.GetData<CreateProductRequest>();
                if (body == null || String.IsNullOrEmpty(body.Name))
                {
                    throw new WebserverException(ApiResultEnum.BadRequest, "Name is required");
                }

                Product product = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = body.Name,
                    Price = body.Price
                };

                _Products[product.Id] = product;
                req.Http.Response.StatusCode = 201;
                return product;
            });

            server.Put<UpdateProductRequest>("/products/{id}", async (req) =>
            {
                Guid id = req.Parameters.GetGuid("id");
                if (!_Products.TryGetValue(id, out Product existing))
                {
                    throw new WebserverException(ApiResultEnum.NotFound, "Product not found");
                }

                UpdateProductRequest body = req.GetData<UpdateProductRequest>();
                if (body.Name != null) existing.Name = body.Name;
                if (body.Price.HasValue) existing.Price = body.Price.Value;

                return existing;
            });

            server.Delete("/products/{id}", async (req) =>
            {
                Guid id = req.Parameters.GetGuid("id");
                if (!_Products.TryRemove(id, out Product _))
                {
                    throw new WebserverException(ApiResultEnum.NotFound, "Product not found");
                }

                return new { Deleted = true, Id = id };
            });

            server.Post("/upload", async (req) =>
            {
                string rawBody = req.Http.Request.DataAsString;
                return new
                {
                    ReceivedBytes = rawBody?.Length ?? 0,
                    Preview = rawBody?.Substring(0, Math.Min(100, rawBody?.Length ?? 0))
                };
            });

            server.Post<LoginRequest>("/login", async (req) =>
            {
                LoginRequest body = req.GetData<LoginRequest>();
                if (body != null && body.Username == "admin" && body.Password == "secret")
                {
                    req.Http.Response.StatusCode = 201;
                    return new LoginSuccessResponse
                    {
                        Token = "test-token-123",
                        ExpiresIn = 3600
                    };
                }

                throw new WebserverException(ApiResultEnum.NotAuthorized, "Invalid credentials");
            });

            server.Get("/slow", async (req) =>
            {
                await Task.Delay(60000, req.CancellationToken);
                return new { Result = "This should never be returned" };
            });

            server.Get("/error", async (req) =>
            {
                throw new WebserverException(ApiResultEnum.NotFound, "This is a test error");
            });

            server.Get("/admin/stats", async (req) =>
            {
                return new
                {
                    ProductCount = _Products.Count,
                    Metadata = req.Metadata,
                    ServerTime = DateTime.UtcNow
                };
            }, auth: true);
        }

        private static void WriteStartupBanner()
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("  Watson Webserver 7.0 - Test.RestApi");
            Console.WriteLine("===========================================");
            Console.WriteLine();
            Console.WriteLine("Endpoints:");
            Console.WriteLine("  GET    http://" + _Hostname + ":" + _Port + "/                     Hello world");
            Console.WriteLine("  GET    http://" + _Hostname + ":" + _Port + "/products             List products (?page=1&size=10)");
            Console.WriteLine("  GET    http://" + _Hostname + ":" + _Port + "/products/{id}        Get product by ID");
            Console.WriteLine("  POST   http://" + _Hostname + ":" + _Port + "/products             Create product (JSON body)");
            Console.WriteLine("  PUT    http://" + _Hostname + ":" + _Port + "/products/{id}        Update product (JSON body)");
            Console.WriteLine("  DELETE http://" + _Hostname + ":" + _Port + "/products/{id}        Delete product");
            Console.WriteLine("  POST   http://" + _Hostname + ":" + _Port + "/upload               Raw body upload");
            Console.WriteLine("  POST   http://" + _Hostname + ":" + _Port + "/login                Login {\"Username\":\"admin\",\"Password\":\"secret\"}");
            Console.WriteLine("  GET    http://" + _Hostname + ":" + _Port + "/slow                 Timeout demo (408 after 30s)");
            Console.WriteLine("  GET    http://" + _Hostname + ":" + _Port + "/error                Error demo (404)");
            Console.WriteLine("  GET    http://" + _Hostname + ":" + _Port + "/health               Health check");
            Console.WriteLine("  GET    http://" + _Hostname + ":" + _Port + "/admin/stats          Protected (Authorization: Bearer test-token-123)");
            Console.WriteLine("  GET    http://" + _Hostname + ":" + _Port + "/openapi.json         OpenAPI spec");
            Console.WriteLine("  GET    http://" + _Hostname + ":" + _Port + "/swagger              Swagger UI");
            Console.WriteLine();
            Console.WriteLine("Press ENTER to exit.");
            Console.WriteLine();
        }

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send("{\"Error\":\"NotFound\",\"Message\":\"No route matched\"}");
        }

        private static void SeedProducts()
        {
            Product p1 = new Product { Id = Guid.NewGuid(), Name = "Widget", Price = 9.99m };
            Product p2 = new Product { Id = Guid.NewGuid(), Name = "Gadget", Price = 24.99m };
            Product p3 = new Product { Id = Guid.NewGuid(), Name = "Doohickey", Price = 4.50m };
            _Products[p1.Id] = p1;
            _Products[p2.Id] = p2;
            _Products[p3.Id] = p3;
        }
    }
}
