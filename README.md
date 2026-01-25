# Baubit.Caching.LiteDB.DI

[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master)
[![codecov](https://codecov.io/gh/pnagoorkar/Baubit.Caching.LiteDB.DI/branch/master/graph/badge.svg)](https://codecov.io/gh/pnagoorkar/Baubit.Caching.LiteDB.DI)<br/>
[![NuGet](https://img.shields.io/nuget/v/Baubit.Caching.LiteDB.DI.svg)](https://www.nuget.org/packages/Baubit.Caching.LiteDB.DI/)
[![NuGet](https://img.shields.io/nuget/dt/Baubit.Caching.LiteDB.DI.svg)](https://www.nuget.org/packages/Baubit.Caching.LiteDB.DI) <br/>
![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)<br/>
[![Known Vulnerabilities](https://snyk.io/test/github/pnagoorkar/Baubit.Caching.LiteDB.DI/badge.svg)](https://snyk.io/test/github/pnagoorkar/Baubit.Caching.LiteDB.DI)

Dependency injection module for [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB). Registers `IOrderedCache<TId, TValue>` with LiteDB-backed L2 persistent storage and optional in-memory L1 caching in your DI container.

> **Note:** This package provides **generic modules** that can only be loaded programmatically. For configuration-based loading from appsettings.json, create a concrete module with `[BaubitModule]` attribute and register it using a `ModuleRegistry`. See [Configuration-Based Loading](https://github.com/pnagoorkar/Baubit.DI#pattern-1-modules-from-appsettingsjson) for details.

[Learn more about Baubit.DI's application creation patterns](https://github.com/pnagoorkar/Baubit.DI#application-creation-patterns)

## Installation

```bash
dotnet add package Baubit.Caching.LiteDB.DI
```

Or via NuGet Package Manager:

```
Install-Package Baubit.Caching.LiteDB.DI
```

## Breaking Changes in v2026.1.1

### Cache Interface Requires ID Type

`IOrderedCache<TValue>` changed to `IOrderedCache<TId, TValue>` to support generic ID types. This package provides two specialized modules:
- `Long.Module<TValue>`: Registers `IOrderedCache<long, TValue>` with long-based identifiers
- `Guid7.Module<TValue>`: Registers `IOrderedCache<Guid, TValue>` with time-ordered GuidV7 identifiers

**Before:**
```csharp
IOrderedCache<string> cache = serviceProvider.GetService<IOrderedCache<string>>();
```

**After:**
```csharp
IOrderedCache<long, string> cache = serviceProvider.GetService<IOrderedCache<long, string>>();
IOrderedCache<Guid, string> cache = serviceProvider.GetService<IOrderedCache<Guid, string>>();
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
        return builder.WithModule<Module<string>, Configuration>(config =>
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

### Using Guid7.Module

For Guid (GuidV7)-based cache identifiers with automatic time-ordered ID generation:

```csharp
using Baubit.Caching.LiteDB.DI.Guid7;
using Baubit.DI;
using Baubit.DI.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class AppComponent : Component
{
    protected override Result<ComponentBuilder> Build(ComponentBuilder builder)
    {
        return builder.WithModule<Module<string>, Configuration>(config =>
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
  - `Guid7.Module<TValue>`: Guid (GuidV7) identifiers with automatic time-ordered ID generation
- **Persistent L2 Storage**: LiteDB-backed L2 store for durable cache data
- **Optional L1 Caching**: Bounded in-memory L1 layer for fast lookups
- **Async Enumerator Persistence**: LiteDB-backed persistence for enumerator positions across application restarts
- **Configurable Lifetimes**: Singleton, Transient, or Scoped registration
- **Keyed Service Registration**: Register caches with a key for multi-instance scenarios
- **IConfiguration Support**: Load settings from appsettings.json or other configuration sources
- **Database Isolation**: Separate database files and collections per cache instance

## Keyed Service Registration

Register multiple cache instances with different keys for multi-tenancy or different data types.

```csharp
using Baubit.Caching.LiteDB.DI.Long;
using Baubit.DI;
using Microsoft.Extensions.DependencyInjection;

public class AppComponent : Component
{
    protected override Result<ComponentBuilder> Build(ComponentBuilder builder)
    {
        return builder.WithModule<Module<string>, Configuration>(config =>
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

Enable session resume for async enumerators to persist and resume from saved positions across application restarts:

```csharp
using Baubit.Caching.LiteDB.DI.Long;

var module = new Module<string>(new Configuration 
{ 
    DatabasePath = "cache.db",
    CollectionName = "entries",
    ResumeSession = true,              // Load existing IDs on startup to resume sessions
    PersistPositionEveryXMoves = 100,  // Persist position every 100 moves (0 = never persist)
    PersistPositionBeforeMove = true,  // When true: persists BEFORE moving (may lose last entry on crash)
                                       // When false: persists AFTER moving (better crash recovery)
    CacheLifetime = ServiceLifetime.Singleton
});
```

**Persistence Strategies:**

- **`ResumeSession = true`**: On startup, the cache loads all existing entry IDs from LiteDB to restore the full cache state
- **`PersistPositionEveryXMoves > 0`**: Async enumerator positions are saved to LiteDB at the specified interval
- **`PersistPositionBeforeMove = true`**: Position is saved before moving to next entry (default, prevents duplicate processing)
- **`PersistPositionBeforeMove = false`**: Position is saved after moving to next entry (better for crash recovery, ensures last read entry is saved)

## Configuration

The `Configuration` class controls cache behavior, storage settings, and enumerator persistence.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DatabasePath` | `string` | `"cache.db"` | Path to the LiteDB database file for L2 storage |
| `CollectionName` | `string` | `"cache"` | Name of the LiteDB collection for cache entries |
| `IncludeL1Caching` | `bool` | `false` | Enable bounded in-memory L1 caching layer for fast lookups |
| `L1MinCap` | `int` | `128` | Minimum capacity for L1 in-memory store (inherited from base) |
| `L1MaxCap` | `int` | `8192` | Maximum capacity for L1 in-memory store (inherited from base) |
| `ResumeSession` | `bool` | `false` | Load existing cache entry IDs from LiteDB on startup to restore cache state |
| `PersistPositionEveryXMoves` | `int` | `0` | Number of `MoveNext` operations before persisting enumerator position (0 = disabled) |
| `PersistPositionBeforeMove` | `bool` | `true` | If true, persist position before moving; if false, persist after moving |
| `CacheConfiguration` | `Configuration` | `null` | Underlying cache configuration (from Baubit.Caching) |
| `CacheLifetime` | `ServiceLifetime` | `Singleton` | DI service lifetime for cache registration |
| `RegistrationKey` | `string` | `null` | Optional key for keyed service registration (multi-instance support) |

## API Reference

### `Long.Module<TValue>`

DI module for registering `IOrderedCache<long, TValue>` with LiteDB-backed L2 storage. Uses long-based identifiers for numeric cache keys.

**Namespace:** `Baubit.Caching.LiteDB.DI.Long`

**Constructors:**
- `Module(IConfiguration configuration)`: Load from configuration
- `Module(Configuration configuration, List<IModule> nestedModules = null)`: Programmatic configuration

### `Guid7.Module<TValue>`

DI module for registering `IOrderedCache<Guid, TValue>` with LiteDB-backed L2 storage. Uses Guid (GuidV7) identifiers with automatic time-ordered ID generation.

**Namespace:** `Baubit.Caching.LiteDB.DI.Guid7`

**Constructors:**
- `Module(IConfiguration configuration)`: Load from configuration
- `Module(Configuration configuration, List<IModule> nestedModules = null)`: Programmatic configuration

### `Module<TId, TValue>`

Abstract base DI module for registering `IOrderedCache<TId, TValue>` with LiteDB-backed L2 storage. Provides shared LiteDB database management and metadata initialization with optional session resume support.

**Do not inherit from this directly.** Use `Long.Module<TValue>` or `Guid7.Module<TValue>` instead.

**Key Methods:**
- `GetOrCreateDatabase()`: Returns the shared LiteDB database instance
- `BuildL1DataStore(IServiceProvider)`: Abstract method for building L1 in-memory store
- `BuildL2DataStore(IServiceProvider)`: Abstract method for building L2 LiteDB-backed store
- `BuildMetadata(IServiceProvider)`: Builds metadata with optional session resume support
- `BuildCacheEnumeratorFactory(IServiceProvider)`: Builds factory for async enumerators with LiteDB persistence
- `BuildCacheEnumeratorCollectionFactory(IServiceProvider)`: Builds factory for LiteDB-aware enumerator collections

## Contributing

Contributions are welcome. Open an issue or submit a pull request.

## License

MIT License - see [LICENSE](LICENSE) file for details.
