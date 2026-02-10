using System.Buffers.Binary;
using System.Collections.Concurrent;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;
using ShufflerWPF.BaseInterFace;
using System.Net.Sockets;

namespace ShufflerWPF.Manager;

public class ClarkShufflerManager:ShufflerBase
{
    private static readonly Lazy<ClarkShufflerManager> _instance = new(() => new ClarkShufflerManager());
    public static ClarkShufflerManager Instance => _instance.Value;
    
    private volatile bool _connected;
    private static readonly object _listLock = new object();
    private readonly List<ShufflerStatus> _sufflerstatusList = new List<ShufflerStatus>();
    private readonly ConcurrentDictionary<string, ClarkShufflerItem> _allClarkItem = new();
    private ClarkShufflerItem? _firstItemRef;
    private readonly object _lock = new();
    private CancellationTokenSource? _loopCts;
    public Task? _loopTask;
    private const int DEFAULT_MODBUS_PORT = 8866;
    private static List<string> _bootstrapIps = new();
    private static int _initFlag;

    // 管理第一個建立的項目，用於 PollLoopAsync 等待
    // 通知 PollLoopAsync：至少已經有第一個 ClarkShufflerItem 被建立，可以開始輪詢了。
    private readonly TaskCompletionSource<ClarkShufflerItem> _firstItemTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    
    // read-only property屬性，每次更新
    private static string[] AllClarkShufflerIpArray =>
        (allClarkShufflerIp ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string allClarkShufflerIp // it's all ip string from setting.ini
    {
        get;
        set;
    }

    private string _targetIP = string.Empty;
    public string TargetIP 
    { 
        get => _targetIP;
        set => _targetIP = value;
    }
    
    /// <summary>
    /// List有更變的時候呼叫此invoke
    /// </summary>
    private void NotifyListChanged()
    {
        // 這邊雖然是copy但是物件也會綁定
        List<ShufflerStatus> listCopy;
        lock (_listLock)
        {
            listCopy = _sufflerstatusList.ToList();
        }

        // Update ListBox of AutoPage and Setting Page!!!
        //ShufflerStatusChangeAction?.Invoke(listCopy);
        OnShufflerStatusChange(listCopy);
    }
    
    public class ClarkShufflerItem
    {
        // 該 IP 專屬的連線管理器 (負責發送/接收)
        public ModbusTCPManager ConnectionManager { get; }
    
        // 該 IP 專屬的狀態物件 (負責給 UI 綁定)
        public ShufflerStatus Status { get; }

        public ClarkShufflerItem(ModbusTCPManager manager, ShufflerStatus status)
        {
            ConnectionManager = manager;
            Status = status;
        }
    }
    
    
    public static void Initialize(IEnumerable<string>? ipList)
    {
        // 只允許設定一次
        if (Interlocked.CompareExchange(ref _initFlag, 1, 0) != 0)
            return;

        _bootstrapIps = ipList == null? new List<string>()
            : ipList
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
    
    public override bool ConnectStatus => _connected;

    public ClarkShufflerItem GetorAddClarkItem(string shufflerIp, ModbusTCPManager mgr = null)
    {
        if (string.IsNullOrEmpty(shufflerIp))
        {
            throw new ArgumentException("shiffler ip is null or empty");
        }

        // 如果該IP已存在，直接從List抓取該ClarkShufflerItem
        // 否則建立新的ClarkShufflerItem並加入List
        return _allClarkItem.GetOrAdd(shufflerIp, ip =>
        {
            var status = new ShufflerStatus
            {
                ShufflerId = ip
            };
            
            var created = new ClarkShufflerItem(mgr, status);
            
            // first ok
            // 只在第一次建立時完成 TCS
            if (Interlocked.CompareExchange(ref _firstItemRef, created, null) == null)
            {
                _firstItemTcs.TrySetResult(created);
            }

            return created;
        });
        
    }
    
    public override async Task<bool> ConnectShuffler()
    {
        // demo
        allClarkShufflerIp = "192.168.1.77";
        //allClarkShufflerIp = DataCenter.MyShufflerSettings.ShufflerMachineServerIp;
        
        // Connect all ClarkShuffler IPs
        foreach (var _ip in AllClarkShufflerIpArray)
        {
            try
            {
                // add ip and new ClarkItem to list
                
                
                // 建構式會確立連線!!! 不要在建構式確立連線!!!!!!
                // 我們先連線、heartbeat然後再建構物件加入倒List
                var newShuffler = new ModbusTCPManager(_ip, DEFAULT_MODBUS_PORT);
                
                // Check target shuffler polling task is healthy and heartbeat is ok
                if (!await newShuffler.ConnectServerAsync())
                {
                    throw new InvalidOperationException("ModbusTCPManager ConnectServerAsync failed.");
                }

                // if (!await VerifyShufflerHeartBeat(mgr, _ip, 3, 1000))
                // {
                //    throw new Exception("Heartbeat verification failed.");
                // }
                
                // send a heartbeat to check connection
                // byte[] heartbeatTest = BuildReadHoldingRegistersRequest(transactionId: 1, unitId:1,startAddress: 0x0000,quantity:1);
                // byte[] rheartbeat_status = await mgr.SendCommandAsync(heartbeatTest);
                // string heartack = analyzeClarkResponseData(rheartbeat_status,AnalyzeType.ack);
                
                // Check ack is ok
                // if (ack)
                // {
                //     .....
                // }
                
                // if this newShuffler is OK, create a ClarkItem(ip, Modbusmanager) add to list
                var targetshuffler = GetorAddClarkItem(_ip, newShuffler);

                // check health again after added to list
                if (! await targetshuffler.ConnectionManager.IsHealthyAsync())
                {
                    throw new InvalidOperationException("ModbusTCPManager is not healthy after adding to list.");
                }
                
            }
            catch (Exception ex)
            {
                Log4netManager.Logger.Error($"ConnectShuffler failed for IP {_ip}: {ex.Message}");
                
                // Show non-blocking MessageBox to avoid freezing UI thread
                // CustomMessageBox.Show($"Failed to connect to shuffler at {_ip}\r\nError: {ex.Message}", 
                //     "Connection Error", 
                //     CustomMessageBoxButtonType.Ok, 
                //     CustomMessageBoxIcon.Error);
            }
        }
        
        _connected = true;
        
        // First time refresh List to UI.
        // 1. Get all shuffler status from dictionary
        List<ShufflerStatus> shufflerStatusList = GetShufflerStatusList();
        
        // 2.Return immediately if shufflerStatusList is empty
        if (shufflerStatusList.Count == 0)
        {
            Log4netManager.Logger.Warn("ConnectShuffler: shufflerStatusList is empty, returning early.");
            return false;
        }
        
        // 3. Copy shufflerStatusList to _sufflerstatusList(ui binded list)
        foreach (var sf in shufflerStatusList)
        {
            _sufflerstatusList.Add(sf);
        }
        
        // 4.Notify UI List Changed
        NotifyListChanged();
        
        // 5.Start Polling Task(fresh status from all shuffler ip)
        _loopCts = new CancellationTokenSource();
        CancellationToken token = _loopCts.Token;
        _loopTask = Task.Run(async () => { await PollLoopSwitchShufflerAsync(token); },token);
        
       
        
        return true;
    }

    public override async Task LockTouch(string shufflerip)
    {
        if (!_connected) return;
        // TODO: 寫入鎖定命令
        await Task.Delay(10);
    }

    public override async Task UnLockTouch(string shufflerip)
    {
        if (!_connected) return;
        // TODO: 寫入解鎖命令
        await Task.Delay(10);
    }

    public override async Task DoShuffle(string shufflerip)
    {
        if (!_connected) return;
        // TODO: 發送啟動洗牌命令
        await Task.Delay(10);
    }

    public override async Task StopShuffle(string shufflerip)
    {
        if (!_connected) return;
        // TODO: 發送停止 / 重置命令
        await Task.Delay(10);
    }

    public override List<ShufflerStatus> GetShufflerStatusList()
    {
        lock (_listLock) 
        {
            //Dictionary to List and return
            return _allClarkItem.Values.Select(instance => instance.Status).ToList();
        }
    }

    public override void CloseShuffler()
    {
        if (!_connected) return;
        // TODO: 關閉連線/釋放資源
        _connected = false;
        
        _loopCts?.Dispose();
        _loopCts?.Cancel();
    }

    public override void Dispose()
    {
        CloseShuffler();
    }
    
    private async Task PollLoopSwitchShufflerAsync(CancellationToken cts) 
    {
        Boolean first = true;
        //Log4netManager.Logger.Error($"SocketReceive Task Enter!");
        //Console.WriteLine($"SocketReceive Task Enter!");

        // 可取消地等待第一個項目
        // 等待有第一台洗牌機被加入到List
        ClarkShufflerItem? firstItem = null;
        try
        {
            #if NET6_0_OR_GREATER
                firstItem = await _firstItemTcs.Task.WaitAsync(cts);
            #else
                firstItem = await WaitForFirstItemAsync(token); // 自行實作 WithCancellation
            #endif
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while(!cts.IsCancellationRequested)
        {
            
            if (DataCenter.AppExiting)
            {
                Console.WriteLine("SocketReceive detected AppExiting. Break loop.");
                break;
            }
            try
            {
                // Read all ip and refresh all status in List<shufflerstatus>!!!!!!!!!!!!!
                foreach (var _ip in AllClarkShufflerIpArray)
                {
                    _targetIP = _ip;

                    if(_targetIP == string.Empty)
                    {
                        await Task.Delay(1000,cts);
                        continue;
                    }
                
                    //連線且發送
                    //Get Target Shuffler by IP
                    var targetshuffler = GetorAddClarkItem(_targetIP);
                
                    //Get Card Count
                    var requestcardCount = BuildReadHoldingRegistersRequest(transactionId: 1, unitId:1,startAddress: 0x0003,quantity:2);
                    
                    // Remember this SendCommandAsync use tsc with runcontinuationsasynchronously to keep thread safe
                    // 如果tsc沒有 runcontinuationsasynchronously，tsc返回結果時。有可能會用原本的執行緒去執行以下的續接工作，然後就不處理_commandQueue的迴圈或是延遲_commandQueue繼續
                    byte[] cardresponse = await targetshuffler.ConnectionManager.SendCommandAsync(requestcardCount);
                    string cardcount = analyzeClarkResponseData(cardresponse,AnalyzeType.CardCount);

                    //Get Status
                    var requestStatus = BuildReadHoldingRegistersRequest(transactionId: 1, unitId:1,startAddress: 0x0000,quantity:2);
                    byte[] statusresponse =await targetshuffler.ConnectionManager.SendCommandAsync(requestStatus);
                    string shufflerstatus = analyzeClarkResponseData(statusresponse,AnalyzeType.status);

                    // refresh UI status
                    ShufflerStatus? shuffler = null;
                    lock (_listLock)
                    {
                        // 使用 LINQ 的 FirstOrDefault() 方法
                        // s => s.ShufflerId == shufflerId 是一個 Lambda 表達式
                        // 它會對列表中的每個項目進行檢查
                        shuffler = _sufflerstatusList.FirstOrDefault(s => s.ShufflerId == _targetIP);
                    }

                    if (shuffler == null)
                    {
                        Console.WriteLine($"PollLoopSwitchShufflerAsync continue: shuffler is null for IP {_targetIP}");
                        Log4netManager.Logger.Error($"PollLoopSwitchShufflerAsync continue: shuffler is null for IP {_targetIP}");
                        continue;
                    }
                    shuffler.CardCount = int.Parse(cardcount);
                    shuffler.LastStatus = (ShufflingStatus)int.Parse(shufflerstatus);
                }


                // // 主要發送端，檢查特定的Register分析洗牌機狀態
                // byte[]? response = null;
                //
                // // 讀取 unitId=1，起始位址 0x0000，數量 2 個保持暫存器
                // // 讀取保持暫存器(0x03)  MBAP(7 bytes) +PDU 可變：功能碼 + 起始位址 + 讀取筆數。
                // ushort transactionId = 1;
                // ushort protocoakId = 0;
                // byte unitId = 1;
                // byte functionCode = 0x03;
                // ushort startAddress = 0x0001;
                // ushort quantity = 2;
                // ushort writedata = 0;
                // ushort writedata2 = 0;
                //
                // ushort lengthField = (ushort)(1 + 5);
                // byte[] request = new byte[12];// 6(header) + unitId + PDU(5)
                //
                // // MBAP header (first 6 bytes)
                // // transactionId  protocoakId  lengthField   unitId
                // // 0001           0000         0006          01
                // BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), transactionId);
                // BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), protocoakId);
                // BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), lengthField);
                // request[6] = unitId;
                //
                // // PDU
                // // Fuctioncode     StartAddress    quantity
                // // 03              0000            0002
                // functionCode = 0x03;
                // quantity = 2;
                // request[7] = functionCode;
                // BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8,2),startAddress);
                // BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10,2),quantity);
                //
                // // 發送讀取命令
                // //response = await ModbusTCPManager.Instance.SendCommandAsync(request);
                
            }
            catch (OperationCanceledException opex)
            {
                // 這是正常的關閉流程 (例如呼叫 CloseShuffler)
                Console.WriteLine($"Cancelled SocketReceive loop: {opex.Message}");
                Log4netManager.Logger.Error($"Cancelled SocketReceive loop: {opex.Message}");
                break;
            }
            catch (SocketException ex)
            {
                Log4netManager.Logger.Error($"SocketReceive caught a SocketException ({ex.SocketErrorCode}).");
                Console.WriteLine($"SocketReceive caught a SocketException ({ex.SocketErrorCode}).");
            
                // 只有以下錯誤要ReconnectAsync重新開始接收
                // 記得這邊ReconnectAsync會再次ConnectServer，而ConnectServer會再次開Task去跑SocketReceive
                // 所以前一次SocketReceive一定要出結束!!!
                if (ex.SocketErrorCode == SocketError.ConnectionReset ||
                    ex.SocketErrorCode == SocketError.ConnectionRefused ||
                    ex.SocketErrorCode == SocketError.NotConnected ||
                    ex.SocketErrorCode == SocketError.TimedOut)
                {
                    CloseAndCleanupResources();
                   ///**** _ = ReconnectAsync(); 
                }
                
                break; 
            }
            catch (ObjectDisposedException)
            {
                // 當 Socket 在關閉過程中被操作，視為正常退出
                Console.WriteLine("SocketReceive loop terminated due to ObjectDisposedException, likely during shutdown.");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SocketReceive loop terminated due to an unexpected exception: {ex.Message}");
                break;
            }
            
            // Notify UI List Changed
            NotifyListChanged();

        }
    
        //this.SocketClosed();
    }
    
    public enum AnalyzeType
    {
        ack,
        status,
        CardCount,
        IP_Address,
        ErrorCode
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    private string analyzeClarkResponseData(byte[] response,AnalyzeType type)
    {
        switch (type)
        {
            case AnalyzeType.CardCount:
            {
                
            } 
                break;
            case AnalyzeType.IP_Address:
            {
                
            }
                break;
            case AnalyzeType.ErrorCode:
            {
                
            }
                break;
        }

        return "";
    }

    private static byte[] BuildReadHoldingRegistersRequest(
        ushort transactionId,
        byte unitId,
        ushort startAddress,
        ushort quantity)
    {
        // MBAP header (7 bytes) + PDU (5 bytes) = 12 bytes
        const ushort protocolId = 0; // Fixed ID
        const byte functionCode = 0x03;// Fixed function code
        ushort lengthField = (ushort)1 + 5; // UnitId + PDU
        var buf = new byte[12];
        
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0,2), transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(2,2), protocolId);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(4,2), lengthField);
        buf[6] = unitId;
        buf[7] = functionCode;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(8,2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(10,2), quantity);
        return buf;
    }

    /// <summary>
    /// 安全地關閉和清理所有連線資源。
    /// </summary>
    public void CloseAndCleanupResources()
    {
        if (_loopCts != null)
        {
            if (!_loopCts.IsCancellationRequested)
            {
                _loopCts.Cancel();
            }
            _loopCts.Dispose();
            _loopCts = null;
        }
        
    }
    
}