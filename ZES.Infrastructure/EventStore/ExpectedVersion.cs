namespace ZES.Infrastructure.EventStore
{
    /// <summary>
    /// Stream expected version constants
    /// </summary>
    public static class ExpectedVersion
    {
        /// <summary>
        /// Gets the Any version 
        /// </summary>
        public static int Any => GetExpectedVersion(SqlStreamStore.Streams.ExpectedVersion.Any);
        
        /// <summary>
        ///  Gets No Stream version. Stream should not exist. If stream exists, then will be considered
        ///     a concurrency problem.
        /// </summary>
        public static int NoStream => GetExpectedVersion(SqlStreamStore.Streams.ExpectedVersion.NoStream);
        
        /// <summary>
        ///     Gets Empty Stream version. stream should exist but be empty. If stream does not exist, or
        ///     contains messages, then will be considered a concurrency problem.
        /// </summary>
        public static int EmptyStream => GetExpectedVersion(SqlStreamStore.Streams.ExpectedVersion.EmptyStream);
        
        private static int GetExpectedVersion(int sqlVersion)
        {
            if (Configuration.UseSqlStore)
                return sqlVersion;
            
            switch (sqlVersion)
            {
                case SqlStreamStore.Streams.ExpectedVersion.Any:
                    return global::EventStore.ClientAPI.ExpectedVersion.Any;
                case SqlStreamStore.Streams.ExpectedVersion.NoStream:
                    return global::EventStore.ClientAPI.ExpectedVersion.NoStream;
                case SqlStreamStore.Streams.ExpectedVersion.EmptyStream:
                    return global::EventStore.ClientAPI.ExpectedVersion.NoStream;
                default:
                    return sqlVersion;
            }    
        }
    }
}