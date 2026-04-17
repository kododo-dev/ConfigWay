using FluentAssertions;
using Kododo.ConfigWay.Core.Model;
using Xunit;

namespace Kododo.ConfigWay.Tests.Unit;

public class InMemoryStoreTests
{
    private readonly InMemoryStore _store = new();

    public InMemoryStoreTests() =>
        _store.InitializeAsync().GetAwaiter().GetResult();

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsEmptyList()
    {
        var result = await _store.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_AfterSet_ReturnsStoredSettings()
    {
        await _store.SetAsync([new Setting("Key1", "Value1"), new Setting("Key2", "Value2")]);

        var result = await _store.GetAllAsync();

        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Key == "Key1" && s.Value == "Value1");
        result.Should().Contain(s => s.Key == "Key2" && s.Value == "Value2");
    }

    // ── Set ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Set_ExistingKey_OverwritesValue()
    {
        await _store.SetAsync([new Setting("Key", "OldValue")]);
        await _store.SetAsync([new Setting("Key", "NewValue")]);

        var result = await _store.GetAllAsync();

        result.Should().ContainSingle();
        result.Single().Value.Should().Be("NewValue");
    }

    [Fact]
    public async Task Set_ExistingKey_DoesNotDuplicateRow()
    {
        await _store.SetAsync([new Setting("Key", "A")]);
        await _store.SetAsync([new Setting("Key", "B")]);
        await _store.SetAsync([new Setting("Key", "C")]);

        var result = await _store.GetAllAsync();
        result.Should().ContainSingle(s => s.Key == "Key");
    }

    [Fact]
    public async Task Set_NullValue_StoredAndRetrievedAsNull()
    {
        await _store.SetAsync([new Setting("Key", null)]);

        var result = await _store.GetAllAsync();
        result.Single(s => s.Key == "Key").Value.Should().BeNull();
    }

    [Fact]
    public async Task Set_NullThenNonNull_Overwrites()
    {
        await _store.SetAsync([new Setting("Key", null)]);
        await _store.SetAsync([new Setting("Key", "Value")]);

        var result = await _store.GetAllAsync();
        result.Single(s => s.Key == "Key").Value.Should().Be("Value");
    }

    [Fact]
    public async Task Set_MultipleSettings_AllInserted()
    {
        var settings = Enumerable.Range(1, 5)
            .Select(i => new Setting($"Key{i}", $"Value{i}"))
            .ToArray();

        await _store.SetAsync(settings);

        var result = await _store.GetAllAsync();
        result.Should().HaveCount(5);
    }
}
