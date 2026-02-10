using ShufflerWPF.Model;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using ShufflerWPF.Manager;
using TheNewIdea.Manager;

namespace ShufflerWPF.SingleTon;

public static class DataCenter
{
    public static readonly string TGcontentType = "application/x-www-form-urlencoded; charset=UTF-8";
    public static readonly string ShufflercontentType = "application/x-www-form-urlencoded;";
    //public static string UserId { get; set; } = string.Empty;
    public static string NowDomain { get; set; } = string.Empty;
    public static TrueBoxModel CurrentTrueBox { get; set; } = new TrueBoxModel();
    public static bool AdministratorMode { get; set; } = false;
    
    public static MemberInfoWebServiceModel? CurrentMember { get; set; }= new MemberInfoWebServiceModel();

    // public static string WebServiceActionApiUrl { get; set; } = string.Empty;
    // public static string WebServiceGetTrueIdUrl { get; set; } = string.Empty;
    public static List<TrueBoxModel> TrueBoxGlobalList { get; set; } = new List<TrueBoxModel>();
    public static object GlobalListLock { get; set; } = new object();
    
    public static SoftWareSettingsModel MyShufflerSettings { get; set; } = new SoftWareSettingsModel();
    public static bool AppExiting { get; set; } = false;
    public static bool ShufflerLoseConnect { get; set; } = false;
    
    public enum ScanType
    {
        normal=0,
        delete=1,
    }
    
    
    /// <summary>
    /// 將 "yyyy/MM/dd HH:mm:ss" 格式的日期字串轉換為 "yyMMMdd" 格式 (例如 "25Sep10")。
    /// </summary>
    /// <param name="inputDateString">輸入的日期時間字串，例如 "2025/09/10 10:00:000"。</param>
    /// <returns>返回轉換後的格式，如果輸入格式不正確則返回錯誤訊息。</returns>
    public static string FormatDateToCustomStyle(string inputDateString)
    {
        // 步驟 1: 檢查輸入是否為空，增加程式的穩健性。
        if (string.IsNullOrWhiteSpace(inputDateString))
        {
            return "null";
        }

        // 步驟 2: 擷取日期部分。因為時間部分我們不需要，直接用空白字元切割，取第一個元素。
        string[] parts = inputDateString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            throw new ArgumentException("Invalid date format");
        string datePart = parts[0]; // 結果為 "2025/09/10"
        //string datePart = inputDateString.Split(' ')[0]; // 結果為 "2025/09/10"

        // 步驟 3: 嘗試將日期字串解析為 DateTime 物件。
        // 使用 TryParseExact 可以嚴格確保輸入格式為 "yyyy/MM/dd"，避免解析錯誤。
        // CultureInfo.InvariantCulture 確保在任何地區設定的電腦上都能正確解析 "/" 分隔符號。
        if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
        {
            // 步驟 4: 解析成功後，將 DateTime 物件格式化為目標字串。
            // "yy": 兩位數年份 (25)
            // "MMM": 三個字母的月份縮寫 (Sep)
            // "dd": 兩位數日期 (10)
            // 為確保月份一定是英文縮寫 (Sep)，我們特別指定使用 "en-US" 文化來進行格式化。
            return parsedDate.ToString("MMM. dd yyyy", new CultureInfo("en-US"));
        }
        else
        {
            // 如果輸入的日期格式不符合 "yyyy/MM/dd"，則返回錯誤提示。
            return "invalid date format";
        }
    }
    
    // /// <summary>
    // /// ApiProxy Transfer Sender
    // /// </summary>
    // /// <param name="url"></param>
    // /// <param name="oripayload"></param>
    // /// <param name="encryKey"></param>
    // /// <param name="datacontentType"></param>
    // /// <param name="sendType"></param>
    // /// <returns></returns>
    // public static async Task<(string? externalRaw, string? error)> APIProxySender(string url, object oripayload, string encryKey = null, string datacontentType="application/x-www-form-urlencoded",WebserviceSingleTonManager.ApiProxyType sendType = WebserviceSingleTonManager.ApiProxyType.Parallel,Dictionary<string,string>header=null,HttpMethod method = null)
    // {
    //     
    //     // string[] apiproxyUrls {get;set;}
    //     // string Targeturl  {get;set;}
    //     // object oripayload  {get;set;}
    //     // string encryKey {get;set;} = null
    //     // string datacontentType  {get;set;} ="application/x-www-form-urlencoded"
    //     // WebserviceSingleTonManager.ApiProxyType sendType {get;set;} = WebserviceSingleTonManager.ApiProxyType.Parallel
    //     // Dictionary<string,string>header  {get;set;} =null
    //     // HttpMethod method {get;set;} = null
    //     
    //     string orijson = JsonSerializer.Serialize(oripayload);
    //     Log4netManager.Logger.Info($"original request:{orijson}");
    //     string? oriencrypted = string.Empty;
    //     object oriform = oripayload;;
    //     
    //     // 1. Override encrypt when encryKey is not null
    //     if (encryKey != null)
    //     {
    //         oriencrypted = EncryptWithJava.Encrypt3DesECB(orijson,EncryptWithJava.EncryKeyShuffler);
    //         if(oriencrypted==null)
    //         {
    //             return (null,"Encrypt3DesECB original payload fail");
    //         }
    //         //oriform = new Dictionary<string, string> { { "encryptedMessage", oriencrypted } };
    //
    //         oriform = new { encryptedMessage = oriencrypted };
    //     }
    //     
    //    
    //     try
    //     {
    //         // 2.Transfer payload to new payload
    //         //string uuid = Guid.NewGuid().ToString();
    //         header.Add("request-source-uuid", Guid.NewGuid().ToString());
    //      
    //         var apipayload = new
    //         {
    //             targetUrl = url,
    //             method = method.ToString(),
    //             contentType = datacontentType,
    //             body = oriform,
    //             headers = header,
    //             query = "",
    //             //requestSourceUuid = uuid,
    //             timeout = 5000,
    //         };
    //         string apipayloadJson = JsonSerializer.Serialize(apipayload);
    //         Log4netManager.Logger.Info($"APIProxySender request:{apipayloadJson}");
    //         
    //         //// **Encrypt entire apiproxy payload here
    //         // string? encrypted = EncryptWithJava.Encrypt3DesECB(json,EncryptWithJava.EncryKeyApiProxy);
    //         // if(encrypted==null)
    //         // {
    //         //     return (null,"ApiProxyEncrypt3DesECB original payload fail");
    //         // }
    //         // var apipayloadForm = new Dictionary<string, string> { { "encryptedMessage", encrypted } };
    //         // Log4netManager.Logger.Info($"APIProxySender encrypted :{encrypted}");
    //         
    //         //// **Without encrypt entire apiproxy payload  and use form data
    //         // var apipayloadForm = new Dictionary<string, string>
    //         // {
    //         //     { "targetUrl", url },
    //         //     { "method", "POST" },
    //         //     { "contentType", datacontentType },
    //         //     { "body", JsonSerializer.Serialize(oriform) }, // 把內層物件序列化成字串塞進去
    //         //     { "requestSourceUuid", uuid },
    //         //     { "timeout", "5000" },
    //         // };
    //         
    //         // 3.new a My Apiproxy request item here
    //         var tcs = new TaskCompletionSource<string>();
    //         var requestItem = new WebserviceSingleTonManager.WebRequestAPIProxyItem
    //         (
    //             url: DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
    //             method: HttpMethod.Post,//fixed post method
    //             tcs,
    //             apipayloadJson,
    //             WebserviceSingleTonManager.RequestModelType.ApiProxy,
    //             sendType//parallel or sequential
    //         );
    //         
    //         // 3.1 Add custom headers if apiproxy need
    //         //requestItem.Headers.TryAdd()....
    //         
    //         // 4.Send request
    //         string resp = await WebserviceSingleTonManager.Instance.SendFormPostAsync(requestItem);
    //         Log4netManager.Logger.Info($"CreateTrue response:{resp}");
    //         var model = JsonSerializer.Deserialize<APIProxyJsonModel>(resp);
    //         
    //         // 5.Handle response
    //         // **檢查Apiproxy和Enepoint的status code有沒有打成功，最終的body內容是否正確或有沒有錯誤信息由外部判定
    //         if (model?.proxyStatusCode!=null && WebserviceSingleTonManager.Instance.IsApiSuccessStatusCode(model.proxyStatusCode))//ApiProxy Url code is pass 
    //         {
    //             if (WebserviceSingleTonManager.Instance.IsApiSuccessStatusCode(model.endpointStatusCode) && !string.IsNullOrEmpty(model.payload?.externalRaw))//Endpoint Url code is pass 
    //             {
    //                 return (model.payload.externalRaw,string.Empty);
    //             }
    //             else
    //             {
    //                 return (null, model.message ?? string.Empty);//Endpoint code fail. return message.
    //             }
    //         
    //         }
    //         else
    //         {
    //             return (null, model?.message ?? string.Empty);//ApiProxy Url  code fail.return message.
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //        
    //         try
    //         {
    //             // ex.Message 可能是 ApiProxy 的 JSON
    //             var apiProxyModel = JsonSerializer.Deserialize<APIProxyJsonModel>(ex.Message);
    //             if (apiProxyModel != null && !string.IsNullOrEmpty(apiProxyModel.message))
    //             {
    //                 // 這裡的 message 就是：
    //                 // {"code":0,"message":"Failed to get User information: record not found"}
    //                 return (null, apiProxyModel.message);
    //             }
    //         }
    //         catch
    //         {
    //             // ex.Message 不是 ApiProxy JSON，就忽略解析錯誤
    //         }
    //
    //         // 退回原本邏輯
    //         return (null, $"Exception Message [{ex.Message}]");
    //     }
    //    
    // }
    //
    //
    
    
    
    public class APIProxyRequestItem
    {
        /// <summary>
        /// ApiProxy Urls
        /// </summary>
        public string[] ApiProxyUrls { get; }
        
        /// <summary>
        /// Endpoint Url
        /// </summary>
        public string Targeturl  {get;set;}
        
        /// <summary>
        /// Endpoint Url Http Method
        /// </summary>
        public HttpMethod EndpointMethod { get; set; } = HttpMethod.Post;
        
        /// <summary>
        /// Endpoint Url Http Method
        /// </summary>
        public string EndpointContentType { get; set; } = "application/x-www-form-urlencoded";
        
        /// <summary>
        /// contentType is application/json, use Json string
        /// </summary>
        public object? oripayload { get; }
        
        /// <summary>
        /// encrypt key for oripayload. By far, only shuffler use it.
        /// </summary>
        public string EndpointencryKey { get; set; } = null;
        
        /// <summary>
        /// Endpoint Url headers
        /// </summary>
        public Dictionary<string, string>? EndpointHeaders { get; } 
        
        /// <summary>
        /// ApiProxy Send Type: Parallel or Sequential
        /// </summary>
        public WebserviceSingleTonManager.ApiProxyType sendType { get; } = WebserviceSingleTonManager.ApiProxyType.Parallel;
        
        /// <summary>
        /// ApiProxy,BiService,TelegramBot
        /// </summary>
        public WebserviceSingleTonManager.RequestModelType? Modeltype { get; } = WebserviceSingleTonManager.RequestModelType.ApiProxy;
        public APIProxyRequestItem(string[] urls, string endpointUrl,
            HttpMethod method, 
            object? endPointpayload = null, 
            Dictionary<string, string>? headers = null,
            WebserviceSingleTonManager.RequestModelType? modeltype = null,
            WebserviceSingleTonManager.ApiProxyType sendmode=WebserviceSingleTonManager.ApiProxyType.Parallel)
        {
            ApiProxyUrls = urls;
            Targeturl = endpointUrl;
            EndpointMethod = method;
            oripayload  = endPointpayload;
            EndpointHeaders = headers;
            Modeltype = modeltype;
            sendType = sendmode;
        }
    }
    
    
    
     /// <summary>
    /// ApiProxy 1. do Encrypy 2.Transfer to WebRequestAPIProxyItem
    /// </summary>
    /// <param name="url"></param>
    /// <param name="oripayload"></param>
    /// <param name="encryKey"></param>
    /// <param name="datacontentType"></param>
    /// <param name="sendType"></param>
    /// <returns></returns>
    public static async Task<(string? externalRaw, string? error)> APIProxySenderByItem(APIProxyRequestItem apirequestItem)
    {
        
        string orijson = JsonSerializer.Serialize(apirequestItem.oripayload);
        Log4netManager.Logger.Info($"original request:{orijson}");
        string? oriencrypted = string.Empty;
        object oripayload = apirequestItem.oripayload;;
        
        // 1.1 Override encrypt when encryKey is not null
        if (apirequestItem.EndpointencryKey != null)
        {
            oriencrypted = EncryptWithJava.Encrypt3DesECB(orijson,EncryptWithJava.EncryKeyShuffler);
            if(oriencrypted==null)
            {
                return (null,"Encrypt3DesECB original payload fail");
            }
            //oriform = new Dictionary<string, string> { { "encryptedMessage", oriencrypted } };

            oripayload = new { encryptedMessage = oriencrypted };
            
        }
        
        // 1.2 Check ApiProxy Url format
        if (string.IsNullOrEmpty(DataCenter.MyShufflerSettings.ApiProxyUrl))
        {
            return (null,"ApiProxyUrl format error");
        }
        
        
       
        try
        {
            // 2. Get uuid from headers if exists
            string uuid=string.Empty;
            if ( apirequestItem.EndpointHeaders != null &&
                 apirequestItem.EndpointHeaders.TryGetValue("request-source-uuid", out var existedUuid) &&
                 !string.IsNullOrWhiteSpace(existedUuid))
            {
                uuid = existedUuid;// to log use
            }

            // 3. Create Endpoint original Body
            var endpointpayload = new
            {
                targetUrl = apirequestItem.Targeturl,
                method =apirequestItem.EndpointMethod.ToString(),
                contentType = apirequestItem.EndpointContentType,
                body = oripayload,
                headers = apirequestItem.EndpointHeaders,
                timeout = 5000,
            };
            string endpointpayloadJson = JsonSerializer.Serialize(endpointpayload);
            Log4netManager.Logger.Info($"APIProxy endpoint send payload:{endpointpayloadJson}");
            
            
            // 4.1 Transfer payload to second payload. Encrypt entire endpoint payload here
            var endpointoriencrypted = EncryptWithJava.Encrypt3DesECB(endpointpayloadJson,EncryptWithJava.EncryKeyApiProxy);
            if (endpointoriencrypted == null)
            {
                return (null, "Encrypt3DesECB original payload fail");
            }
            
            // 4.2 Create final body for ApiProxy. The content type must always be application/x-www-form-urlencoded.
            var oriform = new Dictionary<string, string>
            {
                { "encryptedPayload", endpointoriencrypted }
            };
            //oriform = new Dictionary<string, string> { { "encryptedMessage", oriencrypted } };
            //var finaloripayloadbody = new { encryptedPayload = endpointoriencrypted };
            //var finaloripayloadbodyson = JsonSerializer.Serialize(finaloripayloadbody);
            
            // 5. Create a WebRequestAPIProxyItem here
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var requestItem = new WebserviceSingleTonManager.WebRequestAPIProxyItem
            (
                url: DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                method: HttpMethod.Post,//fixed post method
                tcs,
                oriform,
                WebserviceSingleTonManager.RequestModelType.ApiProxy,
                apirequestItem.sendType//parallel or sequential
            );
            requestItem.Recoiduuid = uuid;// for log use
            
            // 6. Send request
            string resp = await WebserviceSingleTonManager.Instance.SendFormPostAsync(requestItem);
            var model = JsonSerializer.Deserialize<APIProxyJsonModel>(resp);
            
            // 7.1 Handle response
            // **檢查Apiproxy和Enepoint的status code有沒有打成功，最終的body內容是否正確或有沒有錯誤信息由外部判定
            
            
            // 7.2 Check model.proxyStatusCode
            if (model?.proxyStatusCode == null || 
                !WebserviceSingleTonManager.Instance.IsApiSuccessStatusCode(model.proxyStatusCode))
            {
                // ApiProxy URL 失敗
                return (null, model?.message ?? string.Empty);
            }

            // 7.3 Check model.endpointStatusCode
            if (WebserviceSingleTonManager.Instance.IsApiSuccessStatusCode(model.endpointStatusCode) && 
                !string.IsNullOrEmpty(model.payload?.externalRaw))
            {
                // Endpoint 成功
                return (model.payload.externalRaw, string.Empty);
            }

            // 7.4 Endpoint 失敗
            return (null, model?.message ?? string.Empty);
        }
        catch (Exception ex)
        {
           
            try
            {
                // ex.Message 可能是 ApiProxy 的 JSON
                var apiProxyModel = JsonSerializer.Deserialize<APIProxyJsonModel>(ex.Message);
                if (apiProxyModel != null && !string.IsNullOrEmpty(apiProxyModel.message))
                {
                    // 這裡的 message 就是：
                    // {"code":0,"message":"Failed to get User information: record not found"}
                    return (null, apiProxyModel.message);
                }
            }
            catch
            {
                // ex.Message 不是 ApiProxy JSON，就忽略解析錯誤
            }

            // 退回原本邏輯
            return (null, $"Exception Message [{ex.Message}]");
        }
       
    }

}