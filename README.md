# ConfigWay

[![CI](https://github.com/kododo-dev/ConfigWay/actions/workflows/ci.yml/badge.svg)](https://github.com/kododo-dev/ConfigWay/actions/workflows/ci.yml)
[![Demo](https://img.shields.io/badge/demo-live-brightgreen)](https://kododo.dev/configway/demo)

Runtime configuration editor for ASP.NET Core. View and modify `IOptions<T>` values at runtime through a built-in web UI without restarting the application.

A live demo is available at [kododo.dev/configway/demo](https://kododo.dev/configway/demo).

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

## Supported field types

ConfigWay maps C# property types to dedicated UI controls automatically:

| C# type | UI control |
|---|---|
| `string` | Text input |
| `bool` | Toggle switch |
| `int`, `long`, `double`, `decimal`, … | Numeric input |
| `enum` | Dropdown select |
| `T[]`, `List<T>`, `IList<T>`, … | Collapsible array editor |

Nullable variants (`bool?`, `int?`, etc.) are handled the same way.

## Array types

Collection properties (`T[]`, `List<T>`, `IList<T>`, `IEnumerable<T>`, `IReadOnlyList<T>`, `ICollection<T>`, `IReadOnlyCollection<T>`) are rendered as a collapsible array editor with add and remove buttons.

**Simple arrays** — scalar element types (`string[]`, `int[]`, `Severity[]`, …) show one input field per item:

```csharp
public class WebhooksOptions
{
    [Display(Name = "Allowed Origins")]
    public string[] AllowedOrigins { get; set; } = [];
}
```

**Complex arrays** — class element types show a full sub-form per item, supporting nested objects and all scalar field types:

```csharp
public class WebhookEndpoint
{
    public string Url    { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public WebhookEvent Event { get; set; }
}

public class WebhooksOptions
{
    [Display(Name = "Endpoints")]
    public WebhookEndpoint[] Endpoints { get; set; } = [];
}
```

Items that come from lower configuration layers (appsettings.json, environment variables) are marked as non-deletable — they can be edited but not removed, because deleting them from the ConfigWay store would not suppress the underlying value.

## Reset to default

Every field, section and array exposes a **↩ reset** button that appears when the current value differs from the value in the underlying configuration layers (appsettings.json, environment variables, etc.).

Resetting removes the ConfigWay-stored override so the original value from those lower layers takes effect again, without restarting the application.

| Scope | Behaviour |
|---|---|
| **Field** | Removes the single key from the ConfigWay store. The ↩ button is shown only when the field differs from its base-config value. |
| **Section** | Resets all fields and arrays inside the section recursively. Items added via ConfigWay are removed; non-deletable items are reset to their base-config values. |
| **Array** | Items added via ConfigWay are removed. Non-deletable items (those that exist in lower config layers) have their values reset to the base-config value. |

Pending resets are batched with any other edits and applied together when the **Save** button is pressed. Pressing **Discard** also discards pending resets.

## Sensitive fields

Mark any `string` property with `[DataType(DataType.Password)]` from `System.ComponentModel.DataAnnotations` to treat it as a sensitive field:

```csharp
using System.ComponentModel.DataAnnotations;

public class SmtpCredentials
{
    public string Username { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
```

Sensitive fields behave as follows:

- **UI** — the input renders as a password field (`●●●●●`). A placeholder indicates whether a value is currently stored.
- **API** — the real value is never returned. When a value is stored, the API returns `"***"`. When nothing is stored, it returns `null`.
- **Saving** — submitting an empty value for a sensitive field is treated as "no change". To remove a stored sensitive value, use the ↩ reset button.
- **Reset** — the ↩ button appears when a sensitive value is stored. Resetting deletes the stored value; the underlying configuration layer (appsettings.json, environment variable) takes effect again without a restart.

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

`[Display]` on enum members controls the label shown in the dropdown. The underlying member name is still used as the stored value.

```csharp
public enum StorageProvider
{
    [Display(Name = "Amazon S3")]
    S3,

    [Display(Name = "Azure Blob Storage")]
    AzureBlob,

    [Display(Name = "Google Cloud Storage")]
    Gcs,
}
```

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
