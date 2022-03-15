using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;

namespace APSIM.Builds.Utility;

public static class HttpRequestExtensions
{
    /// <summary>
    /// Get a HTTP request header. Throw if not found.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="key">Name of the desired header.</param>
    public static string GetHeader(this HttpRequest request, string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        Func<string, bool> isCorrectHeader = x => string.Equals(x, key, StringComparison.InvariantCultureIgnoreCase);
        if (!request.Headers.TryGetValue(key, out StringValues values))
            throw new ArgumentException($"Request does not contain a {key} header");

        if (!values.Any())
            throw new ArgumentException($"{key} header does not contain a value");

        return values.First();
    }
}
