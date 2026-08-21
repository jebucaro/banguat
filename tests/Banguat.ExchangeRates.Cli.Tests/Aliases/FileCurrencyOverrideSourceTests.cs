using Banguat.ExchangeRates.Cli.Aliases;
using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Cli.Tests.Aliases;

public class FileCurrencyOverrideSourceTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsEmptyMap()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");
        FileCurrencyOverrideSource source = new(path);

        IReadOnlyDictionary<string, CurrencyCode> result = source.Load();

        Assert.Empty(result);
    }

    [Fact]
    public void Load_WhenFileValid_ReturnsMap()
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, """{"EUR": 24}""");
        try
        {
            FileCurrencyOverrideSource source = new(path);

            IReadOnlyDictionary<string, CurrencyCode> result = source.Load();

            Assert.Equal(new CurrencyCode(24), result["EUR"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_IsCaseInsensitive()
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, """{"EUR": 24}""");
        try
        {
            FileCurrencyOverrideSource source = new(path);

            IReadOnlyDictionary<string, CurrencyCode> result = source.Load();

            Assert.True(result.ContainsKey("eur"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WhenFileMalformed_ThrowsCurrencyOverrideLoadException()
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, "{ not valid json");
        try
        {
            FileCurrencyOverrideSource source = new(path);

            CurrencyOverrideLoadException exception =
                Assert.Throws<CurrencyOverrideLoadException>(() => source.Load());

            Assert.Contains(path, exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WhenTwoAliasesShareACode_ThrowsCurrencyOverrideLoadException()
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, """{"EUR": 24, "EURO": 24}""");
        try
        {
            FileCurrencyOverrideSource source = new(path);

            CurrencyOverrideLoadException exception =
                Assert.Throws<CurrencyOverrideLoadException>(() => source.Load());

            Assert.Contains("24", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}