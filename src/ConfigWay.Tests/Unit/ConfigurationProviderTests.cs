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

        Assert.True(provider.TryGet("Simple:Name",  out var name));
        Assert.True(provider.TryGet("Simple:Value", out var value));
        Assert.Equal("Alice", name);
        Assert.Equal("42", value);

        Assert.False(provider.TryGet("Unknown:Key", out _));
    }

    [Fact]
    public void Load_IsCaseInsensitive()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("SIMPLE:NAME", "Alice")]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        Assert.True(provider.TryGet("simple:name", out var value));
        Assert.Equal("Alice", value);
    }

    [Fact]
    public void Load_IncludesNullValues()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("Simple:Name", null)]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        Assert.True(provider.TryGet("Simple:Name", out var value));
        Assert.Null(value);
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

        Assert.True(provider.TryGet("Root:TopLevel",      out var top));
        Assert.True(provider.TryGet("Root:Child:ChildProp", out var child));
        Assert.Equal("top", top);
        Assert.Equal("child", child);
    }

    [Fact]
    public void Load_IgnoresReadOnlyProperties()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("Opt:ReadOnly", "value")]);

        var provider = CreateProvider(store, new CoreOptions("Opt", typeof(ReadOnlyPropOptions)));
        provider.Load();

        Assert.False(provider.TryGet("Opt:ReadOnly", out _));
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
        Assert.Equal("After", value);
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

        Assert.True(changed);
    }

    [Fact]
    public void Load_PassesEverySettingToTheExtensionProviders()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([
            new Setting("Simple:Name", "Alice"),
            new Setting("ext/Beta",    "on"),
        ]);

        var extension = new PrefixProvider("ext/");
        var provider  = CreateProvider(store, [extension], new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        Assert.True(extension.TryGet("Beta", out var beta));
        Assert.Equal("on", beta);
        Assert.False(extension.TryGet("Simple:Name", out _));
        Assert.False(provider.TryGet("ext/Beta", out _));
    }

    [Fact]
    public async Task ReloadAsync_UpdatesTheExtensionProvidersAndFiresTheirChangeTokens()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("ext/Beta", "off")]);

        var extension = new PrefixProvider("ext/");
        var provider  = CreateProvider(store, [extension]);
        provider.Load();

        var changed = false;
        extension.GetReloadToken().RegisterChangeCallback(_ => changed = true, null);

        store.GetAllAsync().Returns([new Setting("ext/Beta", "on")]);
        await ((IConfigurationEditor)provider).ReloadAllAsync(CancellationToken.None);

        extension.TryGet("Beta", out var value);
        Assert.Equal("on", value);
        Assert.True(changed);
    }

    [Fact]
    public void ExtensionProvider_Load_KeepsThePushedData()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("ext/Beta", "on")]);

        var extension = new PrefixProvider("ext/");
        CreateProvider(store, [extension]).Load();
        extension.Load();

        Assert.True(extension.TryGet("Beta", out _));
    }

    [Fact]
    public async Task ReloadIfChangedAsync_WithoutChanges_DoesNotReload()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("Simple:Name", "Alice")]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        var changed = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => changed = true, null);

        store.GetAllAsync().Returns([new Setting("Simple:Name", "Alice")]);
        var reloaded = await provider.ReloadIfChangedAsync(CancellationToken.None);

        Assert.False(reloaded);
        Assert.False(changed);
    }

    public static TheoryData<Setting[]> ChangedStores => new()
    {
        { [new Setting("Simple:Name", "Bob")] },
        { [new Setting("Simple:Name", null)] },
        { [] },
        { [new Setting("Simple:Name", "Alice"), new Setting("Simple:Value", "1")] },
        { [new Setting("Simple:Name", "Alice"), new Setting("Unknown:Key", "x")] },
    };

    [Theory]
    [MemberData(nameof(ChangedStores))]
    public async Task ReloadIfChangedAsync_WhenTheStoreChanged_Reloads(Setting[] current)
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("Simple:Name", "Alice")]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        var changed = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => changed = true, null);

        store.GetAllAsync().Returns(current);
        var reloaded = await provider.ReloadIfChangedAsync(CancellationToken.None);

        Assert.True(reloaded);
        Assert.True(changed);
    }

    [Fact]
    public async Task ReloadIfChangedAsync_AfterReload_ComparesWithTheReloadedValues()
    {
        var store = Substitute.For<IStore>();
        store.GetAllAsync().Returns([new Setting("Simple:Name", "Alice")]);

        var provider = CreateProvider(store, new CoreOptions("Simple", typeof(SimpleOptions)));
        provider.Load();

        store.GetAllAsync().Returns([new Setting("Simple:Name", "Bob")]);
        await ((IConfigurationEditor)provider).ReloadAllAsync(CancellationToken.None);

        Assert.False(await provider.ReloadIfChangedAsync(CancellationToken.None));
    }

    private static ConfigurationProvider CreateProvider(IStore store, params CoreOptions[] options) =>
        CreateProvider(store, [], options);

    private static ConfigurationProvider CreateProvider(
        IStore store,
        StoreConfigurationProvider[] extensions,
        params CoreOptions[] options)
    {
        var config = new Configuration(store, options);
        return new ConfigurationProvider(config, extensions);
    }
}

/// <summary>Exposes the settings whose key starts with a prefix, without the prefix.</summary>
file sealed class PrefixProvider(string prefix) : StoreConfigurationProvider
{
    protected override IEnumerable<KeyValuePair<string, string?>> Select(IReadOnlyList<Setting> settings) =>
        settings
            .Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(s => new KeyValuePair<string, string?>(s.Key[prefix.Length..], s.Value));
}

file class ReadOnlyPropOptions
{
    public string ReadOnly { get; } = string.Empty;
    public string Writable { get; set; } = string.Empty;
}
