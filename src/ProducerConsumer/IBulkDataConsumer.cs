using System;

public interface IBulkDataConsumer : IDisposable
    {
        string SharedMemoryName { get; }
    }