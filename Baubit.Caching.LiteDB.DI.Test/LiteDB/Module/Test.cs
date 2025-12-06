using Baubit.DI;
using Baubit.DI.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Baubit.Caching.LiteDB.DI.Test.LiteDB.Module
{
    /// <summary>
    /// Unit tests for <see cref="global::Baubit.Caching.LiteDB.DI.LiteDB.Module{TValue}"/>
    /// </summary>
    public class Test : IDisposable
    {
        private readonly List<string> _tempFiles = new();

        public void Dispose()
        {
            // Clean up temporary database files created during tests
            foreach (var file in _tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
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

        [Fact]
        public void Load_WithSingletonLifetime_RegistersCacheAsSingleton()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Singleton;
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;

            var cache1 = serviceProvider.GetService<IOrderedCache<string>>();
            var cache2 = serviceProvider.GetService<IOrderedCache<string>>();

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.Same(cache1, cache2); // Singleton returns same instance
        }

        [Fact]
        public void Load_WithTransientLifetime_RegistersCacheAsTransient()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Transient;
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;

            var cache1 = serviceProvider.GetService<IOrderedCache<string>>();
            var cache2 = serviceProvider.GetService<IOrderedCache<string>>();

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.NotSame(cache1, cache2); // Transient returns different instances
        }

        [Fact]
        public void Load_WithScopedLifetime_RegistersCacheAsScoped()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Scoped;
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;

            using var scope1 = serviceProvider.CreateScope();
            using var scope2 = serviceProvider.CreateScope();

            var cache1InScope1 = scope1.ServiceProvider.GetService<IOrderedCache<string>>();
            var cache2InScope1 = scope1.ServiceProvider.GetService<IOrderedCache<string>>();
            var cache1InScope2 = scope2.ServiceProvider.GetService<IOrderedCache<string>>();

            Assert.NotNull(cache1InScope1);
            Assert.NotNull(cache2InScope1);
            Assert.NotNull(cache1InScope2);
            Assert.Same(cache1InScope1, cache2InScope1); // Same scope returns same instance
            Assert.NotSame(cache1InScope1, cache1InScope2); // Different scopes return different instances
        }

        [Fact]
        public void Load_WithL1CachingEnabled_RegistersCacheWithL1Store()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.IncludeL1Caching = true;
                                             config.L1MinCap = 64;
                                             config.L1MaxCap = 1024;
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var cache = result.Value.GetService<IOrderedCache<string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithL1CachingDisabled_RegistersCacheWithoutL1Store()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.IncludeL1Caching = false;
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var cache = result.Value.GetService<IOrderedCache<string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithCacheConfiguration_RegistersCacheWithConfiguration()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheConfiguration = new Baubit.Caching.Configuration();
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var cache = result.Value.GetService<IOrderedCache<string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithCustomDatabasePathAndCollection_RegistersCache()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CollectionName = "custom_collection";
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var cache = result.Value.GetService<IOrderedCache<string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Constructor_WithConfiguration_CreatesModule()
        {
            var config = new global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration
            {
                DatabasePath = GetTempDbPath()
            };
            var module = new global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>(config);

            Assert.NotNull(module);
        }

        [Fact]
        public void Constructor_WithConfigurationAndNestedModules_CreatesModule()
        {
            var config = new global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration
            {
                DatabasePath = GetTempDbPath()
            };
            var nestedModules = new System.Collections.Generic.List<IModule>();
            var module = new global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>(config, nestedModules);

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

            var module = new global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>(configuration);

            Assert.NotNull(module);

            // Test that the module loads correctly with the same config values
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CollectionName = "test_collection";
                                             config.IncludeL1Caching = true;
                                             config.L1MinCap = 64;
                                             config.L1MaxCap = 1024;
                                             config.CacheLifetime = ServiceLifetime.Singleton;
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var cache = result.Value.GetService<IOrderedCache<string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithSingletonLifetimeAndRegistrationKey_RegistersKeyedCacheAsSingleton()
        {
            var dbPath = GetTempDbPath();
            const string registrationKey = "singleton-test-cache";
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>((global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration config) =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Singleton;
                                             config.RegistrationKey = registrationKey;
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;

            var cache1 = serviceProvider.GetKeyedService<IOrderedCache<string>>(registrationKey);
            var cache2 = serviceProvider.GetKeyedService<IOrderedCache<string>>(registrationKey);

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.Same(cache1, cache2); // Singleton returns same instance
        }

        [Fact]
        public void Load_WithTransientLifetimeAndRegistrationKey_RegistersKeyedCacheAsTransient()
        {
            var dbPath = GetTempDbPath();
            const string registrationKey = "transient-test-cache";
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>((global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration config) =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Transient;
                                             config.RegistrationKey = registrationKey;
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;

            var cache1 = serviceProvider.GetKeyedService<IOrderedCache<string>>(registrationKey);
            var cache2 = serviceProvider.GetKeyedService<IOrderedCache<string>>(registrationKey);

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.NotSame(cache1, cache2); // Transient returns different instances
        }

        [Fact]
        public void Load_WithScopedLifetimeAndRegistrationKey_RegistersKeyedCacheAsScoped()
        {
            var dbPath = GetTempDbPath();
            const string registrationKey = "scoped-test-cache";
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                         .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>((global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration config) =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheLifetime = ServiceLifetime.Scoped;
                                             config.RegistrationKey = registrationKey;
                                         })
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;

            using var scope1 = serviceProvider.CreateScope();
            using var scope2 = serviceProvider.CreateScope();

            var cache1InScope1 = scope1.ServiceProvider.GetKeyedService<IOrderedCache<string>>(registrationKey);
            var cache2InScope1 = scope1.ServiceProvider.GetKeyedService<IOrderedCache<string>>(registrationKey);
            var cache1InScope2 = scope2.ServiceProvider.GetKeyedService<IOrderedCache<string>>(registrationKey);

            Assert.NotNull(cache1InScope1);
            Assert.NotNull(cache2InScope1);
            Assert.NotNull(cache1InScope2);
            Assert.Same(cache1InScope1, cache2InScope1); // Same scope returns same instance
            Assert.NotSame(cache1InScope1, cache1InScope2); // Different scopes return different instances
        }

        [Fact]
        public void Configuration_DefaultValues_AreCorrect()
        {
            var configuration = new global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration();

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
            var configuration = new global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration
            {
                DatabasePath = GetTempDbPath()
            };

            var module = new global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>(configuration);

            Assert.IsAssignableFrom<Baubit.Caching.DI.AModule<string, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>>(module);
        }

        [Fact]
        public void Load_WithLiteDBStore_DataCanBeAddedAndRetrieved()
        {
            var dbPath = GetTempDbPath();
            
            // Create a cache and add data
            var result = ComponentBuilder.CreateNew()
                                          .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { })
                                          .WithModule<global::Baubit.Caching.LiteDB.DI.LiteDB.Module<string>, global::Baubit.Caching.LiteDB.DI.LiteDB.Configuration>(config =>
                                          {
                                              config.DatabasePath = dbPath;
                                              config.CacheLifetime = ServiceLifetime.Singleton;
                                          })
                                          .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var cache = result.Value.GetService<IOrderedCache<string>>();
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
