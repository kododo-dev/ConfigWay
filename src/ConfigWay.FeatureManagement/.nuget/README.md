# Kododo.ConfigWay.FeatureManagement

Feature flags for ConfigWay. Manage [Microsoft.FeatureManagement](https://learn.microsoft.com/azure/azure-app-configuration/feature-management-dotnet-reference) flags from the ConfigWay web UI without redeploying: turn flags on and off, roll them out to a percentage of users, limit them to a time window or target users and groups.

Your code does not change: it keeps using `IFeatureManager`, `[FeatureGate]` and the other feature management APIs.

## Install

```bash
dotnet add package Kododo.ConfigWay.FeatureManagement
```

## Setup

```csharp
builder.AddConfigWay(x =>
{
    x.UsePostgreSql(connectionString);
    x.AddUiEditor();
    x.AddFeatureFlags();
});

builder.Services.AddFeatureManagement();

app.UseConfigWay().RequireAuthorization("Admin");
```

Flags defined in your configuration (the `feature_management` section of appsettings, environment variables, ...) are listed in the editor. A flag saved in the editor is kept in the ConfigWay store and takes the place of the flag with the same id; resetting it brings the configured flag back. Flags can also be added in the editor only.

## Filters

The editor has forms for the built-in filters: `Microsoft.Percentage`, `Microsoft.TimeWindow` and `Microsoft.Targeting`. Other filters are edited as JSON parameters; a filter your application has not registered is refused, because checking the flag would throw. Variants and other properties the editor does not edit yet are kept when a flag is saved.

To check a flag for a given user and groups, use the check in the editor: it asks your application's feature manager.

## Several replicas

A save reaches the other replicas when they check the store for changes:

```csharp
x.ReloadInterval = TimeSpan.FromSeconds(30);
```

## Links

- Source: https://github.com/kododo-dev/ConfigWay
