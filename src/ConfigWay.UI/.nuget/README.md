# Kododo.ConfigWay.UI

Embedded web UI for [Kododo.ConfigWay](https://www.nuget.org/packages/Kododo.ConfigWay) — view and edit `IOptions<T>` at runtime through a browser.

## Install

```bash
dotnet add package Kododo.ConfigWay
dotnet add package Kododo.ConfigWay.UI
```

## Setup

```csharp
builder.AddConfigWay(x =>
{
    x.AddOptions<EmailOptions>();
    x.AddUiEditor();   // registers UI handlers
});

app.UseConfigWay();      // mounts UI at /config
```

## Custom path

```csharp
app.UseConfigWay("/admin/config");
```

## Authorization

`UseConfigWay()` returns a `RouteGroupBuilder`:

```csharp
app.UseConfigWay()
   .RequireAuthorization(policy => policy.RequireRole("Admin"));
```

## Features

- All registered sections in one view, or individual section pages via sidebar
- Nested option objects rendered as collapsible subsections with depth-based visual hierarchy
- Collection properties (`T[]`, `List<T>`, …) rendered as collapsible array editors with add and remove buttons; items that come from lower config layers (appsettings.json, env vars) are non-deletable
- Save / Discard in the page header — active only when changes are pending
- Validation errors shown in the sticky header
- Search across section names, field names and values with inline highlighting
- Field type-aware controls: text input for `string`, toggle switch for `bool`, numeric input for numeric types, dropdown for `enum`
- Dark / light theme, persisted to `localStorage`
- Language follows browser preference (English and Polish)

## Links

- Source: https://github.com/kododo-dev/ConfigWay
