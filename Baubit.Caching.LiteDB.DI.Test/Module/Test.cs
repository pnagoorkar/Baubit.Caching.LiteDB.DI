using Baubit.DI;
using Baubit.DI.Extensions;
using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Baubit.Caching.LiteDB.DI.Test.Module
{
    /// <summary>
    /// Concrete test module implementation for testing purposes.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in the cache.</typeparam>
    internal class TestModule<TValue> : Guid7.Module<TValue>
    {
        public TestModule(IConfiguration configuration) : base(configuration)
        {
        }

        public TestModule(Configuration configuration, List<Baubit.DI.IModule>? nestedModules = null) : base(configuration, nestedModules)
        {
        }

        public IStore<System.Guid, TValue> InvokeBuildL1DataStore(IServiceProvider serviceProvider) => BuildL1DataStore(serviceProvider);
    }

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
            var path = Path.Combine(Path.GetTempPath(), $"test_cache_{System.Guid.NewGuid()}.db");
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
                                         .WithModule<TestModule<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;
            TrackServiceProvider(serviceProvider);

            var cache1 = serviceProvider.GetService<IOrderedCache<System.Guid, string>>();
            var cache2 = serviceProvider.GetService<IOrderedCache<System.Guid, string>>();

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.Same(cache1, cache2); // Singleton returns same instance
        }

        #region CacheLifetime

        [Fact]
        public void Load_ResolvedFromDifferentScopes_ReturnsSameSingletonInstance()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<TestModule<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;
            TrackServiceProvider(serviceProvider);

            using var scope1 = serviceProvider.CreateScope();
            using var scope2 = serviceProvider.CreateScope();
            var cacheInScope1 = scope1.ServiceProvider.GetService<IOrderedCache<System.Guid, string>>();
            var cacheInScope2 = scope2.ServiceProvider.GetService<IOrderedCache<System.Guid, string>>();

            Assert.NotNull(cacheInScope1);
            Assert.Same(cacheInScope1, cacheInScope2);
        }

        [Fact]
        public void Configuration_CacheLifetime_IsReadOnly()
        {
            var property = typeof(Configuration).GetProperty(nameof(Configuration.CacheLifetime),
                                                             System.Reflection.BindingFlags.Public |
                                                             System.Reflection.BindingFlags.Instance |
                                                             System.Reflection.BindingFlags.DeclaredOnly);

            Assert.NotNull(property);
            Assert.False(property.CanWrite);
        }

        [Theory]
        [InlineData(ServiceLifetime.Transient)]
        [InlineData(ServiceLifetime.Scoped)]
        public void Constructor_WithNonSingletonCacheLifetime_ThrowsNotSupportedException(ServiceLifetime lifetime)
        {
            var configuration = new Configuration { DatabasePath = GetTempDbPath() };
            ((Caching.DI.Configuration)configuration).CacheLifetime = lifetime;

            Assert.Equal(lifetime, configuration.CacheLifetime);
            var exception = Assert.Throws<NotSupportedException>(() => new TestModule<string>(configuration));
            Assert.Contains(lifetime.ToString(), exception.Message);
        }

        [Theory]
        [InlineData("Transient")]
        [InlineData("Scoped")]
        public void Constructor_WithNonSingletonCacheLifetimeFromIConfiguration_ThrowsNotSupportedException(string lifetime)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabasePath"] = GetTempDbPath(),
                ["CacheLifetime"] = lifetime
            }).Build();

            Assert.Throws<NotSupportedException>(() => new TestModule<string>(configuration));
        }

        [Theory]
        [InlineData(ServiceLifetime.Transient)]
        [InlineData(ServiceLifetime.Scoped)]
        public void BuildServiceProvider_WithNonSingletonCacheLifetime_Fails(ServiceLifetime lifetime)
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<TestModule<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             ((Caching.DI.Configuration)config).CacheLifetime = lifetime;
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsFailed);
            Assert.True(ContainsException<NotSupportedException>(result.Errors));
        }

        private static bool ContainsException<TException>(IEnumerable<FluentResults.IError> errors) where TException : Exception
        {
            return errors.Any(error => (error is FluentResults.ExceptionalError exceptional && exceptional.Exception is TException)
                                       || ContainsException<TException>(error.Reasons));
        }

        #endregion

        [Fact]
        public void Load_WithL1CachingEnabled_RegistersCacheWithL1Store()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<TestModule<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.IncludeL1Caching = true;
                                             config.L1MinCap = 64;
                                             config.L1MaxCap = 1024;
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<System.Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithL1CachingDisabled_RegistersCacheWithoutL1Store()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<TestModule<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.IncludeL1Caching = false;
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<System.Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithCacheConfiguration_RegistersCacheWithConfiguration()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<TestModule<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CacheConfiguration = new Caching.Configuration();
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<System.Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithCustomDatabasePathAndCollection_RegistersCache()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<TestModule<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CollectionName = "custom_collection";
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<System.Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Constructor_WithConfiguration_CreatesModule()
        {
            var config = new Configuration
            {
                DatabasePath = GetTempDbPath()
            };
            var module = new TestModule<string>(config);

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
            var module = new TestModule<string>(config, nestedModules);

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

            var module = new TestModule<string>(configuration);

            Assert.NotNull(module);

            // Test that the module loads correctly with the same config values
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<TestModule<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.CollectionName = "test_collection";
                                             config.IncludeL1Caching = true;
                                             config.L1MinCap = 64;
                                             config.L1MaxCap = 1024;
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<System.Guid, string>>();
            Assert.NotNull(cache);
        }

        [Fact]
        public void Load_WithSingletonLifetimeAndRegistrationKey_RegistersKeyedCacheAsSingleton()
        {
            var dbPath = GetTempDbPath();
            const string registrationKey = "singleton-test-cache";
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<TestModule<string>, Configuration>((Configuration config) =>
                                         {
                                             config.DatabasePath = dbPath;
                                             config.RegistrationKey = registrationKey;
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            var serviceProvider = result.Value;
            TrackServiceProvider(serviceProvider);

            var cache1 = serviceProvider.GetKeyedService<IOrderedCache<System.Guid, string>>(registrationKey);
            var cache2 = serviceProvider.GetKeyedService<IOrderedCache<System.Guid, string>>(registrationKey);

            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            Assert.Same(cache1, cache2); // Singleton returns same instance
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

            var module = new TestModule<string>(configuration);

            Assert.IsAssignableFrom<Caching.DI.Module<System.Guid, string, Configuration>>(module);
        }

        [Fact]
        public void Load_WithLiteDBStore_DataCanBeAddedAndRetrieved()
        {
            var dbPath = GetTempDbPath();
            
            // Create a cache and add data
            var result = ComponentBuilder.CreateNew()
                                          .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                          .WithModule<TestModule<string>, Configuration>(config =>
                                          {
                                              config.DatabasePath = dbPath;
                                          }, config => new TestModule<string>(config))
                                          .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            
            var cache = result.Value.GetService<IOrderedCache<System.Guid, string>>();
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

        #region DatabaseLifetime

        private static Configuration CreateConfiguration(string dbPath, Action<Configuration>? configure = null)
        {
            var configuration = new Configuration
            {
                DatabasePath = dbPath,
                CacheConfiguration = new Caching.Configuration()
            };
            configure?.Invoke(configuration);
            return configuration;
        }

        private ServiceProvider LoadIntoNewServiceProvider(params Baubit.DI.IModule[] modules)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            foreach (var module in modules)
            {
                module.Load(services);
            }
            var serviceProvider = services.BuildServiceProvider();
            TrackServiceProvider(serviceProvider);
            return serviceProvider;
        }

        /// <summary>
        /// Asserts the database file is no longer locked: it can be reopened in-process and then deleted.
        /// </summary>
        private static void AssertDatabaseReleased(string dbPath)
        {
            Assert.True(File.Exists(dbPath));

            using (var database = new LiteDatabase(dbPath))
            {
                Assert.NotNull(database.GetCollectionNames().ToList());
            }

            File.Delete(dbPath);
            Assert.False(File.Exists(dbPath));
        }

        private static int CountEntries(string dbPath, string collectionName = "cache")
        {
            using var database = new LiteDatabase(dbPath);
            return database.GetCollection(collectionName).Count();
        }

        [Fact]
        public void DisposingServiceProvider_ReleasesDatabaseFile()
        {
            var dbPath = GetTempDbPath();
            var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPath)));

            var cache = serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>();
            Assert.True(cache.Add("hello", out _));

            serviceProvider.Dispose();

            Assert.Equal(1, CountEntries(dbPath));
            AssertDatabaseReleased(dbPath);
        }

        [Fact]
        public async Task DisposingServiceProviderAsync_ReleasesDatabaseFile()
        {
            var dbPath = GetTempDbPath();
            var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPath)));

            var cache = serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>();
            Assert.True(cache.Add("hello", out _));

            await serviceProvider.DisposeAsync();

            AssertDatabaseReleased(dbPath);
        }

        [Fact]
        public void DisposingServiceProvider_WithRegistrationKey_ReleasesDatabaseFile()
        {
            var dbPath = GetTempDbPath();
            const string registrationKey = "keyed-cache";
            var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPath, c => c.RegistrationKey = registrationKey)));

            var cache = serviceProvider.GetRequiredKeyedService<IOrderedCache<System.Guid, string>>(registrationKey);
            Assert.True(cache.Add("hello", out _));

            serviceProvider.Dispose();

            AssertDatabaseReleased(dbPath);
        }

        [Fact]
        public void DisposingScope_DoesNotDisposeCacheOrDatabase()
        {
            var dbPath = GetTempDbPath();
            var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPath)));

            using (var scope = serviceProvider.CreateScope())
            {
                var cache = scope.ServiceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>();
                Assert.True(cache.Add("one", out _));
            }

            Assert.True(serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>().Add("two", out _));

            serviceProvider.Dispose();

            Assert.Equal(2, CountEntries(dbPath));
            AssertDatabaseReleased(dbPath);
        }

        [Fact]
        public void DisposingServiceProvider_WhenCacheNeverResolved_DoesNotCreateDatabase()
        {
            var dbPath = GetTempDbPath();
            var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPath)));

            serviceProvider.Dispose();

            Assert.False(File.Exists(dbPath));
        }

        [Fact]
        public void ResumeSession_AfterServiceProviderDisposed_ReopensExistingDatabase()
        {
            var dbPath = GetTempDbPath();
            IEntry<System.Guid, string> first;
            IEntry<System.Guid, string> second;

            using (var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPath))))
            {
                var cache = serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>();
                Assert.True(cache.Add("first", out first));
                Assert.True(cache.Add("second", out second));
            }

            using (var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPath, c => c.ResumeSession = true))))
            {
                var cache = serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>();

                Assert.Equal(2, cache.Count);
                Assert.True(cache.GetEntryOrDefault(first.Id, out var resumedFirst));
                Assert.Equal("first", resumedFirst.Value);
                Assert.True(cache.GetEntryOrDefault(second.Id, out var resumedSecond));
                Assert.Equal("second", resumedSecond.Value);
            }

            AssertDatabaseReleased(dbPath);
        }

        [Fact]
        public async Task ResumeSession_AfterServiceProviderDisposed_ResumesPersistedEnumeratorPosition()
        {
            var dbPath = GetTempDbPath();
            const string enumeratorId = "consumer";
            Action<Configuration> configure = c =>
            {
                c.ResumeSession = true;
                c.PersistPositionEveryXMoves = 1;
                c.PersistPositionBeforeMove = false;
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            using (var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPath, configure))))
            {
                var cache = Assert.IsAssignableFrom<OrderedCache<System.Guid, string>>(serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>());
                Assert.True(cache.Add("first", out _));
                Assert.True(cache.Add("second", out _));

                await using var enumerator = cache.GetAsyncEnumerator(enumeratorId, cts.Token);
                Assert.True(await enumerator.MoveNextAsync());
                Assert.Equal("first", enumerator.Current.Value);
            }

            using (var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPath, configure))))
            {
                var cache = Assert.IsAssignableFrom<OrderedCache<System.Guid, string>>(serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>());

                await using var enumerator = cache.GetAsyncEnumerator(enumeratorId, cts.Token);
                Assert.True(await enumerator.MoveNextAsync());
                Assert.Equal("second", enumerator.Current.Value);
            }

            AssertDatabaseReleased(dbPath);
        }

        [Fact]
        public void SameModuleInstance_LoadedIntoSuccessiveContainers_UsesFreshDatabaseEachTime()
        {
            var dbPath = GetTempDbPath();
            var module = new TestModule<string>(CreateConfiguration(dbPath));

            using (var serviceProvider = LoadIntoNewServiceProvider(module))
            {
                Assert.True(serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>().Add("one", out _));
            }

            using (var serviceProvider = LoadIntoNewServiceProvider(module))
            {
                Assert.True(serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>().Add("two", out _));
            }

            Assert.Equal(2, CountEntries(dbPath));
            AssertDatabaseReleased(dbPath);
        }

        [Fact]
        public void MultipleModulesInSameContainer_UseSeparateDatabases_AndReleaseAll()
        {
            var dbPathA = GetTempDbPath();
            var dbPathB = GetTempDbPath();
            var serviceProvider = LoadIntoNewServiceProvider(new TestModule<string>(CreateConfiguration(dbPathA)),
                                                             new TestModule<int>(CreateConfiguration(dbPathB)));

            var cacheA = serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>();
            var cacheB = serviceProvider.GetRequiredService<IOrderedCache<System.Guid, int>>();
            Assert.True(cacheA.Add("a", out _));
            Assert.True(cacheB.Add(1, out _));
            Assert.True(cacheB.Add(2, out _));

            serviceProvider.Dispose();

            Assert.Equal(1, CountEntries(dbPathA));
            Assert.Equal(2, CountEntries(dbPathB));
            AssertDatabaseReleased(dbPathA);
            AssertDatabaseReleased(dbPathB);
        }

        [Fact]
        public void Load_DoesNotOverrideUserRegisteredLiteDatabase()
        {
            var dbPath = GetTempDbPath();
            using var userDatabase = new LiteDatabase(":memory:");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(userDatabase);
            new TestModule<string>(CreateConfiguration(dbPath)).Load(services);
            var serviceProvider = services.BuildServiceProvider();
            TrackServiceProvider(serviceProvider);

            Assert.True(serviceProvider.GetRequiredService<IOrderedCache<System.Guid, string>>().Add("hello", out _));
            Assert.Same(userDatabase, serviceProvider.GetRequiredService<LiteDatabase>());

            serviceProvider.Dispose();

            AssertDatabaseReleased(dbPath);
        }

        [Fact]
        public void DisposingServiceProvider_BuiltWithComponentBuilder_ReleasesDatabaseFile()
        {
            var dbPath = GetTempDbPath();
            var result = ComponentBuilder.CreateNew()
                                         .WithModule<Setup.Logging.Module, Setup.Logging.Configuration>((Setup.Logging.Configuration _) => { }, config => new Setup.Logging.Module(config))
                                         .WithModule<TestModule<string>, Configuration>(config =>
                                         {
                                             config.DatabasePath = dbPath;
                                         }, config => new TestModule<string>(config))
                                         .BuildServiceProvider();

            Assert.True(result.IsSuccess);
            TrackServiceProvider(result.Value);
            Assert.True(result.Value.GetRequiredService<IOrderedCache<System.Guid, string>>().Add("hello", out _));

            Assert.IsAssignableFrom<IDisposable>(result.Value).Dispose();

            AssertDatabaseReleased(dbPath);
        }

        #endregion

        #region BuildL1DataStore

        private static IStore<System.Guid, string> BuildL1DataStore(out ServiceProvider serviceProvider)
        {
            serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
            return new TestModule<string>(new Configuration { L1MinCap = 2, L1MaxCap = 4 }).InvokeBuildL1DataStore(serviceProvider);
        }

        [Fact]
        public void BuildL1DataStore_ReturnsInMemoryStoreWithConfiguredCapacity()
        {
            using var store = BuildL1DataStore(out var serviceProvider);
            using var _ = serviceProvider;

            var inMemoryStore = Assert.IsAssignableFrom<InMemory.Store<System.Guid, string>>(store);
            Assert.Equal((long?)2, inMemoryStore.MinCapacity);
            Assert.Equal((long?)4, inMemoryStore.MaxCapacity);
        }

        [Fact]
        public void BuildL1DataStore_DoesNotGenerateIds()
        {
            using var store = BuildL1DataStore(out var serviceProvider);
            using var _ = serviceProvider;

            Assert.False(store.Add("value", out var entry));
            Assert.Null(entry);
        }

        [Fact]
        public void BuildL1DataStore_AcceptsIdsProvidedByL2()
        {
            using var store = BuildL1DataStore(out var serviceProvider);
            using var _ = serviceProvider;
            var id = System.Guid.NewGuid();

            Assert.True(store.Add(id, "value", out var entry));
            Assert.Equal(id, entry.Id);
            Assert.True(store.GetValueOrDefault(id, out var value));
            Assert.Equal("value", value);
        }

        #endregion
    }
}
