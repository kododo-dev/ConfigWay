using FluentAssertions;
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
        config.Options.Should().Contain(o => o.Key == expectedKey);
    }

    [Fact]
    public void AddOptions_WithExplicitKey_UsesProvidedKeyVerbatim()
    {
        var builder = CreateBuilder();
        builder.AddOptions<SimpleOptions>("MyCustomKey");

        var config = builder.Build();
        config.Options.Should().ContainSingle(o => o.Key == "MyCustomKey");
    }

    [Fact]
    public void AddOptions_CalledTwice_BothOptionsPresent()
    {
        var builder = CreateBuilder();
        builder.AddOptions<SimpleOptions>("First");
        builder.AddOptions<NestedOptions>("Second");

        var config = builder.Build();
        config.Options.Should().HaveCount(2);
        config.Options.Select(o => o.Key).Should().BeEquivalentTo("First", "Second");
    }

    [Fact]
    public void AddOptions_StoresCorrectType()
    {
        var builder = CreateBuilder();
        builder.AddOptions<SimpleOptions>("Simple");

        var config = builder.Build();
        config.Options.Single(o => o.Key == "Simple").Type.Should().Be(typeof(SimpleOptions));
    }

    [Fact]
    public void Build_WithDefaultStore_UsesInMemoryStore()
    {
        var builder = CreateBuilder();
        var config = builder.Build();

        config.Store.Should().NotBeNull();
    }

    private static ConfigurationBuilder CreateBuilder()
    {
        var appBuilder = WebApplication.CreateBuilder();
        return new ConfigurationBuilder(appBuilder);
    }
}
