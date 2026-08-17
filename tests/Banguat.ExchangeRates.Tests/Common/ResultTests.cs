using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_Should_HaveNoError()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_Should_CarryError()
    {
        Error error = Error.Failure("Test.Failure", "Something went wrong");

        Result result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void SuccessOfT_Should_ExposeValue()
    {
        Result<int> result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void FailureOfT_Should_ThrowWhenAccessingValue()
    {
        Result<int> result = Result.Failure<int>(Error.Failure("Test.Failure", "nope"));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Constructor_Should_ThrowWhenSuccessHasError()
    {
        Assert.Throws<ArgumentException>(() => new Result(true, Error.Failure("X", "Y")));
    }

    [Fact]
    public void Constructor_Should_ThrowWhenFailureHasNoError()
    {
        Assert.Throws<ArgumentException>(() => new Result(false, Error.None));
    }
}
