using Microsoft.AspNetCore.Builder;
using Xunit;

namespace Kododo.ConfigWay.Tests.Unit;

public class ConfigurationBuilderTests
{
    [Theory]
    [InlineData(typeof(EmailOptions),  "Email")]
    [InlineData(typeof(NestedOptions), "Nested")]
    [InlineData(typeof(AppSettings),  "AppSettings")]
    [InlineData(typeof(Options),       "Options")]
    public void AddOptions_WithoutExplicitKey_DerivesKeyFromTypeName(Type type, string expectedKey)
    {
        var builder = CreateBuilder();

        var method = typeof(ConfigurationBuilder)
            .GetMethod(nameof(ConfigurationBuilder.AddOptions))!
            .MakeGenericMethod(type);
        method.Invoke(builder, [null]);

        var config = builder.Build();
        Assert.Contains(config.Options, o => o.Key == expectedKey);
    }

    [Fact]
    public void AddOptions_WithExplicitKey_UsesProvidedKeyVerbatim()
    {
        var builder = CreateBuilder();
        builder.AddOptions<SimpleOptions>("MyCustomKey");

        var config = builder.Build();
        Assert.Single(config.Options, o => o.Key == "MyCustomKey");
    }

    [Fact]
    public void AddOptions_CalledTwice_BothOptionsPresent()
    {
        var builder = CreateBuilder();
        builder.AddOptions<SimpleOptions>("First");
        builder.AddOptions<NestedOptions>("Second");

        var config = builder.Build();
        Assert.Equal(2, config.Options.Count);
        Assert.Equivalent(new[] { "First", "Second" }, config.Options.Select(o => o.Key), strict: true);
    }

    [Fact]
    public void AddOptions_StoresCorrectType()
    {
        var builder = CreateBuilder();
        builder.AddOptions<SimpleOptions>("Simple");

        var config = builder.Build();
        Assert.Equal(typeof(SimpleOptions), config.Options.Single(o => o.Key == "Simple").Type);
    }

    [Fact]
    public void Build_WithDefaultStore_UsesInMemoryStore()
    {
        var builder = CreateBuilder();
        var config = builder.Build();

        Assert.NotNull(config.Store);
    }

    [Fact]
    public void AddProvider_KeepsTheProvidersInOrder()
    {
        var builder = CreateBuilder();
        var first   = new EmptyProvider();
        var second  = new EmptyProvider();

        builder.AddProvider(first).AddProvider(second);

        Assert.Equal([first, second], builder.Providers);
    }

    [Fact]
    public void ReloadInterval_DefaultsToNull()
    {
        Assert.Null(CreateBuilder().ReloadInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddConfigWay_WithANonPositiveReloadInterval_Throws(int seconds)
    {
        var appBuilder = WebApplication.CreateBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            appBuilder.AddConfigWay(x => x.ReloadInterval = TimeSpan.FromSeconds(seconds)));
    }

    private static ConfigurationBuilder CreateBuilder()
    {
        var appBuilder = WebApplication.CreateBuilder();
        return new ConfigurationBuilder(appBuilder);
    }
}

file sealed class EmptyProvider : StoreConfigurationProvider
{
    protected override IEnumerable<KeyValuePair<string, string?>> Select(
        IReadOnlyList<Kododo.ConfigWay.Core.Model.Setting> settings) => [];
}
