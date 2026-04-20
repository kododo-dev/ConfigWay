# Kododo.ConfigWay.Core

Core abstractions and interfaces for [Kododo.ConfigWay](https://www.nuget.org/packages/Kododo.ConfigWay) — the runtime configuration editor for ASP.NET Core.

## When to reference this package

You need this package **only if you are building an extension for ConfigWay**, such as:

- a custom persistence store (`IStore`)
- a custom provider or integration

Application developers should install `Kododo.ConfigWay` instead — it already pulls in this package transitively.

## Install

```bash
dotnet add package Kododo.ConfigWay.Core
```

## Key abstractions

### `IStore`

Implement this interface to provide a custom persistence backend:

```csharp
public interface IStore
{
    Task InitializeAsync(CancellationToken stoppingToken = default);
    Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken stoppingToken = default);
    Task SetAsync(IReadOnlyCollection<Setting> settings, CancellationToken stoppingToken = default);
    Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken stoppingToken = default);
}
```

Register your store:

```csharp
builder.AddConfigWay(x =>
{
    x.Store = new MyCustomStore(); // implements IStore
    x.AddOptions<AppOptions>();
});
```

## Related packages

| Package | Purpose |
|---|---|
| [Kododo.ConfigWay](https://www.nuget.org/packages/Kododo.ConfigWay) | Main package — DI registration, `AddConfigWay` |
| [Kododo.ConfigWay.UI](https://www.nuget.org/packages/Kododo.ConfigWay.UI) | Embedded web UI |
| [Kododo.ConfigWay.PostgreSQL](https://www.nuget.org/packages/Kododo.ConfigWay.PostgreSQL) | PostgreSQL persistence store |

## Links

- Source: https://github.com/kododo-dev/ConfigWay
