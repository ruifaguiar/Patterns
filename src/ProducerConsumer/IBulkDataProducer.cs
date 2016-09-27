using System;

public interface IBulkDataProducer : IDisposable
    {
        string SharedMemoryName { get; }

        /// <summary>
        /// Writes raw message into producer queue. 
        /// </summary>
        /// <remarks>DO NOT reuse rawData - must provide an allocated instance each time you call this</remarks>
        /// <param name="rawData"></param>
        void Write(byte[] rawData);
    }