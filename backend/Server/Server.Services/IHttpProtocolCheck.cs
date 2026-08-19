using System.Net;

namespace Server.Services;

/// <summary>Performs a bounded, read-only HTTP operation for point-source connectivity testing.</summary>
public interface IHttpProtocolCheck
{
    /// <summary>Sends the source's lightweight validation request while pinning connections to previously approved addresses.</summary>
    /// <param name="source">The validated HTTP source defining the HTTPS URI, method, headers, timeout, redirect, and response-size constraints.</param>
    /// <param name="credential">The resolved credential value, or an empty string only when the source requires no authentication.</param>
    /// <param name="pinnedAddresses">The non-empty, duplicate-free DNS results that passed SSRF policy; redirects must be independently resolved and validated.</param>
    /// <param name="cancellationToken">Cancels request, response, and redirect processing within the overall test budget.</param>
    /// <returns>Optional bounded server identity safe for diagnostics, or <see langword="null"/> when no identity is available.</returns>
    Task<string?> CheckAsync(
        PointSource source,
        string credential,
        IReadOnlyList<IPAddress> pinnedAddresses,
        CancellationToken cancellationToken);
}