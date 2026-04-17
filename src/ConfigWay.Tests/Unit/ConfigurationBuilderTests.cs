using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace Kododo.ConfigWay.Tests.Unit;

public class ConfigurationBuilderTests
{
    // ── StripOptionsSuffix (tested via AddOptions key assignment) ─────────────

    [Theory]
    [InlineData(typeof(EmailOptions),  "Email")]        // standard suffix strip
    [InlineData(typeof(NestedOptions), "Nested")]       // another suffix strip
    [InlineData(typeof(AppSettings),  "AppSettings")]  // no "Options" suffix → unchanged
    [InlineData(typeof(Options),       "Options")]      // name == suffix → unchanged (too short)
    public void AddOptions_WithoutExplicitKey_DerivesKeyFromTypeName(Type type, string expectedKey)
    {
        var builder = CreateBuilder();

        // call AddOptions<T> via reflection to avoid hardcoding the type parameter
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ConfigurationBuilder CreateBuilder()
    {
        var appBuilder = WebApplication.CreateBuilder();
        return new ConfigurationBuilder(appBuilder);
    }
}
