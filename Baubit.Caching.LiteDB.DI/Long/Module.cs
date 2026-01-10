using Baubit.Caching.InMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Baubit.Caching.LiteDB.DI.Long
{
    /// <summary>
    /// Dependency injection module for registering an <see cref="IOrderedCache{TId, TValue}"/> with LiteDB-backed L2 storage.
    /// Uses in-memory stores for L1 (when enabled) and LiteDB stores for L2.
    /// This module uses long as the identifier type.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in the cache.</typeparam>
    public class Module<TValue> : Module<long, TValue>
    {
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
        /// <returns>A bounded in-memory store configured with L1 capacity settings.</returns>
        protected override IStore<long, TValue> BuildL1DataStore(IServiceProvider serviceProvider)
        {
            return new Baubit.Caching.InMemory.Store<long, TValue>(
                (long?)Configuration.L1MinCap,
                (long?)Configuration.L1MaxCap,
                _ => null,
                serviceProvider.GetRequiredService<ILoggerFactory>());
        }

        /// <summary>
        /// Builds the L2 data store as a LiteDB-backed persistent store.
        /// Uses a shared database instance for both the store and enumerator factory.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve <see cref="ILoggerFactory"/>.</param>
        /// <returns>A <see cref="StoreLong{TValue}"/> for persistent L2 storage.</returns>
        protected override IStore<long, TValue> BuildL2DataStore(IServiceProvider serviceProvider)
        {
            return new Baubit.Caching.LiteDB.StoreLong<TValue>(
                GetOrCreateDatabase(),
                Configuration.CollectionName,
                serviceProvider.GetRequiredService<ILoggerFactory>());
        }

        /// <summary>
        /// Builds the metadata store for tracking cache entries.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve <see cref="ILoggerFactory"/>.</param>
        /// <returns>A new <see cref="Metadata{TId}"/> instance.</returns>
        protected override IMetadata<long> BuildMetadata(IServiceProvider serviceProvider)
        {
            return new Metadata<long>(
                Configuration.CacheConfiguration,
                serviceProvider.GetRequiredService<ILoggerFactory>());
        }
    }
}
