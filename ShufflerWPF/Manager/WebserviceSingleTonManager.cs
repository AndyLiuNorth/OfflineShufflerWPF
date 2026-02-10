using System;
using System.Net;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using ShufflerWPF.Model;

namespace ShufflerWPF.Manager;

// --- 核心修正：讓 WebServiceManager 成為一個 Singleton ---
public class WebserviceSingleTonManager : IDisposable 
{
    // --- Singleton 模式的靜態成員 ---
    private static WebserviceSingleTonManager? _instance;
    private static readonly object _lock = new object();

    /// <summary>
    /// 提供獲取單例實例的全局訪問點。
    /// </summary>
    public static WebserviceSingleTonManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new WebserviceSingleTonManager();
                    }
                }
            }
            return _instance;
        }
    }

    // --- 類別的非靜態成員 (這些屬於單例實例) ---
    private readonly HttpClient _sharedHttpClient;
    private readonly BlockingCollection<WebRequestItem> _queue = new();
    private readonly BlockingCollection<WebRequestAPIProxyItem> _apiqueue = new();
    private readonly CancellationTokenSource _cts;

    // UI 更新事件
    //public event Action<string>? Updateui;

    
    // --- 內部類別 WebRequestItem 定義請求項目 ---
    public enum RequestModelType
    {
        ApiProxy,
        BiService,
        TelegramBot,
    }
    
    public enum ApiProxyType
    {
        Parallel,
        Sequential,
    }
    
    // 內部類別 WebRequestItem 可以保持不變，但請注意 Nullability
    private class WebRequestItem
    {
        public string Url { get; }
        public HttpMethod Method { get; }
        public string? JsonContent { get; } // 原有的 Content 改名為 JsonContent
        public Dictionary<string, string>? FormData { get; } // 新增：用於儲存 form-data 的字典
        public TaskCompletionSource<string> Tcs { get; } = new();
        public Dictionary<string, string>? Headers { get; } 
        public WebRequestItem(string url, HttpMethod method, TaskCompletionSource<string> tcs,
            string? jsonContent = null, Dictionary<string, string>? formData = null, Dictionary<string, string>? headers = null)
        {
            Url = url;
            Method = method;
            JsonContent  = jsonContent;
            FormData = formData;
            Tcs = tcs;
            Headers = headers;
        }
    }
    
    public class WebRequestAPIProxyItem
    {
        public string[] Url { get; set; }
        public HttpMethod Method { get; }
        
        public object? EndPointPayload { get; set; } // the payload for endpoint server inside APIProxy
        // public string? JsonContent { get; } //contentType is application/json, use Json string
        // public Dictionary<string, string>? FormData { get; } //ContentType is multipart/form-data, use Dictionary<string,string>
        public TaskCompletionSource<string> Tcs { get; } = new();
        public Dictionary<string, string>? Headers { get; set; } = new();
        public string? Recoiduuid { get; set; }
        public ApiProxyType sendType { get; } = ApiProxyType.Parallel;
        public RequestModelType? Modeltype { get; } = RequestModelType.ApiProxy;
        public WebRequestAPIProxyItem(string[] url, HttpMethod method, TaskCompletionSource<string> tcs,
            Object? payload = null,RequestModelType? modeltype = null,WebserviceSingleTonManager.ApiProxyType sendmode=ApiProxyType.Parallel)
        {
            Url = url;
            Method = method;
            Tcs = tcs;
            EndPointPayload = payload;
            Modeltype = modeltype;
            sendType = sendmode;
        }
    }

    // --- 私有建構函式，確保外部無法直接 new ---
    private WebserviceSingleTonManager()
    {
        
        _sharedHttpClient = new HttpClient();
        _sharedHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //_sharedHttpClient.DefaultRequestHeaders.Add("cert", "g7cCS@F@k9uVBmrY%");
        _sharedHttpClient.Timeout = TimeSpan.FromSeconds(10); // 建議在建構時設置逾時

        _cts = new CancellationTokenSource();
        // 啟動背景任務，傳入 CancellationToken
        
        // _ = Task.Run(() => ProcessQueueAsync(_cts.Token));
        
        // ApiProxy
        _ = Task.Run(() => ProcessQueueAPIProxy(_cts.Token));
        
    }

    // --- 公開的佇列發送方法 ---
    public Task<string> SendGetAsync(string url,Dictionary<string, string>? headers = null, RequestModelType? type=null)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        // var item = new WebRequestItem(url, HttpMethod.Get, tcs, null,null, headers);
        // _queue.Add(item);
        
        // // APIPROXY
        var item = new WebRequestAPIProxyItem(url.Split(';'), HttpMethod.Get, tcs, null, RequestModelType.BiService);
        item.Headers = headers;
        
        _apiqueue.Add(item);
        
        return tcs.Task;
    }

    public Task<string> SendPostAsync(string url, string jsonContent, Dictionary<string, string>? headers = null, RequestModelType? type=null)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        // var item = new WebRequestItem(url, HttpMethod.Post, tcs, jsonContent: jsonContent, headers: headers);
        // _queue.Add(item);
        //  APIPROX
         var item = new WebRequestAPIProxyItem(url.Split(';'), HttpMethod.Post, tcs, jsonContent, modeltype:type);
         _apiqueue.Add(item);
        
        return tcs.Task;
    }
    
    public Task<string> SendFormPostAsync(WebRequestAPIProxyItem requestItem)
    {
        //var tcs = new TaskCompletionSource<string>();
        _apiqueue.Add(requestItem);
        
        return requestItem.Tcs.Task;
    }

    private async Task ProcessQueueAPIProxy(CancellationToken cancellationToken)
    {
        foreach (var item in _apiqueue.GetConsumingEnumerable(cancellationToken))
        {
            
            // 1.reset last response
            string lastResponsestring = string.Empty;

            if (item.sendType == ApiProxyType.Parallel)
            {
                 List<Task<string?>> tasklist = new List<Task<string?>>();
            
                // 2.Create Share Token for all tasks.
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var shareToken = linkedCts.Token;
                var total = item.Url.Length;
                
                // 3.send and add task to list
                for (int i = 0; i < item.Url.Length; i++)
                {
                    Log4netManager.Logger.Warn($"SendOneAsync Parallel: {item.Method} {item.Url[i]} Start");
                    var url = item.Url[i];
                    tasklist.Add(SendOneAsync(url, item, shareToken));
                }
                
                // 4.Wait for any task complete~~~~~~~~~~~~~~~~~~~~~~~~
                while (!item.Tcs.Task.IsCompleted && tasklist.Count > 0 && !shareToken.IsCancellationRequested)
                {
                    try
                    {
                        // 5.Wait here. feed back the finished task
                        Task<string?> finifshedTask = await Task.WhenAny(tasklist);
                        tasklist.Remove(finifshedTask);
                        
                        // 6.async wait for the finished task!?!?!?!?!?
                        string? responseString = await finifshedTask;
                        if (string.IsNullOrEmpty(responseString))
                        {
                            Log4netManager.Logger.Warn($"ReadAsStringAsync: {item.Method} {item.Url} with responseResult: null responseBody");
                            continue;
                        }
                        lastResponsestring = responseString;

                        // 7.try parser if Type is APIProxy
                        if (item.Modeltype == RequestModelType.ApiProxy)
                        {
                            try
                            { 
                                // 8.if outer code is success and APIProxyJsonModel can be deserialize=> feed back no matter inner is success or not
                                var model = JsonSerializer.Deserialize<APIProxyJsonModel>(responseString);
                                if (model?.payload.externalRaw!=null)
                                {
                                     if (item.Tcs.TrySetResult(responseString))// Tcs.task IsCompleted after TrySetResult
                                     {
                                         Log4netManager.Logger.Info("APIProxy 已取得第一個成功結果。");
                                         //await linkedCts.CancelAsync();
                                     }
                                    
                                }
                                else
                                {
                                    Log4netManager.Logger.Info($"APIProxy response fail: outer code fail:{model?.proxyStatusCode}. Keep waiting...");
                                }
                            }
                            catch (Exception e)
                            {
                                Log4netManager.Logger.Warn("Deserialize APIProxyJsonModel fail:"+e.Message);
                                continue;
                            }
                        }
                        else//normal method
                        {
                             if (item.Tcs.TrySetResult(responseString))//基本上Apiproxy以外的Server是沒有多個URL的，可不須CancelAsync
                             {
                                 Log4netManager.Logger.Info("已取得第一個成功結果，取消其它請求。");
                                 //await linkedCts.CancelAsync();
                             }
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                        //Maybe canceled by another task success.
                        if (!item.Tcs.Task.IsCompleted)
                        {
                            Log4netManager.Logger.Warn($"ProcessQueueAPIProxy Task IsCompleted Cancel other: {oce.Message}");
                            if (shareToken.IsCancellationRequested) break;
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        // it's a crash of request, so TrySetException to finish the TCS task.
                        if (!item.Tcs.Task.IsCompleted && tasklist.Count == 0)
                        {
                            Log4netManager.Logger.Warn($"ProcessQueueAPIProxy Task IsCompleted Cancel other: {ex.Message}");
                            item.Tcs.TrySetException(new Exception(ex.Message));
                        }
                    }
                    
                }
                
                // all task processed here
                // try { await Task.WhenAll(pending); } catch { /* ignore */ }
                
                if (!item.Tcs.Task.IsCompleted)
                {
                    // if all task processed but no one success. Feed back the last response string.
                    Log4netManager.Logger.Error($"All Url tried but no one success. Feed back the last response:[{lastResponsestring}]");
                    item.Tcs.TrySetException(new Exception(string.IsNullOrEmpty(lastResponsestring) ? "All API Proxy URL Request Fail" : lastResponsestring));
                }
            }
            else if (item.sendType == ApiProxyType.Sequential)
            {
                // create a linked token for cancellation
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var sharetoken = linkedCts.Token;
                bool apiproxuSuccess = false;
                
                // run url one by one
                foreach (var url in item.Url)
                {
                    if (sharetoken.IsCancellationRequested)
                        break;

                    try
                    {
                        // send one request
                        Log4netManager.Logger.Warn($"SendOneAsync Sequential: {item.Method} {url} Start");
                        var responseString = await SendOneAsync(url, item, sharetoken);
                        if (string.IsNullOrEmpty(responseString))
                        {
                            Log4netManager.Logger.Warn($"ReadAsStringAsync: {url}");
                            continue;
                        }
                        lastResponsestring = responseString;
                        if (item.Modeltype == RequestModelType.ApiProxy)
                        {
                            try
                            {
                                var model = JsonSerializer.Deserialize<APIProxyJsonModel>(responseString);
                                if (model?.payload.externalRaw!=null)
                                {
                                    if (item.Tcs.TrySetResult(responseString))
                                    {
                                        Log4netManager.Logger.Info("APIProxy 已取得第一個成功結果。");
                                        apiproxuSuccess = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    Log4netManager.Logger.Info($"APIProxy response fail: outer code fail:{model?.proxyStatusCode}. Keep waiting...");
                                }
                            }
                            catch (Exception e)
                            {
                                Log4netManager.Logger.Warn("Deserialize APIProxyJsonModel fail:"+e.Message);
                                continue;
                            }
                        }
                        else//normal method
                        {
                            if (item.Tcs.TrySetResult(responseString))//基本上Apiproxy以外的Server是沒有多個URL的，可不須CancelAsync
                            {
                                Log4netManager.Logger.Info("已取得第一個成功結果，跳出Sequential");
                                //await linkedCts.CancelAsync();
                            }
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                        //Maybe canceled by another task success.
                        if (!item.Tcs.Task.IsCompleted)
                        {
                            Log4netManager.Logger.Warn($"ProcessQueueAPIProxy Task IsCompleted Cancel other: {oce.Message}");
                            if (sharetoken.IsCancellationRequested) 
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // it's a crash of request, so TrySetException to finish the TCS task.
                        Log4netManager.Logger.Warn($"ProcessQueueAPIProxy Task IsCompleted Cancel other: {ex.Message}");
                        item.Tcs.TrySetException(new Exception(ex.Message));
                    }
                }
                
                // finally check if success. If not, feed back last response
                if (!apiproxuSuccess && !item.Tcs.Task.IsCompleted)
                {
                    Log4netManager.Logger.Error(
                        $"Order 模式: 所有 URL 都嘗試完但沒有成功。Feed back last response:[{lastResponsestring}]");
                    item.Tcs.TrySetException(new Exception(
                        string.IsNullOrEmpty(lastResponsestring)
                            ? "All URL Request Fail (Sequential 模式)"
                            : lastResponsestring));
                }
                
            }
        }
    }

    private async Task<string?> SendOneAsync(string url, WebRequestAPIProxyItem item, CancellationToken token)
    {
        try
        {
            
            using var requestMessage = new HttpRequestMessage(item.Method, url);
            
            if (item.Headers != null)
            {
                foreach (var h in item.Headers)
                    requestMessage.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            if (item.Method == HttpMethod.Post)
            {
                switch (item.EndPointPayload)
                {
                    case string jsonText:
                        requestMessage.Content =
                            new StringContent(jsonText, System.Text.Encoding.UTF8, "application/json");// contentType is application/json
                        break;
                    case Dictionary<string, string> formData:
                        var multipart = new MultipartFormDataContent();
                        foreach (var kv in formData)
                            multipart.Add(new StringContent(kv.Value), kv.Key);
                        requestMessage.Content = multipart; //contentType is multipart/form-data auto
                        break;
                    default:
                        var json = JsonSerializer.Serialize(item.EndPointPayload);
                        requestMessage.Content =
                            new StringContent(json, System.Text.Encoding.UTF8, "application/json");// contentType is application/json
                        break;
                }
                // if (item.JsonContent != null)
                // {
                //     requestMessage.Content =
                //         new StringContent(item.JsonContent, System.Text.Encoding.UTF8, "application/json");// contentType is application/json
                // }
                // else if (item.FormData != null)// always not in this because Apiproxy is Jsoncent? no because Not only apiproxy Shuffller use same base request!!!!!!!! But DS always apiproxy
                // {
                //     var multipart = new MultipartFormDataContent();
                //     foreach (var kv in item.FormData)
                //         multipart.Add(new StringContent(kv.Value), kv.Key);
                //     requestMessage.Content = multipart; //contentType is multipart/form-data auto
                // }
            }

            // Make sure Tls12 is enabled
            if ((ServicePointManager.SecurityProtocol & SecurityProtocolType.Tls12) == 0)
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            
            
            HttpResponseMessage response = await _sharedHttpClient.SendAsync(requestMessage, token).ConfigureAwait(false);
                    
            // Success! set result and cancel other task!!!!!!!!!!!!!!!!!!!!!!
            var responsestring = await response.Content.ReadAsStringAsync(token);
            
            
            if (!response.IsSuccessStatusCode)
            {
                // 非 2xx：記錄錯誤但不拋例外，直接回傳內容讓呼叫端自行判斷
                Log4netManager.Logger.Error($"API請求失敗! 狀態碼: [{(int)response.StatusCode}] [{response.ReasonPhrase}]. URL: {item.Url}. 伺服器回應: {response}");
                return responsestring;
            }
            
            if (string.IsNullOrEmpty(item.Recoiduuid))
            {
                Log4netManager.Logger.Info($"ReadAsStringAsync:{url} with responseResult: {responsestring}");
            }
            else
            {
                Log4netManager.Logger.Info($"ReadAsStringAsync:{url} uuid:[{item.Recoiduuid}] with responseResult: {responsestring}");
            }
            
            return responsestring;
        }
        catch (OperationCanceledException oce)
        {
            return null;
        }
        catch (Exception ex)
        {
            // don't throw exception to upper layer, just return null
            // because maybe other url can success
            Log4netManager.Logger.Error($"SendOneAsync Exception URL:{url} Msg:{ex.Message}");
            return null;
        }
    }
    
    public bool IsApiSuccessStatusCode(int? statusCode)
    {
        return statusCode.HasValue && statusCode.Value >= 200 && statusCode.Value <= 299;
    }

    // --- Dispose 模式修正，用於關閉佇列和 HttpClient ---
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // BlockingCollection stop GetConsumingEnumerable after final add
            _queue.CompleteAdding();

            _cts.Cancel();
            _queue.Dispose();
            _cts.Dispose();

            // HttpClient dispose
            _sharedHttpClient.Dispose();
        }
    }
}