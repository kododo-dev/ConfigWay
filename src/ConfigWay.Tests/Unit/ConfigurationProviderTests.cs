using FluentAssertions;
using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.Core.Store;
using NSubstitute;
using Xunit;
using CoreOptions = Kododo.ConfigWay.Core.Configuration.Options;

namespace Kododo.ConfigWay.Tests.Unit;

public class ConfigurationProviderTests
{
    [Fact]
    public void Load_IncludesOnlyKeysRegisteredForKnownOptions()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([
            new Setting("Simple:Name",  "Alice"),
            new Setting("Simple:Value", "42"),
            new Setting("Unknown:Key",  "should-be-filtered"),
        ]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));

        provider.Load();

        provider.TryGet("Simple:Name",  out var name).Should().BeTrue();
        provider.TryGet("Simple:Value", out var value).Should().BeTrue();
        name.Should().Be("Alice");
        value.Should().Be("42");

        provider.TryGet("Unknown:Key", out _).Should().BeFalse();
    }

    [Fact]
    public void Load_IsCaseInsensitive()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("SIMPLE:NAME", "Alice")]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        provider.TryGet("simple:name", out var value).Should().BeTrue();
        value.Should().Be("Alice");
    }

    [Fact]
    public void Load_IncludesNullValues()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("Simple:Name", null)]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        provider.TryGet("Simple:Name", out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [Fact]
    public void Load_ForNestedType_CollectsDeepKeys()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([
            new Setting("Root:TopLevel",     "top"),
            new Setting("Root:Child:ChildProp", "child"),
        ]);

        var provider = CreateProvider(store, new CoreOptions("Root", typeof(NestedOptions)));
        provider.Load();

        provider.TryGet("Root:TopLevel",      out var top).Should().BeTrue();
        provider.TryGet("Root:Child:ChildProp", out var child).Should().BeTrue();
        top.Should().Be("top");
        child.Should().Be("child");
    }

    [Fact]
    public void Load_IgnoresReadOnlyProperties()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("Opt:ReadOnly", "value")]);

        var provider = CreateProvider(store, new CoreOptions("Opt", typeof(ReadOnlyPropOptions)));
        provider.Load();

        provider.TryGet("Opt:ReadOnly", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ReloadAsync_UpdatesData()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("Simple:Name", "Before")]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        store.GetAllAsync().Returns([new Setting("Simple:Name", "After")]);
        await ((IConfigurationEditor)provider).ReloadAllAsync(CancellationToken.None);

        provider.TryGet("Simple:Name", out var value);
        value.Should().Be("After");
    }

    [Fact]
    public async Task ReloadAsync_FiresChangeToken()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        var tokenBefore = provider.GetReloadToken();
        var changed = false;
        tokenBefore.RegisterChangeCallback(_ => changed = true, null);

        await ((IConfigurationEditor)provider).ReloadAllAsync(CancellationToken.None);

        changed.Should().BeTrue();
    }

    private static ConfigurationProvider CreateProvider(IStore store, params CoreOptions[] options)
    {
        var config = new Configuration(store, options);
        return new ConfigurationProvider(config);
    }
}

file class ReadOnlyPropOptions
{
    public string ReadOnly { get; } = string.Empty;
    public string Writable { get; set; } = string.Empty;
}
