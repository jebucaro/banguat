using CliFx;
using Microsoft.Extensions.DependencyInjection;

namespace Banguat.ExchangeRates.Cli.Tests;

public class ProgramServicesTests
{
    public static TheoryData<Type> CommandTypes()
    {
        TheoryData<Type> types = new();
        foreach (Type type in typeof(Program).Assembly.GetTypes()
                     .Where(type =>
                         typeof(ICommand).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false }))
        {
            types.Add(type);
        }

        return types;
    }

    [Fact]
    public void CreateServices_Should_BuildWithScopeAndBuildValidation()
    {
        // ValidateOnBuild walks every registration, so a lifetime mistake throws here instead of mid-command.
        using ServiceProvider provider = Program.CreateServices().BuildServiceProvider(Program.ProviderOptions);

        Assert.NotNull(provider);
    }

    [Theory]
    [MemberData(nameof(CommandTypes))]
    public void Command_Should_ResolveFromScope(Type commandType)
    {
        using ServiceProvider provider = Program.CreateServices().BuildServiceProvider(Program.ProviderOptions);
        using IServiceScope scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService(commandType));
    }

    [Fact]
    public void Command_Should_NotResolveFromRoot_WhenItDependsOnScopedServices()
    {
        using ServiceProvider provider = Program.CreateServices().BuildServiceProvider(Program.ProviderOptions);

        // Regression guard: Program used to resolve commands from the root provider, which silently turned the
        // scoped client and resolver into process-wide singletons.
        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<Commands.RateHistoryCommand>());
    }
}