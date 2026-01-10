namespace Baubit.Caching.LiteDB.DI
{
    /// <summary>
    /// Configuration for the LiteDB caching module.
    /// Stores cache data in a LiteDB database for persistent L2 storage.
    /// </summary>
    public class Configuration : Baubit.Caching.DI.Configuration
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

        /// <summary>
        /// Gets or sets a value indicating whether to resume enumeration sessions from persisted state.
        /// When true, async enumerators will check LiteDB for saved positions and resume from there.
        /// When false, enumerators always start from the beginning.
        /// Default is false.
        /// </summary>
        public bool ResumeSession { get; set; } = false;

        /// <summary>
        /// Gets or sets the number of MoveNext operations before persisting position to LiteDB.
        /// Higher values improve performance but reduce reliability if application crashes.
        /// Default is 0 (do not persist position at all).
        /// Set to 1 for maximum reliability (persist after every move).
        /// Set to higher values (e.g., 10, 100) to reduce I/O overhead.
        /// </summary>
        public int PersistPositionEveryXMoves { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether to persist position before moving to next entry.
        /// When true (default): persists BEFORE moving (may lose last entry if application crashes before reading it).
        /// When false: persists AFTER moving (position always reflects last successfully read entry, better crash recovery).
        /// </summary>
        public bool PersistPositionBeforeMove { get; set; } = true;
    }
}
