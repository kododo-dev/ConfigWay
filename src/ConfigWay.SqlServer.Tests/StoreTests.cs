using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.SqlServer.Tests.Fixtures;
using Xunit;

namespace Kododo.ConfigWay.SqlServer.Tests;

[Collection("SqlServer")]
public class StoreTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private Store _store = null!;

    public StoreTests(SqlServerFixture fixture) => _fixture = fixture;

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
        Assert.Single(result, s => s.Key == "probe");
    }

    [Fact]
    public async Task InitializeAsync_WhenCalledTwice_IsIdempotent()
    {
        await _store.InitializeAsync();
    }

    [Fact]
    public async Task GetAll_WhenTableIsEmpty_ReturnsEmptyList()
    {
        var result = await _store.GetAllAsync();
        Assert.Empty(result);
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

        Assert.Equal(3, result.Count);
        Assert.Equivalent(new[] { "Key1", "Key2", "Key3" }, result.Select(s => s.Key), strict: true);
    }

    [Fact]
    public async Task GetAll_WhenValueIsNull_ReturnsSettingWithNullValue()
    {
        await _store.SetAsync([new Setting("NullKey", null)]);

        var result = await _store.GetAllAsync();

        Assert.Null(result.Single(s => s.Key == "NullKey").Value);
    }

    [Fact]
    public async Task Set_NewKey_InsertsRow()
    {
        await _store.SetAsync([new Setting("New", "value")]);

        var result = await _store.GetAllAsync();
        Assert.Single(result, s => s.Key == "New" && s.Value == "value");
    }

    [Fact]
    public async Task Set_ExistingKey_UpdatesValueInPlace()
    {
        await _store.SetAsync([new Setting("Key", "OldValue")]);
        await _store.SetAsync([new Setting("Key", "NewValue")]);

        var result = await _store.GetAllAsync();
        Assert.Single(result);
        Assert.Equal("NewValue", result.Single().Value);
    }

    [Fact]
    public async Task Set_ExistingKey_DoesNotDuplicateRow()
    {
        await _store.SetAsync([new Setting("Key", "A")]);
        await _store.SetAsync([new Setting("Key", "B")]);
        await _store.SetAsync([new Setting("Key", "C")]);

        var result = await _store.GetAllAsync();
        Assert.Single(result, s => s.Key == "Key");
    }

    [Fact]
    public async Task Set_NullValue_StoredAndRetrievedAsNull()
    {
        await _store.SetAsync([new Setting("Key", null)]);

        var result = await _store.GetAllAsync();
        Assert.Null(result.Single().Value);
    }

    [Fact]
    public async Task Set_NullThenNonNull_Overwrites()
    {
        await _store.SetAsync([new Setting("Key", null)]);
        await _store.SetAsync([new Setting("Key", "value")]);

        var result = await _store.GetAllAsync();
        Assert.Equal("value", result.Single().Value);
    }

    [Fact]
    public async Task Set_MultipleSettings_AllInsertedInSingleCall()
    {
        var settings = Enumerable.Range(1, 10)
            .Select(i => new Setting($"Key{i}", $"Value{i}"))
            .ToArray();

        await _store.SetAsync(settings);

        var result = await _store.GetAllAsync();
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public async Task Set_AfterPartialInsert_RollsBackAllChanges()
    {
        await _store.SetAsync([new Setting("Existing", "before")]);

        var brokenStore = new Store("Server=127.0.0.1,1;Database=x;User Id=x;Password=x;TrustServerCertificate=True;Connect Timeout=1");

        var act = async () => await brokenStore.SetAsync([new Setting("K", "V")]);
        await Assert.ThrowsAnyAsync<Exception>(act);

        var result = await _store.GetAllAsync();
        Assert.Single(result, s => s.Key == "Existing" && s.Value == "before");
    }

    [Fact]
    public async Task Set_DataPersistedAcrossStoreInstances()
    {
        await _store.SetAsync([new Setting("Persistent", "data")]);

        var freshStore = new Store(_fixture.ConnectionString);
        await freshStore.InitializeAsync();

        var result = await freshStore.GetAllAsync();
        Assert.Contains(result, s => s.Key == "Persistent" && s.Value == "data");
    }
}

[CollectionDefinition("SqlServer")]
public class SqlServerCollectionDefinition : ICollectionFixture<SqlServerFixture> { }
