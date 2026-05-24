using System.Security.Claims;

namespace Gatekeeper.Api.AdminAuthentication;

public sealed class AdminSessionGuard
{
    public bool IsAuthenticated(HttpContext httpContext)
    {
        return httpContext.User.Identity?.IsAuthenticated == true
            && !string.IsNullOrWhiteSpace(GetUsername(httpContext));
    }

    public string GetUsername(HttpContext httpContext)
    {
        return httpContext.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
    }

    public bool HasValidUnsafeRequestOrigin(HttpContext httpContext)
    {
        if (
            HttpMethods.IsGet(httpContext.Request.Method)
            || HttpMethods.IsHead(httpContext.Request.Method)
            || HttpMethods.IsOptions(httpContext.Request.Method)
        )
        {
            return true;
        }

        string expectedOrigin = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

        if (httpContext.Request.Headers.TryGetValue("Origin", out var originValues))
        {
            return originValues.Count == 1
                && string.Equals(
                    originValues[0],
                    expectedOrigin,
                    StringComparison.OrdinalIgnoreCase
                );
        }

        if (httpContext.Request.Headers.TryGetValue("Referer", out var refererValues))
        {
            return refererValues.Count == 1
                && IsSameOriginReferer(refererValues[0], expectedOrigin);
        }

        if (httpContext.Request.Headers.TryGetValue("Sec-Fetch-Site", out var fetchSiteValues))
        {
            return fetchSiteValues.Count == 1
                && (
                    string.Equals(
                        fetchSiteValues[0],
                        "same-origin",
                        StringComparison.OrdinalIgnoreCase
                    )
                    || string.Equals(fetchSiteValues[0], "none", StringComparison.OrdinalIgnoreCase)
                );
        }

        return false;
    }

    private static bool IsSameOriginReferer(string? referer, string expectedOrigin)
    {
        if (!Uri.TryCreate(referer, UriKind.Absolute, out Uri? refererUri))
        {
            return false;
        }

        string refererOrigin = $"{refererUri.Scheme}://{refererUri.Authority}";
        return string.Equals(refererOrigin, expectedOrigin, StringComparison.OrdinalIgnoreCase);
    }
}
