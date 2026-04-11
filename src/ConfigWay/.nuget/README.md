# Kododo.ConfigWay

Runtime configuration editor for ASP.NET Core. View and modify `IOptions<T>` values at runtime through a built-in web UI without restarting the application.

## Install

```bash
dotnet add package Kododo.ConfigWay
dotnet add package Kododo.ConfigWay.UI
dotnet add package Kododo.ConfigWay.PostgreSQL  # optional — for persistence
```

## Setup

```csharp
builder.AddConfigWay(x =>
{
    x.AddOptions<EmailOptions>();
    x.AddOptions<AppOptions>();
    x.AddUiEditor();
    x.UsePostgreSql(builder.Configuration.GetConnectionString("DefaultConnection")!);
});

app.UseConfigWay(); // mounts UI at /config
```

## Registering options

```csharp
// Section name inferred from type — "Options" suffix stripped
x.AddOptions<EmailOptions>();   // → "Email"

// Override explicitly
x.AddOptions<EmailOptions>("Mail");
```

Nested types are supported and rendered as subsections in the UI.

## Validation

```csharp
// Data annotations
builder.Services.AddOptions<EmailOptions>().ValidateDataAnnotations();

// Custom validator
builder.Services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
```

Errors are shown in the UI and the save is blocked until resolved.

## Custom store

```csharp
builder.AddConfigWay(x =>
{
    x.Store = new MyCustomStore(); // implements IStore
    x.AddOptions<AppOptions>();
});
```

## Security

The `/config` route is not protected by default:

```csharp
app.UseConfigWay()
   .RequireAuthorization(policy => policy.RequireRole("Admin"));
```

> Use `IOptionsSnapshot<T>` or `IOptionsMonitor<T>` in your code to always get the latest values after a reload.

## Links

- Source: https://github.com/kododo-dev/ConfigWay
