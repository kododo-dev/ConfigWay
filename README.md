# ConfigWay

[![CI](https://github.com/kododo-dev/ConfigWay/actions/workflows/ci.yml/badge.svg)](https://github.com/kododo-dev/ConfigWay/actions/workflows/ci.yml)

Runtime configuration editor for ASP.NET Core. View and modify `IOptions<T>` values at runtime through a built-in web UI without restarting the application.

## Packages

| Package | NuGet | Description |
|---|---|---|
| `Kododo.ConfigWay` | [![NuGet](https://img.shields.io/nuget/v/Kododo.ConfigWay)](https://www.nuget.org/packages/Kododo.ConfigWay) | Core DI registration and in-memory store |
| `Kododo.ConfigWay.Core` | [![NuGet](https://img.shields.io/nuget/v/Kododo.ConfigWay.Core)](https://www.nuget.org/packages/Kododo.ConfigWay.Core) | Abstractions and interfaces (for extension authors) |
| `Kododo.ConfigWay.UI` | [![NuGet](https://img.shields.io/nuget/v/Kododo.ConfigWay.UI)](https://www.nuget.org/packages/Kododo.ConfigWay.UI) | Embedded web UI |
| `Kododo.ConfigWay.PostgreSQL` | [![NuGet](https://img.shields.io/nuget/v/Kododo.ConfigWay.PostgreSQL)](https://www.nuget.org/packages/Kododo.ConfigWay.PostgreSQL) | PostgreSQL persistence store |

## Quick start

```bash
dotnet add package Kododo.ConfigWay
dotnet add package Kododo.ConfigWay.UI
dotnet add package Kododo.ConfigWay.PostgreSQL  # optional — for persistence
```

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

Nested types are supported and rendered as collapsible subsections in the UI.

## Customizing UI labels and descriptions

Use `[Display]` from `System.ComponentModel.DataAnnotations` to control how options appear in the UI.

```csharp
using System.ComponentModel.DataAnnotations;

[Display(Name = "E-mail", Description = "Outgoing mail server settings")]
public class EmailOptions
{
    [Display(Name = "SMTP Server", Description = "Hostname of the mail server, e.g. smtp.gmail.com")]
    public string SmtpServer { get; set; } = string.Empty;

    [Display(Name = "Sender address")]
    public string SenderEmail { get; set; } = string.Empty;

    [Display(Name = "Credentials")]
    public Credentials Credentials { get; set; } = new();
}
```

`Name` overrides the label shown next to the field or in the section header. When omitted, the property name is used as-is.

`Description` renders a small **ⓘ** icon next to the label — hovering over it shows the description as a tooltip. Works on both fields and sections.

## Validation

```csharp
// Data annotations
builder.Services.AddOptions<EmailOptions>().ValidateDataAnnotations();

// Custom validator
builder.Services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
```

Errors are shown in the UI and saving is blocked until they are resolved.

## Security

The `/config` route is not protected by default:

```csharp
app.UseConfigWay()
   .RequireAuthorization(policy => policy.RequireRole("Admin"));
```

> Use `IOptionsSnapshot<T>` or `IOptionsMonitor<T>` in your code to always get the latest values after a reload.

## Custom store

You can replace the built-in in-memory store with any backend by implementing `IStore` from `Kododo.ConfigWay.Core`:

```csharp
builder.AddConfigWay(x =>
{
    x.Store = new MyCustomStore(); // implements IStore
    x.AddOptions<AppOptions>();
});
```

## Project structure

```
src/
├── ConfigWay.Core/          # Abstractions: IStore and shared models
├── ConfigWay/               # Main package: DI registration, in-memory store
├── ConfigWay.UI/            # Embedded SPA web UI
├── ConfigWay.PostgreSQL/    # PostgreSQL IStore implementation
└── ConfigWay.Demo.Web/      # Demo application
```

## License

MIT
