# Kododo.ConfigWay.SqlServer

SQL Server persistence store for [Kododo.ConfigWay](https://www.nuget.org/packages/Kododo.ConfigWay). Without this package, ConfigWay uses an in-memory store and settings are lost on restart.

## Install

```bash
dotnet add package Kododo.ConfigWay
dotnet add package Kododo.ConfigWay.SqlServer
```

## Setup

```csharp
builder.AddConfigWay(x =>
{
    x.AddOptions<EmailOptions>();
    x.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);
});
```

## What gets created

On first startup the store automatically creates its schema and table:

```sql
CREATE SCHEMA configway; -- if it does not already exist

CREATE TABLE configway.settings (
    [key]   NVARCHAR(450) NOT NULL,
    [value] NVARCHAR(MAX) NULL,
    CONSTRAINT pk_settings PRIMARY KEY ([key])
);
```

No migrations required. Safe to run on every startup.

## Requirements

- SQL Server 2016 or later (or Azure SQL Database)
- The database user needs `CREATE SCHEMA` and `CREATE TABLE` privileges on first run

## Links

- Source: https://github.com/kododo-dev/ConfigWay
