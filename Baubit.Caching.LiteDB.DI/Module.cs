using Baubit.Caching.DI;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Baubit.Caching.LiteDB.DI
{
    /// <summary>
    /// Dependency injection module for registering an <see cref="IOrderedCache{TValue}"/> with LiteDB-backed L2 storage.
    /// Uses in-memory <see cref="Store{TValue}"/> for L1 (when enabled) and <see cref="Store{TValue}"/> for L2.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in the cache.</typeparam>
    public class Module<TValue> : AModule<TValue, Configuration>
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
        /// <returns>A bounded <see cref="Store{TValue}"/> configured with L1 capacity settings.</returns>
        protected override IStore<TValue> BuildL1DataStore(IServiceProvider serviceProvider)
        {
            return new InMemory.Store<TValue>(Configuration.L1MinCap, Configuration.L1MaxCap, serviceProvider.GetRequiredService<ILoggerFactory>());
        }

        /// <summary>
        /// Builds the L2 data store as a LiteDB-backed persistent store.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve <see cref="ILoggerFactory"/>.</param>
        /// <returns>A <see cref="Store{TValue}"/> for persistent L2 storage.</returns>
        protected override IStore<TValue> BuildL2DataStore(IServiceProvider serviceProvider)
        {
            return new Store<TValue>(
                Configuration.DatabasePath,
                Configuration.CollectionName,
                serviceProvider.GetRequiredService<ILoggerFactory>());
        }

        /// <summary>
        /// Builds the metadata store for tracking cache entries.
        /// </summary>
        /// <param name="serviceProvider">The service provider (unused for in-memory metadata).</param>
        /// <returns>A new <see cref="Metadata"/> instance.</returns>
        protected override IMetadata BuildMetadata(IServiceProvider serviceProvider)
        {
            return new Metadata();
        }
    }
}
