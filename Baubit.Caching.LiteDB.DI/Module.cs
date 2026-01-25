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
        private LiteDatabase database;

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
        /// Gets or creates the shared LiteDB database instance.
        /// </summary>
        /// <returns>The shared <see cref="LiteDatabase"/> instance.</returns>
        protected LiteDatabase GetOrCreateDatabase()
        {
            if (database == null)
            {
                database = new LiteDatabase(Configuration.DatabasePath);
            }
            return database;
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
                var database = GetOrCreateDatabase();

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

            return new CacheAsyncEnumeratorFactory<TId, TValue>(GetOrCreateDatabase(), liteDbConfiguration);
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

            return () => new LiteDB.CacheEnumeratorCollection<TId>(liteDbConfiguration, GetOrCreateDatabase());
        }
    }
}
