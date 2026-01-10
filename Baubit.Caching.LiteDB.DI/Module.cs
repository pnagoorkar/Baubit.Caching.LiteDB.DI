using Baubit.Caching.DI;
using Baubit.Caching.InMemory;
using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Baubit.Caching.LiteDB.DI
{
    /// <summary>
    /// Dependency injection module for registering an <see cref="IOrderedCache{TId, TValue}"/> with LiteDB-backed L2 storage.
    /// Uses in-memory <see cref="Store{TId, TValue}"/> for L1 (when enabled) and <see cref="StoreGuid{TValue}"/> for L2.
    /// This module uses Guid (GuidV7) as the identifier type.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in the cache.</typeparam>
    public class Module<TValue> : Baubit.Caching.DI.Module<Guid, TValue, Configuration>
    {
        private LiteDatabase _database;
        /// <summary>
        /// Initializes a new instance of the <see cref="Module{TValue}"/> class
        /// using an <see cref="IConfiguration"/> to bind settings.
        /// </summary>
        /// <param name="configuration">The configuration section to bind to <see cref="Configuration"/>.</param>
        public Module(IConfiguration configuration) : base(configuration)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Module{TValue}"/> class
        /// using an explicit configuration object and optional nested modules.
        /// </summary>
        /// <param name="configuration">The configuration object.</param>
        /// <param name="nestedModules">Optional list of nested modules to load.</param>
        public Module(Configuration configuration, List<Baubit.DI.IModule> nestedModules = null) : base(configuration, nestedModules)
        {
        }

        /// <summary>
        /// Builds the L1 data store as a bounded in-memory store with capacity limits.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve <see cref="ILoggerFactory"/>.</param>
        /// <returns>A bounded <see cref="Store{TId, TValue}"/> configured with L1 capacity settings.</returns>
        protected override IStore<Guid, TValue> BuildL1DataStore(IServiceProvider serviceProvider)
        {
            return new InMemory.Store<Guid, TValue>(Configuration.L1MinCap, Configuration.L1MaxCap, _ => null, serviceProvider.GetRequiredService<ILoggerFactory>());
        }

        /// <summary>
        /// Gets or creates the shared LiteDB database instance.
        /// </summary>
        /// <returns>The shared <see cref="LiteDatabase"/> instance.</returns>
        private LiteDatabase GetOrCreateDatabase()
        {
            if (_database == null)
            {
                _database = new LiteDatabase(Configuration.DatabasePath);
            }
            return _database;
        }

        /// <summary>
        /// Builds the L2 data store as a LiteDB-backed persistent store.
        /// Uses a shared database instance for both the store and enumerator factory.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve <see cref="ILoggerFactory"/>.</param>
        /// <returns>A <see cref="StoreGuid{TValue}"/> for persistent L2 storage.</returns>
        protected override IStore<Guid, TValue> BuildL2DataStore(IServiceProvider serviceProvider)
        {
            return new StoreGuid<TValue>(
                GetOrCreateDatabase(),
                Configuration.CollectionName,
                serviceProvider.GetRequiredService<ILoggerFactory>());
        }

        /// <summary>
        /// Builds the metadata store for tracking cache entries.
        /// </summary>
        /// <param name="serviceProvider">The service provider (unused for in-memory metadata).</param>
        /// <returns>A new <see cref="Metadata{TId}"/> instance.</returns>
        protected override IMetadata<Guid> BuildMetadata(IServiceProvider serviceProvider)
        {
            return new Metadata<Guid>(Configuration.CacheConfiguration, serviceProvider.GetRequiredService<ILoggerFactory>());
        }

        /// <summary>
        /// Builds a factory for creating cache async enumerators with LiteDB persistence support.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
        /// <returns>A <see cref="CacheAsyncEnumeratorFactory{TId, TValue}"/> instance with LiteDB persistence.</returns>
        protected override ICacheAsyncEnumeratorFactory<Guid, TValue> BuildCacheEnumeratorFactory(IServiceProvider serviceProvider)
        {
            var liteDbConfiguration = new Baubit.Caching.LiteDB.Configuration
            {
                ResumeSession = Configuration.ResumeSession,
                PersistPositionEveryXMoves = Configuration.PersistPositionEveryXMoves,
                PersistPositionBeforeMove = Configuration.PersistPositionBeforeMove
            };

            return new Baubit.Caching.LiteDB.CacheAsyncEnumeratorFactory<Guid, TValue>(
                GetOrCreateDatabase(),
                liteDbConfiguration);
        }
    }
}
