using System.Text;
using Banguat.ExchangeRates.Api.Common;

namespace Banguat.ExchangeRates.Api.Tests.Common;

public class ResponseETagTests
{
    private static string Compute(string payload) => ResponseETag.Compute(Encoding.UTF8.GetBytes(payload));

    [Fact]
    public void Compute_IsStableAcrossCalls()
    {
        const string payload = """{"currency":2,"buy":7.65,"sell":7.70}""";

        Assert.Equal(Compute(payload), Compute(payload));
    }

    [Fact]
    public void Compute_ChangesWhenASingleFieldChanges()
    {
        string unchanged = Compute("""{"currency":2,"buy":7.65,"sell":7.70}""");
        string changed = Compute("""{"currency":2,"buy":7.66,"sell":7.70}""");

        Assert.NotEqual(unchanged, changed);
    }

    [Fact]
    public void Compute_ChangesWhenFieldOrderChanges()
    {
        string first = Compute("""{"buy":7.65,"sell":7.70}""");
        string second = Compute("""{"sell":7.70,"buy":7.65}""");

        Assert.NotEqual(first, second);
    }

    // RFC 9110 requires an entity tag to be a quoted string; unquoted tags are silently ignored by some
    // intermediaries, which would disable revalidation without any visible error.
    [Fact]
    public void Compute_ReturnsAQuotedStrongTag()
    {
        string tag = Compute("payload");

        Assert.StartsWith("\"", tag, StringComparison.Ordinal);
        Assert.EndsWith("\"", tag, StringComparison.Ordinal);
        Assert.DoesNotContain('"', tag[1..^1]);
        Assert.DoesNotContain('\\', tag);
    }

    [Fact]
    public void Compute_HandlesEmptyPayload()
    {
        Assert.Equal(Compute(string.Empty), ResponseETag.Compute(ReadOnlySpan<byte>.Empty));
    }
}
