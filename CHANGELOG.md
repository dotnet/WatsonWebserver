# Change Log

## Current Version

v3.0.6.1

- Fix for content routes causing 500 (thank you @zaksnet)

## Previous Versions

v3.0.6

- Async/await change in main request look to fix InvalidOperationException (thank you @zaksnet)

v3.0.5

- Removed ThreadPool.QueueUserWorkItem in favor of unawaited Tasks
- Removed .RunSynchronously in favor of .Wait for the default route, thereby eliminating an InvalidOperationException (thank you @at1993)
- Properly firing ResponseSent events when the event callback is defined (thank you @at1993)
- Fixed an issue where the file path for content routes was not properly constructed (thank you @zaksnet)
- Added better documentation on event callbacks

v3.0.4

- Exposed certain HttpRequest factories to support 3rd-party apps built using Watson.

v3.0.3

- Removed welcome message

v3.0.2

- XML documentation

v3.0.1

- BREAKING CHANGE from previous versions, major refactor!
- Improved support for both sending and receiving data/payloads using ```Transfer-Encoding: chunked```
- Routes and callbacks now use ```Task MyRouteHandler(HttpContext ctx)```
- All request data is now either accessible through ```HttpRequest.Data``` (stream) or ```HttpRequest.ReadChunk``` (for chunked transfers only)
- Huge thanks to @winkmichael and @xmike402 for their help, guidance, and contribution to the project!
 
v2.1.x 

- Pre-routing handler, i.e. a callback used for all requests prior to routing
- Automatic decoding of incoming requests that have ```Transfer-Encoding: chunked``` in the headers
- Does not validate chunk signatures or decompress using gzip/deflate yet
- Better support for HEAD requests where content-length header is required (separate constructor for HttpResponse)
- Added stream support to content route processor for better large object support
- Bugfixes (content type not being set)

v2.0.x

- Support for Stream in ```HttpRequest``` and ```HttpResponse```.  To use, set ```Server.ReadInputStream``` to ```false```.  Refer to the ```TestStreamServer``` project for a full example
- Simplified constructors, removed pre-defined JSON packaging for responses
- ```HttpResponse``` now only accepts byte arrays for ```Data``` for simplicity

v1.x

- Fix URL encoding (using System.Net.WebUtility.UrlDecode instead of Uri.EscapeString)
- Refactored content routes, static routes, and dynamic routes (breaking change)
- Added default permit/deny operation along with whitelist and blacklist
- Added a new constructor allowing Watson to support multiple listener hostnames
- Retarget to support both .NET Core 2.0 and .NET Framework 4.6.2.
- Fix for attaching request body data to the HttpRequest object (thanks @user4000!)
- Retarget to .NET Framework 4.6.2
- Enum for HTTP method instead of string (breaking change)
- Bugfix for content routes that have spaces or ```+``` (thanks @Linqx)
- Support for passing an object as Data to HttpResponse (will be JSON serialized)
- Support for implementing your own OPTIONS handler (for CORS and other use cases)
- Bugfix for dispose (thank you @AChmieletzki)
- Static methods for building HttpRequest from various sources and conversion
- Static input methods
- Better initialization of object members
- More HTTP status codes (see https://en.wikipedia.org/wiki/List_of_HTTP_status_codes)
- Fix for content routes (thank you @KKoustas!)
- Fix for Xamarin IOS and Android (thank you @Tutch!)
- Added content routes for serving static files.
- Dynamic route support using C#/.NET regular expressions (see RegexMatcher library https://github.com/jchristn/RegexMatcher).
- IsListening property
- Added support for static routes.  The default handler can be used for cases where a matching route isn't available, for instance, to build a custom 404 response.
