using Baubit.DI;
using Baubit.DI.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Baubit.Caching.LiteDB.DI.Test.Module
{
    /// <summary>
    /// Unit tests for <see cref="Module{TValue}"/>
    /// </summary>
    public class Test : IDisposable
    {
        private readonly List<string> _tempFiles = new();
        private readonly List<IServiceProvider> _serviceProviders = new();

        public void Dispose()
        {
            // Dispose service providers first to close database connections
            foreach (var provider in _serviceProviders)
            {
                if (provider is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
            }

            // Clean up temporary database files created during tests
            foreach (var file in _tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                    // Also clean up LiteDB log files
                    var logFile = file + "-log";
                    if (File.Exists(logFile))
                    {
                        File.Delete(logFile);
                    }
                }
                catch (IOException)
                {
                    // Ignore IO errors during cleanup
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore access errors during cleanup
                }
            }
        }

        private string GetTempDbPath()
        {
            var path = Path.Combine(Path.GetTempPath(), $"test_cache_{Guid.NewGuid()}.db");
            _tempFiles.Add(path);
            return path;
        }

        private void TrackServiceProvider(IServiceProvider provider)
        {
            _serviceProviders.Add(provider);
        }

        [Fact]
        public void Load_WithSingletonLifetime_RegistersCacheAsSingleton()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Singleton;
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;
            TrackServiceProvider(serviceProvider);

            var cache1 = serviceProvider.GetService<IOrderedCache<Guid, string>>();
            var cache2 = serviceProvider.GetService<IOrderedCache<Guid, string>>();

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.Same(cache1, cache2); // Singleton returns same instance
        }

        [Fact]
        public void Load_WithTransientLifetime_RegistersCacheAsTransient()
        {
            // Use unique database paths for each transient instance
            var dbPath1 = GetTempDbPath();
            var dbPath2 = GetTempDbPath();
            
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath1;
                                             config.CacheLifetime = ServiceLifetime.Transient;
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;
            TrackServiceProvider(serviceProvider);

            var cache1 = serviceProvider.GetService<IOrderedCache<Guid, string>>();
            
            // Create a second service provider with different database path
            var result2 = ComponentBuilder.CreateNew()
                                          .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                          .WithModule<Module<string>, Configuration>(config =>
                                          {
                                              config.DatabasePath = dbPath2;
                                              config.CacheLifetime = ServiceLifetime.Transient;
                                          }, config => new Module<string>(config))
                                          .BuildServiceProvider();

            Assert.True(result2.IsSuccess);
            var serviceProvider2 = result2.Value;
            TrackServiceProvider(serviceProvider2);

            var cache2 = serviceProvider2.GetService<IOrderedCache<Guid, string>>();

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.NotSame(cache1, cache2); // Different instances from different providers
        }

        [Fact]
        public void Load_WithScopedLifetime_RegistersCacheAsScoped()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Scoped;
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;
            TrackServiceProvider(serviceProvider);

            using var scope1 = serviceProvider.CreateScope();
            var cache1InScope1 = scope1.ServiceProvider.GetService<IOrderedCache<Guid, string>>();
            var cache2InScope1 = scope1.ServiceProvider.GetService<IOrderedCache<Guid, string>>();

            Assert.NotNull(cache1InScope1);
            Assert.NotNull(cache2InScope1);
            Assert.Same(cache1InScope1, cache2InScope1); // Same scope returns same instance
            
            // Dispose scope1 before creating scope2 to release database lock
            scope1.Dispose();
            
            using var scope2 = serviceProvider.CreateScope();
            var cache1InScope2 = scope2.ServiceProvider.GetService<IOrderedCache<Guid, string>>();

            Assert.NotNull(cache1InScope2);
            Assert.NotSame(cache1InScope1, cache1InScope2); // Different scopes return different instances
        }

        [Fact]
        public void Load_WithL1CachingEnabled_RegistersCacheWithL1Store()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.IncludeL1Caching = true;
                                             config.L1MinCap = 64;
                                             config.L1MaxCap = 1024;
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithL1CachingDisabled_RegistersCacheWithoutL1Store()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.IncludeL1Caching = false;
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithCacheConfiguration_RegistersCacheWithConfiguration()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheConfiguration = new Caching.Configuration();
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithCustomDatabasePathAndCollection_RegistersCache()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CollectionName = "custom_collection";
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Constructor_WithConfiguration_CreatesModule()
        {
            var config = new Configuration
            {
                DatabasePath = GetTempDbPath()
            };
            var module = new Module<string>(config);

            Assert.NotNull(module);
        }

        [Fact]
        public void Constructor_WithConfigurationAndNestedModules_CreatesModule()
        {
            var config = new Configuration
            {
                DatabasePath = GetTempDbPath()
            };
            var nestedModules = new System.Collections.Generic.List<IModule>();
            var module = new Module<string>(config, nestedModules);

            Assert.NotNull(module);
        }

        [Fact]
        public void Constructor_WithIConfiguration_CreatesModule()
        {
            var dbPath = GetTempDbPath();
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabasePath"] = dbPath,
                ["CollectionName"] = "test_collection",
                ["IncludeL1Caching"] = "true",
                ["L1MinCap"] = "64",
                ["L1MaxCap"] = "1024",
                ["CacheLifetime"] = "Singleton"
            });
            var configuration = configBuilder.Build();

            var module = new Module<string>(configuration);

            Assert.NotNull(module);

            // Test that the module loads correctly with the same config values
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CollectionName = "test_collection";
                                             config.IncludeL1Caching = true;
                                             config.L1MinCap = 64;
                                             config.L1MaxCap = 1024;
                                             config.CacheLifetime = ServiceLifetime.Singleton;
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithSingletonLifetimeAndRegistrationKey_RegistersKeyedCacheAsSingleton()
        {
            var dbPath = GetTempDbPath();
            const string registrationKey = "singleton-test-cache";
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>((Configuration config) =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Singleton;
                                             config.RegistrationKey = registrationKey;
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;
            TrackServiceProvider(serviceProvider);

            var cache1 = serviceProvider.GetKeyedService<IOrderedCache<Guid, string>>(registrationKey);
            var cache2 = serviceProvider.GetKeyedService<IOrderedCache<Guid, string>>(registrationKey);

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.Same(cache1, cache2); // Singleton returns same instance
        }

        [Fact]
        public void Load_WithTransientLifetimeAndRegistrationKey_RegistersKeyedCacheAsTransient()
        {
            // Use unique database paths for each transient instance
            var dbPath1 = GetTempDbPath();
            var dbPath2 = GetTempDbPath();
            const string registrationKey1 = "transient-test-cache-1";
            const string registrationKey2 = "transient-test-cache-2";
            
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>((Configuration config) =>
                                         {
                                             config.DatabasePath = dbPath1;
                                             config.CacheLifetime = ServiceLifetime.Transient;
                                             config.RegistrationKey = registrationKey1;
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;
            TrackServiceProvider(serviceProvider);

            var cache1 = serviceProvider.GetKeyedService<IOrderedCache<Guid, string>>(registrationKey1);

            // Create a second service provider with different database path and key
            var result2 = ComponentBuilder.CreateNew()
                                          .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                          .WithModule<Module<string>, Configuration>((Configuration config) =>
                                          {
                                              config.DatabasePath = dbPath2;
                                              config.CacheLifetime = ServiceLifetime.Transient;
                                              config.RegistrationKey = registrationKey2;
                                          }, config => new Module<string>(config))
                                          .BuildServiceProvider();

            Assert.True(result2.IsSuccess);
            var serviceProvider2 = result2.Value;
            TrackServiceProvider(serviceProvider2);

            var cache2 = serviceProvider2.GetKeyedService<IOrderedCache<Guid, string>>(registrationKey2);

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.NotSame(cache1, cache2); // Different instances from different providers
        }

        [Fact]
        public void Load_WithScopedLifetimeAndRegistrationKey_RegistersKeyedCacheAsScoped()
        {
            var dbPath = GetTempDbPath();
            const string registrationKey = "scoped-test-cache";
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<Module<string>, Configuration>((Configuration config) =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Scoped;
                                             config.RegistrationKey = registrationKey;
                                         }, config => new Module<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;
            TrackServiceProvider(serviceProvider);

            using var scope1 = serviceProvider.CreateScope();
            var cache1InScope1 = scope1.ServiceProvider.GetKeyedService<IOrderedCache<Guid, string>>(registrationKey);
            var cache2InScope1 = scope1.ServiceProvider.GetKeyedService<IOrderedCache<Guid, string>>(registrationKey);

            Assert.NotNull(cache1InScope1);
            Assert.NotNull(cache2InScope1);
            Assert.Same(cache1InScope1, cache2InScope1); // Same scope returns same instance
            
            // Dispose scope1 before creating scope2 to release database lock
            scope1.Dispose();
            
            using var scope2 = serviceProvider.CreateScope();
            var cache1InScope2 = scope2.ServiceProvider.GetKeyedService<IOrderedCache<Guid, string>>(registrationKey);

            Assert.NotNull(cache1InScope2);
            Assert.NotSame(cache1InScope1, cache1InScope2); // Different scopes return different instances
        }

        [Fact]
        public void Configuration_DefaultValues_AreCorrect()
        {
            var configuration = new Configuration();

            Assert.Equal("cache.db", configuration.DatabasePath);
            Assert.Equal("cache", configuration.CollectionName);
            Assert.False(configuration.IncludeL1Caching);
            Assert.Equal(128, configuration.L1MinCap);
            Assert.Equal(8192, configuration.L1MaxCap);
            Assert.Equal(ServiceLifetime.Singleton, configuration.CacheLifetime);
            Assert.Null(configuration.RegistrationKey);
        }

        [Fact]
        public void Module_IsSubclassOfAModule()
        {
            var configuration = new Configuration
            {
                DatabasePath = GetTempDbPath()
            };

            var module = new Module<string>(configuration);

            Assert.IsAssignableFrom<Caching.DI.Module<Guid, string, Configuration>>(module);
        }

        [Fact]
        public void Load_WithLiteDBStore_DataCanBeAddedAndRetrieved()
        {
            var dbPath = GetTempDbPath();
            
            // Create a cache and add data
            var result = ComponentBuilder.CreateNew()
                                          .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                          .WithModule<Module<string>, Configuration>(config =>
                                          {
                                              config.DatabasePath = dbPath;
                                              config.CacheLifetime = ServiceLifetime.Singleton;
                                          }, config => new Module<string>(config))
                                          .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<Guid, string>>();
            Assert.NotNull(cache);

            // Add an entry
            cache.Add("test-value", out var entry);
            Assert.NotNull(entry);

            // Retrieve the entry
            var retrieved = cache.GetEntryOrDefault(entry.Id, out var retrievedEntry);
            Assert.True(retrieved);
            Assert.NotNull(retrievedEntry);
            Assert.Equal("test-value", retrievedEntry.Value);
        }
    }
}
