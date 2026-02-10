using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;
using ShufflerWPF.BaseInterFace;

namespace ShufflerWPF.Manager;

public class ShufflerManager: ShufflerBase
{
    #region [private setting]
    //private static ShufflerManager? _instance;
    private static readonly Lazy<ShufflerManager> _instance = 
        new Lazy<ShufflerManager>(() => new ShufflerManager());
        
    public static ShufflerManager Instance => _instance.Value;
    //private static readonly object _lock = new object();
    private int port = 63322;

    private Byte[] receiveBuffer = new Byte[2048];
    private Byte[] buffer = new Byte[512];
    private Int32 receiverBufferLen = 0;
    private Socket? _socket;
    private Aes aes = null;
    private Boolean isFirstSend = true;
    private Byte[] encryptIV = null;
    //private Thread thread = null;
    private Dictionary<string, ShufflerStatus> shufflerStatus = new Dictionary<string, ShufflerStatus>();
    
    
    private static readonly object _listLock = new object();
    private readonly List<ShufflerStatus> _sufflerstatusList = new List<ShufflerStatus>();
    private CancellationTokenSource? _socketCts;
    //public event Action<List<ShufflerStatus>>? ShufflerStatusChangeAction;
    
    // 【新增】: 用於重新連線的狀態旗標和鎖
    private bool _isReconnecting = false;
    // private static readonly object _reconnectLock = new object();
    
    // 【新增】: 使用 SemaphoreSlim 來保護 Socket 通訊，防止競爭條件
    private readonly SemaphoreSlim _socketLock = new SemaphoreSlim(1, 1);

    #endregion
    // public static ShufflerManager Instance
    // {
    //     get
    //     {
    //         if (_instance == null)
    //         {
    //             lock (_lock)
    //             {
    //                 _instance = new ShufflerManager();
    //             }
    //         }
    //         return _instance;
    //     }
    // }
    private readonly SemaphoreSlim _reconnectLockSlim = new SemaphoreSlim(1, 1);

    private ShufflerManager()
    {
        this.aes = Aes.Create();
        this.aes.Mode = CipherMode.CBC;
        this.aes.Padding = PaddingMode.PKCS7;
        // this.aes = new AesCryptoServiceProvider();
        // this.aes.Mode = CipherMode.CBC;
        // this.aes.Padding = PaddingMode.PKCS7;
        //this.ConnectShuffler();
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

    public override bool ConnectStatus
    {
        get { return this._socket != null && this._socket.Connected; }
    }

    /// <summary>
    /// Lock Touch
    /// </summary>
    /// <param name="selectedShuffler"></param>
    public override async Task LockTouch(string shufflerip)
    {
        await _socketLock.WaitAsync(); // 等待取得鎖
        try 
        {
            if (_socket == null || !_socket.Connected)
                return;
            
            JObject jsonlockcmd = new JObject()
            {
                { "CommandName", "LockTouch" },
                { "ShufflerId", shufflerip },
            };
            await SendByJsonPacketNewConvert(jsonlockcmd);
            await Task.Delay(500);
        }
        finally
        {
            _socketLock.Release(); // 確保釋放鎖
        }
    }
    
    /// <summary>
    /// UnLock Touch
    /// </summary>
    /// <param name="selectedShuffler"></param>
    public override async Task UnLockTouch(string shufflerip)
    {
        await _socketLock.WaitAsync(); // 等待取得鎖
        try {
        if (!_socket.Connected)
        {
            return;
        }
        JObject jsonlockcmd = new JObject()
        {
            { "CommandName", "UnlockTouch" },
            { "ShufflerId", shufflerip },
        };
        await SendByJsonPacketNewConvert(jsonlockcmd);
        await Task.Delay(500);
        }
        finally
        {
            _socketLock.Release(); // 確保釋放鎖
        }
    }

    /// <summary>
    /// 開始洗牌，先寫死驗牌次數、模式
    /// </summary>
    /// <param name="selectedShuffler"></param>
    public override async Task DoShuffle(string shufflerip)
    {
        await _socketLock.WaitAsync(); // 等待取得鎖
        try {
        JObject json = new JObject()
        {
            { "CommandName", "Shuffling" },
            { "ShufflerId",  shufflerip },
            { "Mode", -1 },//debug
            { "Count", -1 },
            { "Counting", true },
            { "NumOfDeck", 8 },
        };
        // JObject json = new JObject()
        // {
        //     { "CommandName", "Shuffling" },
        //     { "ShufflerId",  shufflerip },
        //     { "Counting", true },
        //     { "NumOfDeck", 0 },
        // };
        
        await SendByJsonPacketNewConvert(json);
        //this.socket?.Send(TranslateToPacket(jsonBytes));
        }
        finally
        {
            _socketLock.Release(); // 確保釋放鎖
        }
    }
    
    /// <summary>
    /// 開始洗牌，先寫死驗牌次數、模式
    /// </summary>
    /// <param name="selectedShuffler"></param>
    public override async Task StopShuffle(string shufflerip)
    {
        await _socketLock.WaitAsync(); // 等待取得鎖
        try {
        JObject json = new JObject()
        {
            { "CommandName", "Reset" },
            { "ShufflerId",  shufflerip },
        };
        await SendByJsonPacketNewConvert(json);
        await Task.Delay(1000);
        }
        finally
        {
            _socketLock.Release(); // 確保釋放鎖
        }
    }
    
    /// <summary>
    /// 先返回Shuffler List給上層UI,測試用，以後可刪除
    /// </summary>
    /// <returns></returns>
    public override List<ShufflerStatus> GetShufflerStatusList()
    {
        lock (_listLock) 
        {
            return _sufflerstatusList.ToList();
        }
    }

    public override void CloseShuffler()
    {
        if(_socketCts != null && !_socketCts.IsCancellationRequested)
        {
            _socketCts.Cancel();
            try
            {
                this._socket?.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during socket shutdown: {ex.Message}");
            }
            finally
            {
                this._socket?.Dispose();
            }
            
            _socketCts.Dispose();
            _socketCts = null;
            
            
        }
    }
    
    /// <summary>
    /// Connect 先寫死Key IP
    /// </summary>
    /// <returns></returns>
    public override async Task<bool> ConnectShuffler()
    {
        try {
            if (this._socket != null && this._socket.Connected)
            {
                return true;
            }
            
            CloseAndCleanupResources();
            receiverBufferLen = 0;
            encryptIV = null;
            Array.Clear(receiveBuffer, 0, receiveBuffer.Length); // 清空接收緩衝區
            Log4netManager.Logger.Info("Attempting to connect to shuffler server...");

            this.isFirstSend = true;
            String key = "1ead39afc3ed4ce98364fe00bda5583e";//debug
            String ip = DataCenter.MyShufflerSettings.ShufflerMachineServerIp;
            using(SHA256CryptoServiceProvider md5 = new SHA256CryptoServiceProvider()) {
                aes.Key = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            }
            this._socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this._socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            await this._socket.ConnectAsync(ip, this.port);
            if (!_socket.Connected)
            {
                Console.WriteLine("Socket is disconnected.");
                return false;
            }
            // this.thread = new Thread(this.SocketReceive);
            // this.thread.Start();
            _socketCts = new CancellationTokenSource();
            CancellationToken token = _socketCts.Token;
            //Log4netManager.Logger.Error($"SocketReceive Task Go!");
            Console.WriteLine($"SocketReceive Task Go!");

            //Task.Run(async () => { await SocketReceive(token); },token);

            _ = Task.Run(async () =>
            {
                try
                {
                    await SocketReceive(token);
                }
                catch (Exception ex)
                {
                    Log4netManager.Logger.Fatal($"SocketReceive task crashed unexpectedly: {ex}");
                    Console.WriteLine($"CRITICAL: SocketReceive crashed: {ex.Message}");
                }
                
            }, token);

        } catch(Exception ex) {
            //this.UILog($"Connection failed: {ex.StackTrace} {ex.Message}");
            //Log4netManager.Logger.Error($"ConnectShuffler Fail exception[{ex.Message}]");
            //Console.WriteLine($"ConnectShuffler Fail exception[{ex.Message}]");
            this._socket?.Dispose();
            return false;
        }
        
        return true;
    }

    private async Task SendByJsonPacketNewConvert(JObject jObj)
    {
        //JObject json = new JObject { { "CommandName", "LifeResponse" } };
        string jsonString = jObj.ToString(Newtonsoft.Json.Formatting.None); // ✅ 建議用壓縮格式
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);

        byte[] initpacket = TranslateToPacket(jsonBytes); // ✅ 正確呼叫方式
                    
        await this._socket.SendAsync(new ArraySegment<byte>(initpacket), SocketFlags.None);
    }

    #region [shuffler Fixed API]
        
        //主要迴圈!!!
        private async Task SocketReceive(CancellationToken cts) {
        Boolean first = true;
        //Log4netManager.Logger.Error($"SocketReceive Task Enter!");
        //Console.WriteLine($"SocketReceive Task Enter!");

        while(!cts.IsCancellationRequested)
        {
            if (DataCenter.AppExiting)
            {
                Console.WriteLine("SocketReceive detected AppExiting. Break loop.");
                break;
            }
            try
            {
                //主要接收!!!
                await ReceiveAsync(cts);
            }
            catch (OperationCanceledException)
            {
                // 這是正常的關閉流程 (例如呼叫 CloseShuffler)
                Console.WriteLine($"SocketReceive loop gracefully cancelled.");
                break;
            }
            catch (SocketException ex)
            {
                //Log4netManager.Logger.Error($"SocketReceive caught a SocketException ({ex.SocketErrorCode}).");
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
                    _ = ReconnectAsync(); 
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
            
            while(true) {
                Byte[] data = GotCommand();
                if(data == null || data.Length == 0) { break; }
                if(first) {
                    using(RSACryptoServiceProvider rsa = new RSACryptoServiceProvider()) {
                        rsa.FromXmlString("<RSAKeyValue><Modulus>pW5Q8GX37hzhogV+mYZrAQf0VtqmwUipUCqwPLXATDbUE4StAuQ4GqoggR5bHr3W2XLyj/+tRUejlw/FGAvKM2WlYNG7SBjQzhoWsz6gRRXbi6otSA5GDr97J1jAXR0OHBoatRykQ3pTDxyfhoUOslEY4H+7/OfN4CXKKpMZSk0=</Modulus><Exponent>AQAB</Exponent><P>zL727A6J9qXXdIByBlj64JNaj0Th9gkneDS0BM0dTWeZ214XRCvLjujMR2peO1iuVe/mxNoEwq/CCzMQimXyPw==</P><Q>ztfeY1yTY2D3ZBrayK5pOuJ9x7ZX7pWfP1vc2F/hL0x2tgKdsWRopzoiiMRREpYIS/19eHpW/a6Crc144mSIcw==</Q><DP>WtC1W30TasiiqXUznmcnWCdj+rpV87iZvjK6SorkXWn/j5LLhRxb3NabjW27wF0Ubt/LHzOI+wXUbv1Gb+zKJw==</DP><DQ>m20+oO9JmV2dyE0dpbrZO/RBi7aLMK6hsVx4AOdbMM2GTpJ8qHXI5hAbLyZFvW+b4G4kwEk94PYnIC7L7WKZ3Q==</DQ><InverseQ>Tkuj69/EbSUKuvrtLSqZDRR6CMuAPMRoCIkVIjBQT+UPMDIF/B5QUzTFYlZxHnjXrZ76ensfoW+mAozOBBIE0g==</InverseQ><D>kwwB6g3ZWdBWR3x20eSHjL0TVXi5rSj3RwkK6ovryFMcI9VVLDLMI/eBOQRQnnzEUzk3nwP3cpOBOC+OVBd2vNcMIrnMpJI+XyRHmgAsdh5c9A9JeQlRZleTD/v8PKpwH7yDCMuzqvZDiRIpY0PerAtCrgsgrkCtlgwXFaVFfIk=</D></RSAKeyValue>");
                        encryptIV = rsa.Decrypt(data, false);
                        aes.Mode = CipherMode.CBC;
                        first = false;
                    }
                    using(RSACryptoServiceProvider rsa = new RSACryptoServiceProvider()) { // 連線就送出公鑰
                        rsa.FromXmlString("<RSAKeyValue><Modulus>3HLRZMoigjQhmwqF72yLsKFbJdS0yr6twNd4PDQk8/GiyTM7K70KDt1MiWENZthksuHO5ZX3dgi2tbLbrgGrqwQ2FTwn35ktD2AAA6Bh9rGZbzEgd2S6ZETJv7Jl6AC5Op935fYPp2+E6p/fKC+hI2bg9T309ksZRk4NTo+TTMk=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>");
                        
                        // old method:
                        // aes.GenerateIV();
                        // Byte[] seed = rsa.Encrypt(aes.IV, false);
                        // //this.socket.Send(TranslateToPacket(seed));
                        // //await this.socket.SendAsync(TranslateToPacket(seed));
                        // await this.socket.SendAsync(new ArraySegment<byte>(seed), SocketFlags.None);
                        
                        try
                        {
                            
                            aes.GenerateIV();

                            var seed = rsa.Encrypt(aes.IV, false);

                            Console.WriteLine("Sending encrypted IV...");
                            Console.WriteLine("IV: " + Convert.ToBase64String(aes.IV));
                            Console.WriteLine("Encrypted IV: " + Convert.ToBase64String(seed));
                            byte[] seedpacket = TranslateToPacket(seed); // ✅ 正確呼叫方式
                            try
                            {
                                await this._socket.SendAsync(new ArraySegment<byte>(seedpacket), SocketFlags.None);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"SendAsync failed: {e.GetType().Name} - {e.Message}");
                                Log4netManager.Logger.Info($"SendAsync failed: {e.GetType().Name} - {e.Message}");
                                //throw;
                            }
                        }
                        catch (CryptographicException cex)
                        {
                            Console.WriteLine("Encryption error: " + cex.Message);
                        }
                        catch (SocketException sex)
                        {
                            Console.WriteLine($"Socket error ({sex.SocketErrorCode}): {sex.Message}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("General error: " + ex);
                        }
                    }

                    await Task.Delay(1000);
                    JObject json = new JObject { { "CommandName", "Initialize" } };
                    try
                    {
                        await SendByJsonPacketNewConvert(json);
                        //await this.socket.SendAsync(new ArraySegment<byte>(initpacket), SocketFlags.None);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"SendAsync failed: {e.GetType().Name} - {e.Message}");
                        Log4netManager.Logger.Info($"SendAsync failed: {e.GetType().Name} - {e.Message}");
                        //throw;
                    }
                    
                    continue;
                }

                try
                {
                    Byte[] decdata = null;
                    using(var decryptor = aes.CreateDecryptor(aes.Key, aes.IV)) {
                        decdata = decryptor.TransformFinalBlock(data, 0, data.Length);
                    }
                    String text = Encoding.UTF8.GetString(decdata);
                    this.ReceiveCommand(text);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Log4netManager.Logger.Info($"SendAsync failed: {e.GetType().Name} - {e.Message}");
                    //throw;
                }
            }
        }
        this.SocketClosed();
    }
    private async Task ReceiveAsync(CancellationToken cts) 
    {
        // Check task cancel from outside
        cts.ThrowIfCancellationRequested();
        if (_socket == null || !_socket.Connected)
        {
            throw new SocketException((int)SocketError.NotConnected);
        }

        var receiveTask = this._socket.ReceiveAsync(buffer, SocketFlags.None, cts).AsTask();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cts);

        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
    
        // Check task cancel from outside again!
        cts.ThrowIfCancellationRequested();

        // check timeout
        if (completedTask == timeoutTask)
        {
            throw new SocketException((int)SocketError.TimedOut);
        }
    
        int len = await receiveTask;

        if (len == 0) // Server closed the connection normally
        {
            throw new SocketException((int)SocketError.ConnectionReset);
        }

        // Copy Received Data to receiveBuffer
        if(receiverBufferLen + len > receiveBuffer.Length) {
            Byte[] newBuffer = new Byte[receiverBufferLen + len + 512];
            Array.Copy(receiveBuffer, newBuffer, receiverBufferLen);
            receiveBuffer = newBuffer;
        }
        Array.Copy(buffer, 0, receiveBuffer, receiverBufferLen, len);
        receiverBufferLen += len;
    }
    private Byte[] ErrorCommand = new Byte[0];
    private Byte[] GotCommand() {
        int index = -1;
        for(Int32 i = 0; i < receiverBufferLen; i++) {
            if(receiveBuffer[i] == (Byte)',') {
                index = i;
                break;
            }
        }
        if(index == -1) { return null; }
        if(index == 0) { return ErrorCommand; }
        Byte[] temp = new Byte[index];
        Array.Copy(receiveBuffer, 0, temp, 0, temp.Length);
        Int32 length = Int32.Parse(Encoding.UTF8.GetString(temp));
        if(length <= 0) { return ErrorCommand; }
        if(receiverBufferLen < (index + 1 + length)) return null;

        Byte[] data = new Byte[length];
        Array.Copy(receiveBuffer, index + 1, data, 0, length);
        Byte[] newBuffer = new Byte[receiveBuffer.Length];
        Array.Copy(receiveBuffer, index + 1 + length, newBuffer, 0, receiverBufferLen - (index + 1 + length));
        receiveBuffer = newBuffer;
        receiverBufferLen -= (index + 1 + length);
        return data;
    }
    private byte[] TranslateToPacket(byte[] data) {
        String text = Encoding.UTF8.GetString(data);
        Byte[] encdata = data;
        if(!isFirstSend) {
            if(encryptIV == null)
                throw new InvalidOperationException();
            using(var encryptor = aes.CreateEncryptor(aes.Key, encryptIV)) {
                encdata = encryptor.TransformFinalBlock(data, 0, data.Length);
            }
        } else {
            isFirstSend = false;
            encdata = new Byte[data.Length];
            Array.Copy(data, 0, encdata, 0, data.Length);
        }
        String head = $"{encdata.Length},";
        Int32 headLen = Encoding.UTF8.GetByteCount(head);
        Byte[] packet = new Byte[headLen + encdata.Length];
        Array.Copy(Encoding.UTF8.GetBytes(head), packet, headLen);
        Array.Copy(encdata, 0, packet, headLen, encdata.Length);
        return packet;
    }

    #endregion
    private async Task ReceiveCommand(String jsonText)
    {
            JObject jObj = JObject.Parse(jsonText);

            string commandName = jObj["CommandName"].ToObject<String>();
            string shufflerId = "";
            ShufflerStatus? shuffler = null;
            if(jObj.ContainsKey("ShufflerId")) 
            {
                shufflerId = jObj["ShufflerId"].ToObject<String>();
                //this.shufflerStatus.TryGetValue(shufflerId, out shuffler);
                
                lock (_listLock)
                {
                    // 使用 LINQ 的 FirstOrDefault() 方法
                    // s => s.ShufflerId == shufflerId 是一個 Lambda 表達式
                    // 它會對列表中的每個項目進行檢查
                    shuffler = _sufflerstatusList.FirstOrDefault(s => s.ShufflerId == shufflerId);
                }
            }
            Console.WriteLine($"GetJson--> {jsonText}");
            //Log4netManager.Logger.Info($"GetJson[{shufflerId}]--> {jsonText}");
            switch (commandName)
            {
                case "Ack":
                {
                    break;
                }
                case "LifeAsk":
                {
                    JObject json = new JObject { { "CommandName", "LifeResponse" } };
                    await SendByJsonPacketNewConvert(json);
                }
                break;
                case "ShufflerList":
                {
                    var list = jObj["ShufflerIdList"].ToObject<List<string>>();
                    var toRemove = new List<string>();
                    foreach (var kvp in this.shufflerStatus)
                    {
                        if (list.Contains(kvp.Key) == false)
                        {
                            toRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var id in toRemove)
                    {
                        this.shufflerStatus.TryGetValue(id, out var status);
                        this.shufflerStatus.Remove(id);
                        //this.lbxShufferList.Items.Remove(status);
                        lock (_listLock)
                        {
                            _sufflerstatusList.Remove(status);
                           
                        }
                    }
                    
                    foreach (var id in list)
                    {
                        if (this.shufflerStatus.ContainsKey(id) == false)
                        {
                            var ss = new ShufflerStatus() { ShufflerId = id };
                            //this.lbxShufferList.Items.Add(ss);
                            lock (_listLock)
                            {
                                _sufflerstatusList.Add(ss);
                            }

                            this.shufflerStatus.Add(id, ss);
                        }
                    }

                    NotifyListChanged();

                    if (list.Count > 0)
                    {
                        //this.lbxShufferList.Enabled = true;
                        //this.lbxShufferList.SelectedIndex = 0;
                    }

                    break;
                }
                case "InitializeComplete":
                {
                    //this.labelControlStatus.Text = "連線";
                    break;
                }
                   
            }
            if (shuffler != null)
            {
                switch (commandName)
                {
                    // 以下為單一洗牌機的狀態 (有ShufflerId)
                    case "ShufflingOnline":
                        shuffler.IsOnline = true;
                        break;
                    case "ShufflingOffline":
                        shuffler.IsOnline = false;
                        break;
                    case "ShufflingCompleted":
                        shuffler.IsShuffleCompleted = jObj["IsCompleted"].ToObject<bool>();
                        break;
                    case "ReplaceCompleted":
                        //this.Log($"ReplaceCompleted");
                        break;
                    case "IRStatus":
                        shuffler.CardCase = jObj["CardsCase"].ToObject<bool>();
                        break;
                    case "NumberOfCompletedShuffling":
                        shuffler.NumberOfCompletedShuffling = jObj["Count"].ToObject<int>();
                        break;
                    case "ShufflingCardCount":
                        {
                            shuffler.CardCount = jObj["CardCount"].ToObject<int>();
                            Console.WriteLine($"ShufflingCardCount--> { shuffler.CardCount}");
                        }
                        break;
                    case "ShufflingStatus":
                        shuffler.LastStatus = jObj["Status"].ToObject<ShufflingStatus>();
                        break;
                    case "ShufflingOCRReply":
                        shuffler.CardCount = jObj["Cards"].ToObject<int>();
                        shuffler.NumberOfPokerValue = jObj["NumberOfPokerValue"].ToObject<int[]>();
                        break;
                    case "ShufflingError":
                        shuffler.LastError = jObj["ErrorCode"].ToObject<string>();
                        break;
                    case "TouchLockState":
                        shuffler.IsTouchLocked = jObj["IsLocked"].ToObject<bool>();
                        break;
                }
                //this.RefreshShufflerStatus(shufflerId);
            }
    }
    
    
    
    private void SocketClosed() {
        // if(this.InvokeRequired) {
        //     this.BeginInvoke(new Action(() => { this.SocketClosed(); }));
        //     return;
        // }
        // this.labelControlStatus.Text = "斷線";
        // this.lbShufflerStatus.Text = "斷線";
        // this.gpbAction.Enabled = false;
        // this.lbxShufferList.Items.Clear();
        // this.lbxShufferList.Enabled = false;
        // this.shufflerStatus.Clear();
    }
    public override void Dispose()
    {
        CloseAndCleanupResources();
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {

        }
    }
    /// <summary>
    /// 安全地關閉和清理所有連線資源。
    /// </summary>
    public void CloseAndCleanupResources()
    {
        if (_socketCts != null)
        {
            if (!_socketCts.IsCancellationRequested)
            {
                _socketCts.Cancel();
            }
            _socketCts.Dispose();
            _socketCts = null;
        }

        if (this._socket != null)
        {
            try
            {
                if (this._socket.Connected)
                {
                    this._socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch (Exception) { /* 忽略可能的錯誤 */ }
            finally
            {
                this._socket.Close();
                this._socket.Dispose();
                this._socket = null;
            }
        }
        Log4netManager.Logger.Info("Shuffler connection resources have been cleaned up.");
    }
    
    /// <summary>
    /// 重新連線!
    /// </summary>
    private async Task ReconnectAsync()
    {
        await _reconnectLockSlim.WaitAsync();

        try
        {
            if (_isReconnecting)
            {
                Console.WriteLine("Reconnection already in progress.");
                return;
            }
            _isReconnecting = true;
            if (DataCenter.AppExiting)
            {
                Console.WriteLine("Skip ReconnectAsync because AppExiting.");
                _isReconnecting = false;
                return;
            }
            
            Console.WriteLine("Connection lost. Attempting to reconnect...");
            Log4netManager.Logger.Info("Connection lost. Attempting to reconnect...");
            const int maxRetries = 5;
            int currentRetry = 0;
            DataCenter.ShufflerLoseConnect = false;
            for (currentRetry=0; currentRetry <= maxRetries; currentRetry++)
            {
                if (DataCenter.AppExiting)
                {
                    Log4netManager.Logger.Info("Reconnect loop aborted due to AppExiting.");
                    Console.WriteLine("Reconnect loop aborted due to AppExiting.");
                    _isReconnecting = false;
                    return;
                }
                try
                {
                    // 等待時間隨重試次數增加 (指數性延遲)
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, currentRetry));
                    Log4netManager.Logger.Info($"Reconnection attempt {currentRetry}/{maxRetries} in {delay.TotalSeconds} seconds...");
                    //Console.WriteLine($"Reconnection attempt {currentRetry}/{maxRetries} in {delay.TotalSeconds} seconds...");
                    await Task.Delay(delay);
                    if (DataCenter.AppExiting) { break; }
                    bool connected = await ConnectShuffler();
                    if (connected && ConnectStatus)
                    {
                        _isReconnecting = false;
                        Log4netManager.Logger.Info("Reconnection successful!");
                        Console.WriteLine("Reconnection successful!");
                        return; // 成功後退出
                    }
                }
                catch (Exception ex)
                {
                    if (DataCenter.AppExiting)
                    {
                        Console.WriteLine("Reconnect attempt aborted (exception + AppExiting).");
                        break;
                    }
                    Log4netManager.Logger.Error($"Error during reconnection attempt {currentRetry}: {ex.Message}");
                    Console.WriteLine($"Error during reconnection attempt {currentRetry}: {ex.Message}");
                }
                if (currentRetry == 5)
                {
                    DataCenter.ShufflerLoseConnect = true;
                }
            }
            Console.WriteLine("Failed to reconnect after multiple attempts.");
            _isReconnecting = false;
            
        }
        finally
        {
            _reconnectLockSlim.Release();
        }
        
    }
 
}