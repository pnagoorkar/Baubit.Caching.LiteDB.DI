using Baubit.Caching.InMemory;
using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Baubit.Caching.LiteDB.DI
{
    /// <summary>
    /// Base dependency injection module for registering an <see cref="IOrderedCache{TId, TValue}"/> with LiteDB-backed L2 storage.
    /// Uses in-memory stores for L1 (when enabled) and LiteDB stores for L2.
    /// </summary>
    /// <typeparam name="TId">The type of IDs used to identify cache entries.</typeparam>
    /// <typeparam name="TValue">The type of values stored in the cache.</typeparam>
    public abstract class Module<TId, TValue> : Baubit.Caching.DI.Module<TId, TValue, Configuration>
        where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        // Private, per-module-instance key so the database registration never collides with
        // user registrations of LiteDatabase or with databases registered by other modules.
        private readonly object databaseKey = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="Module{TId, TValue}"/> class
        /// using an <see cref="IConfiguration"/> to bind settings.
        /// </summary>
        /// <param name="configuration">The configuration section to bind to <see cref="Configuration"/>.</param>
        protected Module(IConfiguration configuration) : base(configuration)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Module{TId, TValue}"/> class
        /// using an explicit configuration object and optional nested modules.
        /// </summary>
        /// <param name="configuration">The configuration object.</param>
        /// <param name="nestedModules">Optional list of nested modules to load.</param>
        protected Module(Configuration configuration, List<Baubit.DI.IModule> nestedModules = null) : base(configuration, nestedModules)
        {
        }

        /// <summary>
        /// Validates that the configured cache lifetime is supported.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown when <see cref="Configuration.CacheLifetime"/> is not <see cref="ServiceLifetime.Singleton"/>.
        /// Transient and scoped LiteDB-backed caches are not supported yet.
        /// </exception>
        protected override void OnInitialized()
        {
            if (Configuration.CacheLifetime != ServiceLifetime.Singleton)
            {
                throw new NotSupportedException($"LiteDB-backed caches support only the {nameof(ServiceLifetime.Singleton)} lifetime. " +
                                                $"The configured lifetime '{Configuration.CacheLifetime}' is not supported yet.");
            }
            base.OnInitialized();
        }

        /// <summary>
        /// Registers the <see cref="LiteDatabase"/> used by this module as a container-owned singleton,
        /// then registers the ordered cache.
        /// The database is created lazily on first use and is disposed when the root service provider is disposed,
        /// releasing the lock on the database file.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        public override void Load(IServiceCollection services)
        {
            services.AddKeyedSingleton(databaseKey, (_, __) => new LiteDatabase(Configuration.DatabasePath));
            base.Load(services);
        }

        /// <summary>
        /// Gets the shared LiteDB database instance owned by the container, creating it on first use.
        /// All caches built by this module within the same container share this instance.
        /// </summary>
        /// <param name="serviceProvider">The service provider the cache is being resolved from.</param>
        /// <returns>The shared <see cref="LiteDatabase"/> instance.</returns>
        protected LiteDatabase GetOrCreateDatabase(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredKeyedService<LiteDatabase>(databaseKey);
        }

        /// <summary>
        /// Builds the L1 data store as a bounded in-memory store with capacity limits.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve <see cref="ILoggerFactory"/>.</param>
        /// <returns>A bounded in-memory store configured with L1 capacity settings.</returns>
        protected abstract override IStore<TId, TValue> BuildL1DataStore(IServiceProvider serviceProvider);

        /// <summary>
        /// Builds the L2 data store as a LiteDB-backed persistent store.
        /// Uses a shared database instance for both the store and enumerator factory.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve <see cref="ILoggerFactory"/>.</param>
        /// <returns>A LiteDB-backed store for persistent L2 storage.</returns>
        protected abstract override IStore<TId, TValue> BuildL2DataStore(IServiceProvider serviceProvider);

        /// <inheritdoc/>
        protected override IMetadata<TId> BuildMetadata(IServiceProvider serviceProvider)
        {
            // Only load existing IDs if ResumeSession is enabled
            IEnumerable<TId> ids = null;
            if (Configuration.ResumeSession)
            {
                var database = GetOrCreateDatabase(serviceProvider);

                var collection = database.GetCollection<Entry<TId, TValue>>(Configuration.CollectionName);
                // Materialize the IDs immediately to avoid deferred execution issues
                ids = collection.FindAll().Select(entry => entry.Id).ToList();
            }

            return new Metadata<TId>(Configuration.CacheConfiguration,
                                     serviceProvider.GetRequiredService<ILoggerFactory>(),
                                     ids);
        }

        /// <summary>
        /// Builds a factory for creating cache async enumerators with LiteDB persistence support.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
        /// <returns>A <see cref="CacheAsyncEnumeratorFactory{TId, TValue}"/> instance with LiteDB persistence.</returns>
        protected override ICacheAsyncEnumeratorFactory<TId, TValue> BuildCacheEnumeratorFactory(IServiceProvider serviceProvider)
        {
            var liteDbConfiguration = new LiteDB.Configuration
            {
                ResumeSession = Configuration.ResumeSession,
                PersistPositionEveryXMoves = Configuration.PersistPositionEveryXMoves,
                PersistPositionBeforeMove = Configuration.PersistPositionBeforeMove
            };

            return new CacheAsyncEnumeratorFactory<TId, TValue>(GetOrCreateDatabase(serviceProvider), liteDbConfiguration);
        }

        /// <summary>
        /// Builds a factory for creating LiteDB-aware cache enumerator collections.
        /// Returns a factory that creates <see cref="LiteDB.CacheEnumeratorCollection{TId}"/> instances
        /// which track both active and persisted enumerator positions when ResumeSession is enabled.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
        /// <returns>A factory function that creates <see cref="LiteDB.CacheEnumeratorCollection{TId}"/> instances.</returns>
        protected override Func<Baubit.Caching.CacheEnumeratorCollection<TId>> BuildCacheEnumeratorCollectionFactory(IServiceProvider serviceProvider)
        {
            var liteDbConfiguration = new LiteDB.Configuration
            {
                ResumeSession = Configuration.ResumeSession,
                PersistPositionEveryXMoves = Configuration.PersistPositionEveryXMoves,
                PersistPositionBeforeMove = Configuration.PersistPositionBeforeMove
            };

            var database = GetOrCreateDatabase(serviceProvider);
            return () => new LiteDB.CacheEnumeratorCollection<TId>(liteDbConfiguration, database);
        }
    }
}
