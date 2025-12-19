# Baubit.Caching.LiteDB.DI

[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master)
[![codecov](https://codecov.io/gh/pnagoorkar/Baubit.Caching.LiteDB.DI/branch/master/graph/badge.svg)](https://codecov.io/gh/pnagoorkar/Baubit.Caching.LiteDB.DI)<br/>
[![NuGet](https://img.shields.io/nuget/v/Baubit.Caching.LiteDB.DI.svg)](https://www.nuget.org/packages/Baubit.Caching.LiteDB.DI/)
[![NuGet](https://img.shields.io/nuget/dt/Baubit.Caching.LiteDB.DI.svg)](https://www.nuget.org/packages/Baubit.Caching.LiteDB.DI) <br/>
![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)<br/>
[![Known Vulnerabilities](https://snyk.io/test/github/pnagoorkar/Baubit.Caching.LiteDB.DI/badge.svg)](https://snyk.io/test/github/pnagoorkar/Baubit.Caching.LiteDB.DI)

Dependency injection module for [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB). Registers `IOrderedCache<TValue>` with LiteDB-backed L2 persistent storage in your DI container.

> **Note:** This package provides a **generic module** (`Module<TValue>`) that can only be loaded programmatically. For configuration-based loading from appsettings.json, you'll need to create a concrete module with `[BaubitModule]` attribute and register it using a `ModuleRegistry`. See [Configuration-Based Loading](#configuration-based-loading-advanced) for details.

## Installation

```bash
dotnet add package Baubit.Caching.LiteDB.DI
```

Or via NuGet Package Manager:

```
Install-Package Baubit.Caching.LiteDB.DI
```

## Quick Start

### Programmatic Module Loading

Load modules programmatically using `IComponent`. This is the **recommended approach** for the generic `Module<TValue>`.

```csharp
using Baubit.Caching.LiteDB.DI;
using Baubit.DI;
using Baubit.DI.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Simple logging module for registering ILoggerFactory
public class LoggingModule : Baubit.DI.Module<Baubit.DI.Configuration>
{
    public LoggingModule(Baubit.DI.Configuration config) : base(config) { }
    
    public override void Load(IServiceCollection services)
    {
        services.AddLogging();
        base.Load(services);
    }
}

public class AppComponent : Component
{
    protected override Result<ComponentBuilder> Build(ComponentBuilder builder)
    {
        return builder.WithModule<LoggingModule, Baubit.DI.Configuration>(
                          _ => { }, 
                          config => new LoggingModule(config))
                      .WithModule<Module<string>, Configuration>(config =>
                      {
                          config.DatabasePath = "cache.db";
                          config.CollectionName = "entries";
                          config.IncludeL1Caching = true;
                          config.L1MinCap = 128;
                          config.L1MaxCap = 8192;
                          config.CacheLifetime = ServiceLifetime.Singleton;
                      }, config => new Module<string>(config));
    }
}

await Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings())
          .UseConfiguredServiceProviderFactory(componentsFactory: () => [new AppComponent()])
          .Build()
          .RunAsync();
```

### Using AddModule Directly

For direct service collection registration without the component builder.

```csharp
using Baubit.Caching.LiteDB.DI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging();

var module = new Module<string>(new Configuration 
{ 
    DatabasePath = "cache.db",
    CollectionName = "entries",
    CacheLifetime = ServiceLifetime.Singleton
});

module.Load(services);

var serviceProvider = services.BuildServiceProvider();
var cache = serviceProvider.GetService<IOrderedCache<string>>();
```

## Keyed Service Registration

Register multiple cache instances with different keys for multi-tenancy or different data types.

```csharp
using Baubit.Caching.LiteDB.DI;
using Baubit.DI;
using Microsoft.Extensions.DependencyInjection;

public class AppComponent : Component
{
    protected override Result<ComponentBuilder> Build(ComponentBuilder builder)
    {
        return builder.WithModule<LoggingModule, Baubit.DI.Configuration>(
                          _ => { }, 
                          config => new LoggingModule(config))
                      .WithModule<Module<string>, Configuration>(config =>
                      {
                          config.DatabasePath = "user-cache.db";
                          config.CollectionName = "users";
                          config.RegistrationKey = "user-cache";
                          config.CacheLifetime = ServiceLifetime.Singleton;
                      }, config => new Module<string>(config))
                      .WithModule<Module<string>, Configuration>(config =>
                      {
                          config.DatabasePath = "product-cache.db";
                          config.CollectionName = "products";
                          config.RegistrationKey = "product-cache";
                          config.CacheLifetime = ServiceLifetime.Singleton;
                      }, config => new Module<string>(config));
    }
}

// Resolve keyed services
var userCache = serviceProvider.GetKeyedService<IOrderedCache<string>>("user-cache");
var productCache = serviceProvider.GetKeyedService<IOrderedCache<string>>("product-cache");
```

## Configuration-Based Loading (Advanced)

The generic `Module<TValue>` cannot be loaded from configuration because generic type parameters cannot be specified in JSON. To enable configuration-based loading, create a **concrete module** for your specific value type.

### Step 1: Create a Concrete Module

```csharp
using Baubit.Caching.LiteDB.DI;
using Baubit.DI;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace MyApp.Caching
{
    /// <summary>
    /// Concrete LiteDB cache module for string values.
    /// Can be loaded from appsettings.json using the "litedb-string-cache" key.
    /// </summary>
    [BaubitModule("litedb-string-cache")]
    public class LiteDBStringCacheModule : Module<string>
    {
        // Constructor for configuration-based loading
        public LiteDBStringCacheModule(IConfiguration configuration) 
            : base(configuration) { }
        
        // Constructor for programmatic loading
        public LiteDBStringCacheModule(Configuration configuration, List<IModule> nestedModules = null) 
            : base(configuration, nestedModules) { }
    }
}
```

### Step 2: Create Module Registry

```csharp
using Baubit.DI;

namespace MyApp.Caching
{
    /// <summary>
    /// Module registry for MyApp caching modules.
    /// MUST call Register() before UseConfiguredServiceProviderFactory().
    /// </summary>
    [GeneratedModuleRegistry]
    internal static partial class CachingModuleRegistry
    {
        // Register() method will be generated automatically
    }
}
```

### Step 3: Register and Load

> **CRITICAL:** You **MUST** call `CachingModuleRegistry.Register()` before `UseConfiguredServiceProviderFactory()`. This registers your modules with Baubit.DI's module registry. **Forgetting this step will cause your modules to not be found**, leading to frustrating runtime errors.

```csharp
using MyApp.Caching;
using Microsoft.Extensions.Hosting;

// REQUIRED: Register modules before loading
CachingModuleRegistry.Register();

await Host.CreateApplicationBuilder()
          .UseConfiguredServiceProviderFactory()
          .Build()
          .RunAsync();
```

**appsettings.json:**
```json
{
  "modules": [
    {
      "key": "litedb-string-cache",
      "configuration": {
        "databasePath": "my-cache.db",
        "collectionName": "entries",
        "includeL1Caching": true,
        "l1MinCap": 128,
        "l1MaxCap": 8192,
        "cacheLifetime": "Singleton",
        "registrationKey": "my-cache"
      }
    }
  ]
}
```

### Step 4: Hybrid Loading

Combine configuration-based and programmatic loading:

```csharp
using MyApp.Caching;

// MUST call Register() first
CachingModuleRegistry.Register();

await Host.CreateApplicationBuilder()
          .UseConfiguredServiceProviderFactory(componentsFactory: () => [new AppComponent()])
          .Build()
          .RunAsync();
```

## Features

- **Persistent L2 Storage**: LiteDB-backed L2 store for durable cache data
- **Optional L1 Caching**: Bounded in-memory L1 layer for fast lookups
- **Configurable Lifetimes**: Singleton, Transient, or Scoped registration
- **Keyed Service Registration**: Register caches with a key for multi-instance scenarios
- **IConfiguration Support**: Load settings from appsettings.json or other configuration sources
- **Database Isolation**: Separate database files and collections per cache instance

## Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DatabasePath` | `string` | `"cache.db"` | Path to the LiteDB database file |
| `CollectionName` | `string` | `"cache"` | Name of the collection within the database |
| `IncludeL1Caching` | `bool` | `false` | Enable bounded L1 caching layer |
| `L1MinCap` | `int` | `128` | Minimum capacity for L1 store |
| `L1MaxCap` | `int` | `8192` | Maximum capacity for L1 store |
| `CacheConfiguration` | `Configuration` | `null` | Underlying cache configuration |
| `CacheLifetime` | `ServiceLifetime` | `Singleton` | DI service lifetime |
| `RegistrationKey` | `string` | `null` | Key for keyed service registration |

## API Reference

### `Configuration`

Configuration class for the LiteDB caching module. Extends `Configuration` from Baubit.Caching.DI.

### `Module<TValue>`

DI module for registering `IOrderedCache<TValue>` with LiteDB-backed L2 storage. Uses in-memory store for L1 (when enabled) and LiteDB store for L2.

## Dependencies

- [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB)
- [Baubit.DI.Extensions](https://github.com/pnagoorkar/Baubit.DI.Extensions)

## Contributing

Contributions are welcome. Open an issue or submit a pull request.

## License

MIT License - see [LICENSE](LICENSE) file for details.
