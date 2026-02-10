using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Net.Http;
using System.Threading;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Manager {
    public sealed class TelegramBotTypeManager {
        private static readonly HttpClient _httpClient = new HttpClient{ Timeout = TimeSpan.FromSeconds(5)}; // 共用
        private readonly string _token = string.Empty; 
        public string _chatId = string.Empty;
        
        ////https://api.telegram.org/bot492624515:AAHIU6AAnPpgs9SdCRZYBJjRqPYDa-XntQI/getUpdates
        ////https://api.telegram.org/bot492624515:AAHIU6AAnPpgs9SdCRZYBJjRqPYDa-XntQI/sendMessage
        public static readonly TelegramBotTypeManager PD2GroupFATAL = new TelegramBotTypeManager("492624515:AAHIU6AAnPpgs9SdCRZYBJjRqPYDa-XntQI", "-1002859978419");
        public static readonly TelegramBotTypeManager Pd2GroupGb= new TelegramBotTypeManager("492624515:AAHIU6AAnPpgs9SdCRZYBJjRqPYDa-XntQI", "-1002859978419");
        public static readonly TelegramBotTypeManager MexicoSupervisorGroup = new TelegramBotTypeManager("492624515:AAHIU6AAnPpgs9SdCRZYBJjRqPYDa-XntQI", "-1001761641566");
        public static readonly TelegramBotTypeManager CebuSupervisorGroup = new TelegramBotTypeManager("492624515:AAHIU6AAnPpgs9SdCRZYBJjRqPYDa-XntQI", "-1002053213924");
        //private const int timeout = 5000;

        private TelegramBotTypeManager() { }

        private TelegramBotTypeManager(string token, string chatId) {
            this._token = token;
            this._chatId = chatId;
        }
        
        
        
        
        public async Task SendMessageAsync(string msg, CancellationToken ct = default) {
            try {
                if (DataCenter.MyShufflerSettings.LiveMode == "False") { /* 可視需要過濾或改路由 */ }
                string url = $"https://api.telegram.org/bot{_token}/sendMessage";
                var payload = new { chat_id = _chatId, text = msg };
                string jsoncontent = JsonSerializer.Serialize(payload);
                await WebserviceSingleTonManager.Instance.SendPostAsync(url, jsoncontent);
            } catch (Exception ex) {
                Log4netManager.Logger.Error($"Telegram SendMessage Error: {ex.Message}");
            }
        }
        
        public async Task SendMessageGbAsync(string msg, CancellationToken ct = default) {
            try {
                var chatId = DataCenter.MyShufflerSettings.LiveMode == "False" ? "-1002624529384" : "-1002859978419"; // dev / live
                string url = $"https://api.telegram.org/bot{_token}/sendMessage";
                msg += "\r\n" + await BaseContextAsync(ct);
                var payload = new { chat_id = chatId, text = msg };
                
                // string jsoncontent = JsonSerializer.Serialize(payload);
                // await WebserviceSingleTonManager.Instance.SendPostAsync(url, jsoncontent);
                
                //var (model,errMsg) = await DataCenter.APIProxySender(url, payload,null,DataCenter.TGcontentType, WebserviceSingleTonManager.ApiProxyType.Sequential);
                
                // var customHeaders = new Dictionary<string, string>
                // {
                //     { "request-source-uuid", Guid.NewGuid().ToString() }
                // };
            
                var apirequest = new DataCenter.APIProxyRequestItem
                (
                    DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                    url,
                    HttpMethod.Post,
                    payload,
                    null,
                    WebserviceSingleTonManager.RequestModelType.ApiProxy,
                    WebserviceSingleTonManager.ApiProxyType.Sequential
                );
                //apirequest.EndpointContentType = "application/json";
            
            
                var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
                
                // var (externalRaw,errMsg) = await DataCenter.APIProxySender(url, payload, 
                //     null,  
                //     DataCenter.TGcontentType,
                //     WebserviceSingleTonManager.ApiProxyType.Sequential,
                //     null,
                //     HttpMethod.Post
                // );
                
                if (externalRaw == null) {
                    Log4netManager.Logger.Fatal($"Telegram SendMessageGb Error: {errMsg}");
                }
                
            } catch (Exception ex) {
                Log4netManager.Logger.Fatal($"Telegram SendMessageGb Error: {ex.Message}");
            }
        }
        
        private async Task<string> BaseContextAsync(CancellationToken ct = default) {
            try {
                var localIps = GetIpAddressArray();
                var external = await GetExtranetNetWorkDataAsync(ct) ?? "(external ip unknown)";
                return "Local IP:" + string.Join(" ; ", localIps ?? new List<string>()) + Environment.NewLine + "Extranet IP:" + external;
            } catch (Exception ex) { Log4netManager.Logger.Fatal($"Error: {ex}"); return string.Empty; }
        }
        private string BaseContext() => BaseContextAsync().GetAwaiter().GetResult(); // 保留呼叫若尚未移除
        
        private static List<string>? GetIpAddressArray()
        {
            try
            {
                List<string> ipList = new List<string>();
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipList.Add(ip.ToString());
                    }
                }
                return ipList;
            }
            catch (Exception ex)
            {
                Log4netManager.Logger.Fatal($"Error: {ex.Message}");
                return null;
            }
        }

        private static async Task<string?> GetExtranetNetWorkDataAsync(CancellationToken ct = default) {
            string? externalIp = null;
            try {
                var resp1 = await _httpClient.GetAsync("https://api.myip.com", ct);
                if (resp1.IsSuccessStatusCode) {
                    using var s = await resp1.Content.ReadAsStreamAsync(ct);
                    var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
                    if (doc.RootElement.TryGetProperty("ip", out var ipProp)) {
                        externalIp = ipProp.GetString();
                    }
                }
            } catch (Exception ex) { Log4netManager.Logger.Warn($"Primary external IP service failed: {ex.Message}"); }
            if (string.IsNullOrWhiteSpace(externalIp)) {
                try {
                    var resp2 = await _httpClient.GetAsync("http://icanhazip.com", ct);
                    if (resp2.IsSuccessStatusCode) {
                        var raw = (await resp2.Content.ReadAsStringAsync(ct)).Replace("\r", "").Replace("\n", "").Trim();
                        if (IPAddress.TryParse(raw, out var ipAddr)) externalIp = ipAddr.ToString();
                        else externalIp = raw; // 若服務回應格式改變
                    }
                } catch (Exception ex) { Log4netManager.Logger.Warn($"Fallback external IP service failed: {ex.Message}"); }
            }
            return externalIp;
        }

        private static string? GetExtranetNetWorkData() => GetExtranetNetWorkDataAsync().GetAwaiter().GetResult(); // 向下相容

        public static Task CallSupervisorGroup(string message) => Task.CompletedTask;

        public static Task FatalAndCallTelegram(string message) => Task.CompletedTask;
    }
}
