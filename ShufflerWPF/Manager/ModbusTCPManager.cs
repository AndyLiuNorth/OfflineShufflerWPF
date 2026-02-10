using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ShufflerWPF.Manager; 

public sealed class ModbusTCPManager
{
    // Lazy Singleton Instance
    //private static readonly Lazy<ModbusTCPManager> _instance = new Lazy<ModbusTCPManager>(() => new ModbusTCPManager());
    //public static ModbusTCPManager Instance => _instance.Value;

    // Some member variables
    private string _serverIp;
    private int _serverPort;
    private const int ConnectionTimeout = 5000;

    // Queue
    //private readonly BlockingCollection<PendingRequest> _commandQueue;

    private readonly Channel<PendingRequest> _commandQueue;
    
    // TCPIP Source
    private TcpClient? _client;
    private NetworkStream? _stream;
    
    // Task Run tcp and token
    private readonly Task _consumerTask;
    private readonly CancellationTokenSource _cts;
    
    // Heartbeat
    private Task? _heartbeatTask;
    private CancellationTokenSource? _heartbeatCts;
    private ushort _heartbeatValue;
    private volatile bool _heartbeatCommDown;
    private int _heartbeatStarted;
    
    private readonly TaskCompletionSource<bool> _heartbeatStartedTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    // Lock item !?
    // private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
    
    // Class add to BlockingCollection
    private class PendingRequest
    {
        
        // Modbus buffer
        public byte[] CommandAdu { get; }
        
        // Task Compelete Flag
        public TaskCompletionSource<byte[]> Tcs { get; }
        
        // PendingRequest Constructor
        public PendingRequest(byte[] commandAdu, TaskCompletionSource<byte[]> tcs)
        {
            CommandAdu = commandAdu;
            Tcs = tcs;
        }
    }
    
    public async Task<bool> IsHealthyAsync()
    {
        // check construction status
        bool consumer = _consumerTask != null && !_consumerTask.IsFaulted && !_consumerTask.IsCanceled;
        
        await _heartbeatStartedTcs.Task;
        
        // check heartbeat status
        bool heartbeatOk = true;
        if (_heartbeatStarted > 0)
        {
            heartbeatOk = _heartbeatTask != null && !_heartbeatTask.IsFaulted && !_heartbeatTask.IsCanceled && !_heartbeatCommDown;
        }
        return consumer && heartbeatOk;
    }
    public Exception? GetTaskException()
    {
        return _consumerTask?.Exception?.InnerException;
    }

    // ModbusTCPManager Constructor
    public ModbusTCPManager(string serverIp, int serverPort)
    {

        _serverIp = serverIp;
        _serverPort = serverPort;
        
        _commandQueue = Channel.CreateUnbounded<PendingRequest>(
            new UnboundedChannelOptions()
            {
                SingleReader = true, // Single consumer
                SingleWriter = false // Multiple producers
            }
        );
        
        _cts = new  CancellationTokenSource();
        
        //Start Run Task
        _consumerTask = Task.Run(async () =>
        {
            try
            {
                await ProcessQueueLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Log4netManager.Logger.Fatal($"ProcessQueueLoopAsync crashed: {ex}");
                // 可選：設定錯誤狀態標記
            }
        });
    }

    #region [heartbeat methods]

    public void StartHeartbeat(
        ushort DAddress = 12,
        ushort DrecoveryAddress = 11,
        ushort recoveryValue = 9,
        TimeSpan? interval = null,
        byte unitId = 1)
    {
        
        // 只允許心跳啟動一次
        if (Interlocked.Exchange(ref _heartbeatStarted, 1) == 1)
            return;
        
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

        var hbInterval = interval ?? TimeSpan.FromSeconds(1);
        var ct = _heartbeatCts.Token;

        // 長時間後台運行的Task，屬於最外層了所以不需要Async去await，因為不需要等待
        // ConfigureAwait完成這個 Task 時，**等待這個 Task 的後續程式（continuation）不要在「同一個呼叫 TrySet* 的執行緒」上同步立刻執行**，而是改成**排程到 ThreadPool（非同步）**再跑
        _heartbeatTask = Task.Run(async () =>
        {
            await HeartbeatLoopAsync(DAddress, DrecoveryAddress, recoveryValue, hbInterval, unitId, ct)
                .ConfigureAwait(false);
        }, ct);

    }

    public void StopHeartbeat()
    {
        if (Interlocked.Exchange(ref _heartbeatStarted, 0) == 0)
            return;

        try
        {
            _heartbeatCts?.Cancel();
        }
        catch (Exception e)
        {
        }
        finally
        {
            _heartbeatCts?.Dispose();
            _heartbeatCts = null;
        }
    }

    private async Task HeartbeatLoopAsync(
        ushort DAddress,
        ushort DrecoveryAddress,
        ushort recoveryValue,
        TimeSpan? interval,
        byte unitId = 1, 
        CancellationToken ct = default)
    {
        ushort txId = 1;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _heartbeatValue++;
                
                var hbAdu = BuildWriteSingleRegisterAdu(
                    txId++,
                    unitId,
                    DAddress,
                    _heartbeatValue);

                _ = await SendCommandAsync(hbAdu).WaitAsync(ct);

                if (_heartbeatCommDown)
                {
                    var recAdu = BuildWriteSingleRegisterAdu(txId++, unitId, DrecoveryAddress, recoveryValue);
                    _ = await SendCommandAsync(recAdu).WaitAsync(ct);
                    _heartbeatCommDown = false;
                }
                
                // set heartbeat started only once
                if (_heartbeatStartedTcs.Task.Status != TaskStatus.RanToCompletion)
                {
                    _heartbeatStartedTcs.SetResult(true);
                }

            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                _heartbeatCommDown = true;
            }

            await Task.Delay(100); 

        }
    }
    
    private byte[] BuildWriteSingleRegisterAdu(
        ushort transactionId,
        byte unitId,
        ushort registerAddress,
        ushort value)
    {
        byte[] adu = new byte[12];
        
        // MBAP Header
        BinaryPrimitives.WriteUInt16BigEndian(adu.AsSpan(0, 2), transactionId); // Transaction ID
        BinaryPrimitives.WriteUInt16BigEndian(adu.AsSpan(2, 2), 0); // Protocol ID
        BinaryPrimitives.WriteUInt16BigEndian(adu.AsSpan(4, 2), 6); // Length
        adu[6] = unitId; // Unit ID
        
        // PDU
        adu[7] = 0x06; // Function Code: Write Single Register
        BinaryPrimitives.WriteUInt16BigEndian(adu.AsSpan(8, 2), registerAddress); // Register Address
        BinaryPrimitives.WriteUInt16BigEndian(adu.AsSpan(10, 2), value); // Value to write
        
        return adu;
    }

    #endregion
    
    // Process Queue Loop Async
    private async Task ProcessQueueLoopAsync(CancellationToken token)
    {
        await foreach (var requestJob in _commandQueue.Reader.ReadAllAsync(token))
        {
            try
            {
                try
                {
                    // Connect if not connected
                    await EnsureConnectedAsync(token);

                    // send command
                    var stream = _stream;
                    if (stream is null)
                    {
                        requestJob.Tcs.TrySetException(new InvalidOperationException("Network stream is null."));
                        continue;
                    }
                    await _stream.WriteAsync(requestJob.CommandAdu, 0, requestJob.CommandAdu.Length, token);

                    // read response
                    byte[] response = await ReadModbusResponseAsync(token);

                    // success and feed backresult
                    requestJob.Tcs.TrySetResult(response);
                }
                catch (OperationCanceledException)
                {
                    requestJob.Tcs.TrySetCanceled(token);
                }
                catch (Exception ex)
                {
                    requestJob.Tcs.TrySetException(ex);
                }
               
            }
            catch(OperationCanceledException)
            {
                requestJob.Tcs.TrySetCanceled();
                break; // loop exit
            }
            catch (Exception ex)
            {
                requestJob.Tcs.TrySetException(ex);

                // 如果是網路錯誤，關閉連線，下次會自動重連
                if (ex is IOException || ex is SocketException)
                {
                    CloseConnection();
                }
                
                // TODO: 記錄日誌 (Log)
                Console.WriteLine($"[Modbus Error] {ex.Message}");
            }

        } 
    }

    // Public Api to add command
    public Task<byte[]> SendCommandAsync(byte[]? modbusAdu)
    {
        
        if(modbusAdu == null || modbusAdu.Length == 0)
        {
            return Task.FromException<byte[]>(new ArgumentException("modbusAdu is null or empty."));
        }
        
        if (_cts.IsCancellationRequested || _commandQueue.Reader.Completion.IsCompleted)
        {
            return Task.FromException<byte[]>(new InvalidOperationException("ModbusTcpManager is shutting down."));
        }

        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new PendingRequest(modbusAdu, tcs);

        if (!_commandQueue.Writer.TryWrite(request))
        {
            return tcs.Task;
        }
        
        // 外部是使用await，會卡在下面tcs部分，直到物件的tcs.setvalue被呼叫
        return tcs.Task;

    }
    
    public async Task<bool> ConnectServerAsync()
    {
        try
        {
            await EnsureConnectedAsync(CancellationToken.None);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }

        // wait a moment for heartbeat to start
        await Task.Delay(100);

        if (! await IsHealthyAsync())
        {
            return false;
        }
        return true;
    }
    
    // async Task EnsureSonnect
    private async Task EnsureConnectedAsync(CancellationToken token)
    {
        try
        {
            if (_client == null)
            {
                CloseConnection();
                _client = new TcpClient();
            }

            if (_client.Connected)
            {
                return;
            }
            
            // CancellationToken timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(ConnectionTimeout);
            
            // Connect Server here
            var connectTask =  _client.ConnectAsync(_serverIp, _serverPort);
            await connectTask.WaitAsync(TimeSpan.FromMilliseconds(ConnectionTimeout), token);
            
            _stream = _client.GetStream();
            _stream.ReadTimeout = 5000;
            _stream.WriteTimeout = 5000;
        }
        catch (TimeoutException)
        {
            CloseConnection();
            throw new TimeoutException($"Connect {_serverIp}:{_serverPort} timeout after {ConnectionTimeout} ms.");
        }
        catch (Exception e)
        {
            CloseConnection();
            throw;
        }
        
    }

    private async Task<byte[]> ReadModbusResponseAsync(CancellationToken token)
    {
        
        // Read header (6 bytes)
        byte[] header = new byte[6];
        await _stream.ReadExactlyAsync(header, 0 ,6, token);
        
        // Get remain DataLength 
        int remainingLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        
        // Read remain Data
        byte[] responseAdu = new byte[6 + remainingLength];
        
        // 複製已讀 header 進回傳緩衝
        Buffer.BlockCopy(header, 0, responseAdu, 0, 6);
        
        if (remainingLength > 0)
        {
            await _stream.ReadExactlyAsync(responseAdu, 6, remainingLength, token);
        }

        return responseAdu;
    }

    private void CloseConnection()
    {
        if (_stream != null)
        {
            try
            {
                _stream.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        if (_client != null)
        {
            try
            {
                if (_client.Client != null && _client.Client.Connected)
                {
                    _client.Client.Shutdown(SocketShutdown.Both);
                }
                _client.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        
    }

    public void Dispose()
    {
        try
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            _commandQueue.Writer.TryComplete();

            try
            {
                _consumerTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, not an error
            }
            catch (Exception ex)
            {
                Log4netManager.Logger.Error($"Error waiting for consumer task: {ex}");
            }
            
            CloseConnection();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
}