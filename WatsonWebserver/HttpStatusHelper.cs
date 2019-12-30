using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tfres
{
  public static class HttpStatusHelper
  {
    public static string GetStatusMessage(int status) => GetStatusMessage((HttpStatus) status);

    public static string GetStatusMessage(HttpStatus status)
    {
      switch (status)
      {
        case HttpStatus.Continue:
          return "Continue";
        case HttpStatus.SwitchingProtocols:
          return "Switching protocols";
        case HttpStatus.Processing:
          return "Processing";
        case HttpStatus.Ok:
          return "Ok";
        case HttpStatus.Created:
          return "Created";
        case HttpStatus.Accepted:
          return "Accepted";
        case HttpStatus.NonAuthoritativeInformation:
          return "Non authoritative information";
        case HttpStatus.NoContent:
          return "No content";
        case HttpStatus.ResetContent:
          return "Reset content";
        case HttpStatus.PartialContent:
          return "Partial content";
        case HttpStatus.MultiStatus:
          return "Multi status";
        case HttpStatus.AlreadyReported:
          return "Already reported";
        case HttpStatus.ImUsed:
          return "IM used";
        case HttpStatus.MultipleChoices:
          return "Multiple choices";
        case HttpStatus.MovedPermanently:
          return "Moved permanently";
        case HttpStatus.FoundMovedTemporarily:
          return "Found moved temporarily";
        case HttpStatus.SeeOther:
          return "See other";
        case HttpStatus.NotModified:
          return "Not modified";
        case HttpStatus.UseProxy:
          return "Use proxy";
        case HttpStatus.SwitchProxy:
          return "Switch proxy";
        case HttpStatus.TemporaryRedirect:
          return "Temporary redirect";
        case HttpStatus.PermanentRedirect:
          return "Permanent redirect";
        case HttpStatus.BadRequest:
          return "Bad request";
        case HttpStatus.Unauthorized:
          return "Unauthorized";
        case HttpStatus.PaymentRequired:
          return "Payment required";
        case HttpStatus.Forbidden:
          return "Forbidden";
        case HttpStatus.NotFound:
          return "Not found";
        case HttpStatus.MethodNotAllowed:
          return "Method not allowed";
        case HttpStatus.NotAcceptable:
          return "Not acceptable";
        case HttpStatus.ProxyAuthenticationRequired:
          return "Proxy authentication required";
        case HttpStatus.RequestTimeout:
          return "Request timeout";
        case HttpStatus.Conflict:
          return "Conflict";
        case HttpStatus.Gone:
          return "Gone";
        case HttpStatus.LengthRequired:
          return "Length required";
        case HttpStatus.PreconditionFailed:
          return "Precondition failed";
        case HttpStatus.RequestEntityTooLarge:
          return "Request entity too large";
        case HttpStatus.UriTooLong:
          return "Uri too long";
        case HttpStatus.UnsupportedMediaType:
          return "Unsupported media type";
        case HttpStatus.RequestedRangeNotSatisfiable:
          return "Requested range not satisfiable";
        case HttpStatus.ExpectationFailed:
          return "Expectation failed";
        case HttpStatus.PolicyNotFulfilled:
          return "Policy not fulfilled";
        case HttpStatus.MisdirectedRequest:
          return "Misdirected request";
        case HttpStatus.UnprocessableEntity:
          return "Unprocessable entity";
        case HttpStatus.Locked:
          return "Locked";
        case HttpStatus.FailedDependency:
          return "Failed dependency";
        case HttpStatus.UpgradeRequired:
          return "Upgrade required";
        case HttpStatus.PreconditionRequired:
          return "Precondition required";
        case HttpStatus.TooManyRequests:
          return "TooMany requests";
        case HttpStatus.RequestHeaderFieldsTooLarge:
          return "Request header fields too large";
        case HttpStatus.UnavailableForLegalReasons:
          return "Unavailable for legal reasons";
        case HttpStatus.ImaTeapot:
          return "I'm a teapot";
        case HttpStatus.UnorderedCollection:
          return "Unordered collection";
        case HttpStatus.NoResponse:
          return "No response";
        case HttpStatus.TheRequestShouldBeRetriedAfterDoingTheAppropriateAction:
          return "The request should be retried after doing the appropriate action";
        case HttpStatus.ClientClosedRequest:
          return "Client Closed Request";
        case HttpStatus.InternalServerError:
          return "Internal Server Error";
        case HttpStatus.NotImplemented:
          return "Not implemented";
        case HttpStatus.BadGateway:
          return "Bad gateway";
        case HttpStatus.ServiceUnavailable:
          return "Service unavailable";
        case HttpStatus.GatewayTimeout:
          return "Gateway timeout";
        case HttpStatus.HttpVersionNotSupported:
          return "HTTP version not supported";
        case HttpStatus.VariantAlsoNegotiates:
          return "Variant also negotiates";
        case HttpStatus.InsufficientStorage:
          return "Insufficient storage";
        case HttpStatus.LoopDetected:
          return "Loop detected";
        case HttpStatus.BandwidthLimitExceeded:
          return "Bandwidth limit exceeded";
        case HttpStatus.NotExtended:
          return "Not extended";
        case HttpStatus.NetworkAuthenticationRequired:
          return "Network authentication required";
        default:
          return string.Empty;
      }
    }
  }
}
