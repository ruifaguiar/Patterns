using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

public class BulkDataStreamer : IBulkDataProducer, IBulkDataConsumer
{
    #region Fields

    private const int MappedFileSize = 40000000;

    private MemoryMappedFile _sharedMemory;
    private Stream _stream;
    private EventWaitHandle _streamWriteEvent;
    private EventWaitHandle _streamReadEvent;
    private bool _isMessagePumpThreadAlive;
    private readonly ConcurrentQueue<byte[]> _producerQueue;
    private readonly byte[] _messageBuffer = new byte[sizeof(int)];
    private bool _disposed;
    private readonly bool _isProducer;
    private ManualResetEvent _pointStreamManualResetEvent;

    #endregion

    #region Properties

    public string SharedMemoryName { get; private set; }

    protected Action<byte[]> MessageReceivedCallback { get; set; }

    #endregion

    #region Constructor

    /// <summary>
    ///     Allows communicating point cloud data between different processes.
    /// </summary>
    private BulkDataStreamer(string sharedMemoryName, bool isProducer,
        ManualResetEvent pointStreamManualResetEvent = null)
    {
        SharedMemoryName = sharedMemoryName;
        _isProducer = isProducer;

        _pointStreamManualResetEvent = pointStreamManualResetEvent;

        SetupStream();

        _producerQueue = new ConcurrentQueue<byte[]>();

        _isMessagePumpThreadAlive = true;

        var messagePumpThread = isProducer ? new Thread(ProducerProcess) : new Thread(ConsumerProcess);
        messagePumpThread.IsBackground = true;
        messagePumpThread.Start();
    }

    #endregion

    #region Public Methods

    public static IBulkDataProducer GetBulkDataProducer(string sharedMemoryName,
        ManualResetEvent pointStreamManualResetEvent = null)
    {
        var result = new BulkDataStreamer(sharedMemoryName, true, pointStreamManualResetEvent);
        return result;
    }

    public static IBulkDataConsumer GetBulkDataConsumer(string sharedMemoryName,
        Action<byte[]> messageReceivedCallback)
    {
        var result = new BulkDataStreamer(sharedMemoryName, false)
        {
            MessageReceivedCallback = messageReceivedCallback
        };
        return result;
    }

    public void Write(byte[] rawData)
    {
        if (rawData != null)
        {
            _producerQueue.Enqueue(rawData);
        }
    }

    #endregion

    #region Implementation

    private void SetupStream()
    {
        _streamWriteEvent = new EventWaitHandle(false, EventResetMode.AutoReset, SharedMemoryName + "WriteEvent");
        _streamReadEvent = new EventWaitHandle(false, EventResetMode.AutoReset, SharedMemoryName + "ReadEvent");

        _sharedMemory = MemoryMappedFile.CreateOrOpen(SharedMemoryName, MappedFileSize,
            MemoryMappedFileAccess.ReadWrite);
        _stream = _sharedMemory.CreateViewStream();
    }

    private void ShutdownWaitTimerOnElapsed()
    {
        // stop waiting for consumer to respond
        _isMessagePumpThreadAlive = false;
    }

    #region Consumer Logic

    private void ConsumerProcess()
    {
        while (_isMessagePumpThreadAlive)
        {
            try
            {
                // timeout returns false
                // must wake up from time to time to check for shutdown condition
                while (!_streamWriteEvent.WaitOne(1000))
                {
                    if (!_isMessagePumpThreadAlive)
                    {
                        break;
                    }
                }

                ReadMessageChunks();
            }
            catch (Exception ex)
            {
                Debug.Assert(false, ex.Message);
            }
        }


    }

    private void ReadMessageChunks()
    {
        _stream.Position = 0;

        // read number of chunks
        _stream.Read(_messageBuffer, 0, sizeof(int));

        int numChunks = BitConverter.ToInt32(_messageBuffer, 0);
        var chunkList = new List<byte[]>();

        for (int i = 0; i < numChunks; i++)
        {
            // next int is the chunk size
            _stream.Read(_messageBuffer, 0, sizeof(int));
            int chunkSize = BitConverter.ToInt32(_messageBuffer, 0);

            // read chunk data
            var rawMessage = new byte[chunkSize];
            _stream.Read(rawMessage, 0, chunkSize);

            chunkList.Add(rawMessage);
        }

        // signal producer to that it is safe to write to mapped memory file
        _streamReadEvent.Set();

        foreach (var rawMessage in chunkList)
        {
            MessageReceivedCallback(rawMessage);
        }
    }

    #endregion

    #region Producer Logic

    private void ProducerProcess()
    {
        while (_isMessagePumpThreadAlive)
        {
            if (_pointStreamManualResetEvent != null)
            {
                //FARO Arm USB is receiveing a point update, let us give priority to that
                _pointStreamManualResetEvent.WaitOne();
            }
            int currentQueueLevel = _producerQueue.Count;

            if (currentQueueLevel > 0)
            {
                var messageChunk = CreateMessageChunk(currentQueueLevel);
                SendOneMessage(messageChunk);

                // timeout returns false
                while (!_streamReadEvent.WaitOne(1000))
                {
                    if (!_isMessagePumpThreadAlive)
                    {
                        break;
                    }
                }
            }
        }


    }

    private byte[] CreateMessageChunk(int currentQueueLevel)
    {
        var result = new List<byte>();

        int numberOfLines = 0;
        int totalMessageSize = sizeof(int);
        for (int i = 0; i < currentQueueLevel; i++)
        {
            byte[] rawData;
            if (_producerQueue.TryPeek(out rawData))
            {
                totalMessageSize += rawData.Length + sizeof(int);

                if (totalMessageSize >= MappedFileSize)
                {
                    break;
                }
                if (_producerQueue.TryDequeue(out rawData))
                {
                    // add data length ahead of the raw data
                    result.AddRange(BitConverter.GetBytes(rawData.Length));
                    result.AddRange(rawData);
                    numberOfLines++;
                }
            }
        }

        // insert number of individual chunks in the front of the message
        result.InsertRange(0, BitConverter.GetBytes(numberOfLines));
        return result.ToArray();
    }

    private void SendOneMessage(byte[] rawData)
    {
        try
        {
            _stream.Position = 0;
            _stream.WriteAsync(rawData, 0, rawData.Length);
        }
        catch (Exception ex)
        {
            Debug.Assert(false, ex.Message);
        }
    }

    #endregion

    #endregion

    #region IDisposable Members

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                //Ensure that we do not use this object after the dispose (because of the timer used to flush the queue)
                _pointStreamManualResetEvent = null;
                //if (_isProducer && _producerQueue.Count > 0)
                if (_isProducer && !_producerQueue.IsEmpty)
                {
                    // give it five seconds to flush the sender queue
                    Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                    });

                }

            }
        }
        _disposed = true;
    }

    /// <summary>
    /// Exit strategy:
    /// Producer: if producer queue is not empty, wait a second before allowing shutdown to proceed.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
    }

    #endregion
}