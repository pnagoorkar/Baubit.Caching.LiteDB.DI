# Baubit.Caching.LiteDB.DI

[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master)
[![codecov](https://codecov.io/gh/pnagoorkar/Baubit.Caching.LiteDB.DI/branch/master/graph/badge.svg)](https://codecov.io/gh/pnagoorkar/Baubit.Caching.LiteDB.DI)<br/>
[![NuGet](https://img.shields.io/nuget/v/Baubit.Caching.LiteDB.DI.svg)](https://www.nuget.org/packages/Baubit.Caching.LiteDB.DI/)
[![NuGet](https://img.shields.io/nuget/dt/Baubit.Caching.LiteDB.DI.svg)](https://www.nuget.org/packages/Baubit.Caching.LiteDB.DI) <br/>
![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)<br/>
[![Known Vulnerabilities](https://snyk.io/test/github/pnagoorkar/Baubit.Caching.LiteDB.DI/badge.svg)](https://snyk.io/test/github/pnagoorkar/Baubit.Caching.LiteDB.DI)

Dependency injection module for [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB). Registers `IOrderedCache<TValue>` with LiteDB-backed L2 persistent storage in your DI container.

## Installation

```bash
dotnet add package Baubit.Caching.LiteDB.DI
```

Or via NuGet Package Manager:

```
Install-Package Baubit.Caching.LiteDB.DI
```

## Quick Start

### Pattern 1: Modules from appsettings.json

Load modules from configuration. Module types and settings are defined in JSON.

```csharp
await Host.CreateApplicationBuilder()
          .UseConfiguredServiceProviderFactory()
          .Build()
          .RunAsync();
```

**appsettings.json:**
```json
{
  "type": "Baubit.Caching.LiteDB.DI.LiteDB.Module`1[[System.String]], Baubit.Caching.LiteDB.DI",
  "configuration": {
    "databasePath": "my-cache.db",
    "collectionName": "entries",
    "includeL1Caching": true,
    "l1MinCap": 128,
    "l1MaxCap": 8192,
    "cacheLifetime": "Singleton",
    "registrationKey": "my-cache",
    "modules": [
      {
        "type": "MyApp.LoggingModule, MyApp",
        "configuration": {}
      }
    ]
  }
}
```

### Pattern 2: Modules from Code (IComponent)

Load modules programmatically using `IComponent`.

```csharp
public class AppComponent : Component
{
    protected override Result<ComponentBuilder> Build(ComponentBuilder builder)
    {
        return builder.WithModule<LoggingModule, LoggingConfiguration>(
                          ConfigureLogging, 
                          config => new LoggingModule(config))
                      .WithModule<Module<string>, Configuration>(
                          ConfigureCaching,
                          config => new Module<string>(config));
    }
    
    private void ConfigureLogging(LoggingConfiguration config) 
    {
        // configure as needed
    }
    
    private void ConfigureCaching(Configuration config) 
    {
        config.DatabasePath = "cache.db";
        config.CollectionName = "entries";
        config.IncludeL1Caching = true;
        config.CacheLifetime = ServiceLifetime.Singleton;
    }
}

await Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings())
          .UseConfiguredServiceProviderFactory(componentsFactory: () => [new AppComponent()])
          .Build()
          .RunAsync();
```

### Pattern 3: Hybrid Loading (appsettings.json + IComponent)

Combine configuration-based and code-based module loading.

```csharp
await Host.CreateApplicationBuilder()
          .UseConfiguredServiceProviderFactory(componentsFactory: () => [new AppComponent()])
          .Build()
          .RunAsync();
```

### Using AddModule Directly

```csharp
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
public class AppComponent : Component
{
    protected override Result<ComponentBuilder> Build(ComponentBuilder builder)
    {
        return builder.WithModule<LoggingModule, LoggingConfiguration>(
                          ConfigureLogging, 
                          config => new LoggingModule(config))
                      .WithModule<Module<string>, Configuration>(config =>
                      {
                          config.DatabasePath = "user-cache.db";
                          config.RegistrationKey = "user-cache";
                          config.CacheLifetime = ServiceLifetime.Singleton;
                      }, config => new Module<string>(config))
                      .WithModule<Module<string>, Configuration>(config =>
                      {
                          config.DatabasePath = "product-cache.db";
                          config.RegistrationKey = "product-cache";
                          config.CacheLifetime = ServiceLifetime.Singleton;
                      }, config => new Module<string>(config));
    }
}

// Resolve keyed services
var userCache = serviceProvider.GetKeyedService<IOrderedCache<string>>("user-cache");
var productCache = serviceProvider.GetKeyedService<IOrderedCache<string>>("product-cache");
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
