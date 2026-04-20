using FluentAssertions;
using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.PostgreSQL.Tests.Fixtures;
using Xunit;

namespace Kododo.ConfigWay.PostgreSQL.Tests;

[Collection("PostgreSQL")]
public class StoreTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private Store _store = null!;

    public StoreTests(PostgreSqlFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _store = new Store(_fixture.ConnectionString);
        await _store.InitializeAsync();
        await _fixture.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InitializeAsync_CreatesSchemaAndTable()
    {
        await _store.SetAsync([new Setting("probe", "ok")]);
        var result = await _store.GetAllAsync();
        result.Should().ContainSingle(s => s.Key == "probe");
    }

    [Fact]
    public async Task InitializeAsync_WhenCalledTwice_IsIdempotent()
    {
        var act = async () => await _store.InitializeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAll_WhenTableIsEmpty_ReturnsEmptyList()
    {
        var result = await _store.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_ReturnsAllStoredRows()
    {
        await _store.SetAsync([
            new Setting("Key1", "Value1"),
            new Setting("Key2", "Value2"),
            new Setting("Key3", "Value3"),
        ]);

        var result = await _store.GetAllAsync();

        result.Should().HaveCount(3);
        result.Select(s => s.Key).Should().BeEquivalentTo("Key1", "Key2", "Key3");
    }

    [Fact]
    public async Task GetAll_WhenValueIsNull_ReturnsSettingWithNullValue()
    {
        await _store.SetAsync([new Setting("NullKey", null)]);

        var result = await _store.GetAllAsync();

        result.Single(s => s.Key == "NullKey").Value.Should().BeNull();
    }

    [Fact]
    public async Task Set_NewKey_InsertsRow()
    {
        await _store.SetAsync([new Setting("New", "value")]);

        var result = await _store.GetAllAsync();
        result.Should().ContainSingle(s => s.Key == "New" && s.Value == "value");
    }

    [Fact]
    public async Task Set_ExistingKey_UpdatesValueInPlace()
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
        result.Single().Value.Should().BeNull();
    }

    [Fact]
    public async Task Set_NullThenNonNull_Overwrites()
    {
        await _store.SetAsync([new Setting("Key", null)]);
        await _store.SetAsync([new Setting("Key", "value")]);

        var result = await _store.GetAllAsync();
        result.Single().Value.Should().Be("value");
    }

    [Fact]
    public async Task Set_MultipleSettings_AllInsertedInSingleCall()
    {
        var settings = Enumerable.Range(1, 10)
            .Select(i => new Setting($"Key{i}", $"Value{i}"))
            .ToArray();

        await _store.SetAsync(settings);

        var result = await _store.GetAllAsync();
        result.Should().HaveCount(10);
    }

    [Fact]
    public async Task Set_AfterPartialInsert_RollsBackAllChanges()
    {
        await _store.SetAsync([new Setting("Existing", "before")]);

        var brokenStore = new Store("Host=127.0.0.1;Port=9;Database=x;Username=x;Password=x");

        var act = async () => await brokenStore.SetAsync([new Setting("K", "V")]);
        await act.Should().ThrowAsync<Exception>();

        var result = await _store.GetAllAsync();
        result.Should().ContainSingle(s => s.Key == "Existing" && s.Value == "before");
    }

    [Fact]
    public async Task Set_DataPersistedAcrossStoreInstances()
    {
        await _store.SetAsync([new Setting("Persistent", "data")]);

        var freshStore = new Store(_fixture.ConnectionString);
        await freshStore.InitializeAsync();

        var result = await freshStore.GetAllAsync();
        result.Should().Contain(s => s.Key == "Persistent" && s.Value == "data");
    }
}

[CollectionDefinition("PostgreSQL")]
public class PostgreSqlCollectionDefinition : ICollectionFixture<PostgreSqlFixture> { }
