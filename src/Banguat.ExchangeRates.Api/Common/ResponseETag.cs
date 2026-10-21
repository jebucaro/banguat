using System.Security.Cryptography;

namespace Banguat.ExchangeRates.Api.Common;

/// <summary>
/// Derives an entity tag from the exact bytes that will be written to the response.
/// </summary>
/// <remarks>
/// Hashing the serialized payload rather than the request inputs means the tag changes if and only if the
/// representation changes. That is what lets a revalidation succeed on days Banguat publishes nothing: the
/// payload is byte-identical, so the tag matches and the client receives a 304 instead of a full body.
/// </remarks>
internal static class ResponseETag
{
    /// <summary>Computes a strong, quoted entity tag for <paramref name="payload"/>.</summary>
    internal static string Compute(ReadOnlySpan<byte> payload)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(payload, hash);

        // Base64 output contains only etagc-legal characters, so no further escaping is needed.
        return string.Concat("\"", Convert.ToBase64String(hash), "\"");
    }
}