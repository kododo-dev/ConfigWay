# Kododo.ConfigWay.PostgreSQL

PostgreSQL persistence store for [Kododo.ConfigWay](https://www.nuget.org/packages/Kododo.ConfigWay). Without this package, ConfigWay uses an in-memory store and settings are lost on restart.

## Install

```bash
dotnet add package Kododo.ConfigWay
dotnet add package Kododo.ConfigWay.PostgreSQL
```

## Setup

```csharp
builder.AddConfigWay(x =>
{
    x.AddOptions<EmailOptions>();
    x.UsePostgreSql(builder.Configuration.GetConnectionString("DefaultConnection")!);
});
```

## What gets created

On first startup the store automatically creates its schema and table:

```sql
CREATE SCHEMA IF NOT EXISTS "configway";

CREATE TABLE IF NOT EXISTS configway.settings (
    key   TEXT NOT NULL,
    value TEXT NULL,
    CONSTRAINT pk_settings PRIMARY KEY (key)
);
```

No migrations required. Safe to run on every startup.

## Requirements

- PostgreSQL 12 or later
- The database user needs `CREATE` privileges for schema creation on first run

## Links

- Source: https://github.com/kododo-dev/ConfigWay
