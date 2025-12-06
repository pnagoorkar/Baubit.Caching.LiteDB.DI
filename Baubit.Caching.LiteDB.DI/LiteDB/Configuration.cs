using Baubit.Caching.DI;

namespace Baubit.Caching.LiteDB.DI.LiteDB
{
    /// <summary>
    /// Configuration for the LiteDB caching module.
    /// Stores cache data in a LiteDB database for persistent L2 storage.
    /// </summary>
    public class Configuration : AConfiguration
    {
        /// <summary>
        /// Gets or sets the path to the LiteDB database file.
        /// If not specified, a default path will be used.
        /// </summary>
        public string DatabasePath { get; set; } = "cache.db";

        /// <summary>
        /// Gets or sets the name of the collection within the LiteDB database.
        /// Defaults to "cache".
        /// </summary>
        public string CollectionName { get; set; } = "cache";
    }
}
