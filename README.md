# Baubit.Caching.LiteDB.DI

[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master)
[![codecov](https://codecov.io/gh/pnagoorkar/Baubit.Caching.LiteDB.DI/branch/master/graph/badge.svg)](https://codecov.io/gh/pnagoorkar/Baubit.Caching.LiteDB.DI)<br/>
[![NuGet](https://img.shields.io/nuget/v/Baubit.Caching.LiteDB.DI.svg)](https://www.nuget.org/packages/Baubit.Caching.LiteDB.DI/)
[![NuGet](https://img.shields.io/nuget/dt/Baubit.Caching.LiteDB.DI.svg)](https://www.nuget.org/packages/Baubit.Caching.LiteDB.DI) <br/>
![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)<br/>
[![Known Vulnerabilities](https://snyk.io/test/github/pnagoorkar/Baubit.Caching.LiteDB.DI/badge.svg)](https://snyk.io/test/github/pnagoorkar/Baubit.Caching.LiteDB.DI)

Dependency injection module for [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB). Registers `IOrderedCache<TId, TValue>` with LiteDB-backed L2 persistent storage and optional in-memory L1 caching in your DI container.

## Installation

```bash
dotnet add package Baubit.Caching.LiteDB.DI
```

Or via NuGet Package Manager:

```
Install-Package Baubit.Caching.LiteDB.DI
```

## Quick Start

### Using Long.Module

For long-based cache identifiers:

```csharp
using Baubit.Caching.LiteDB.DI.Long;
using Baubit.DI.Extensions;
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
                          config.DatabasePath = "cache.db";
                          config.CollectionName = "entries";
                          config.CacheLifetime = ServiceLifetime.Singleton;
                      }, config => new Module<string>(config));
    }
}

// Resolve the cache
var cache = serviceProvider.GetService<IOrderedCache<long, string>>();
```

### Using Guid.Module

For Guid (GuidV7)-based cache identifiers with automatic time-ordered ID generation:

```csharp
using Baubit.Caching.LiteDB.DI.Guid;
using Baubit.DI;
using Baubit.DI.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

// Resolve the cache
var cache = serviceProvider.GetService<IOrderedCache<Guid, string>>();
```

### Using AddModule Directly

For direct service collection registration:

```csharp
using Baubit.Caching.LiteDB.DI.Long;
using Microsoft.Extensions.DependencyInjection;

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
var cache = serviceProvider.GetService<IOrderedCache<long, string>>();
```

## Features

- **Two ID Type Options**: 
  - `Long.Module<TValue>`: Long-based identifiers for numeric cache keys
  - `Guid.Module<TValue>`: Guid (GuidV7) identifiers with automatic time-ordered ID generation
- **Persistent L2 Storage**: LiteDB-backed L2 store for durable cache data
- **Optional L1 Caching**: Bounded in-memory L1 layer for fast lookups
- **Async Enumerator Persistence**: LiteDB-backed persistence for enumerator positions across application restarts
- **Configurable Lifetimes**: Singleton, Transient, or Scoped registration
- **Keyed Service Registration**: Register caches with a key for multi-instance scenarios
- **IConfiguration Support**: Load settings from appsettings.json or other configuration sources
- **Database Isolation**: Separate database files and collections per cache instance

## Keyed Service Registration

Register multiple cache instances with different keys:

```csharp
using Baubit.Caching.LiteDB.DI.Long;
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
var userCache = serviceProvider.GetKeyedService<IOrderedCache<long, string>>("user-cache");
var productCache = serviceProvider.GetKeyedService<IOrderedCache<long, string>>("product-cache");
```

## Async Enumerator Persistence

Enable session resume for async enumerators to persist and resume from saved positions:

```csharp
using Baubit.Caching.LiteDB.DI.Long;

var module = new Module<string>(new Configuration 
{ 
    DatabasePath = "cache.db",
    CollectionName = "entries",
    ResumeSession = true,              // Enable session resume
    PersistPositionEveryXMoves = 100,  // Persist every 100 moves
    PersistPositionBeforeMove = true,  // Persist before moving
    CacheLifetime = ServiceLifetime.Singleton
});
```

## Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DatabasePath` | `string` | `"cache.db"` | Path to the LiteDB database file |
| `CollectionName` | `string` | `"cache"` | Name of the collection within the database |
| `IncludeL1Caching` | `bool` | `false` | Enable bounded L1 caching layer |
| `L1MinCap` | `int` | `128` | Minimum capacity for L1 store |
| `L1MaxCap` | `int` | `8192` | Maximum capacity for L1 store |
| `ResumeSession` | `bool` | `false` | Enable async enumerator session resume |
| `PersistPositionEveryXMoves` | `int` | `0` | Persist position every X moves (0 = disabled) |
| `PersistPositionBeforeMove` | `bool` | `true` | Persist position before or after move |
| `CacheConfiguration` | `Configuration` | `null` | Underlying cache configuration |
| `CacheLifetime` | `ServiceLifetime` | `Singleton` | DI service lifetime |
| `RegistrationKey` | `string` | `null` | Key for keyed service registration |

## API Reference

### `Long.Module<TValue>`

DI module for registering `IOrderedCache<long, TValue>` with LiteDB-backed L2 storage. Uses long-based identifiers for numeric cache keys.

### `Guid.Module<TValue>`

DI module for registering `IOrderedCache<Guid, TValue>` with LiteDB-backed L2 storage. Uses Guid (GuidV7) identifiers with automatic time-ordered ID generation.

### `Module<TId, TValue>`

Abstract base DI module for registering `IOrderedCache<TId, TValue>` with LiteDB-backed L2 storage. Use `Guid.Module<TValue>` or `Long.Module<TValue>` instead of inheriting from this directly.

## Contributing

Contributions are welcome. Open an issue or submit a pull request.

## License

MIT License - see [LICENSE](LICENSE) file for details.
