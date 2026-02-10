using System.Diagnostics.Eventing.Reader;
using System.Text.Json;
using System.Threading.Tasks;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;
using TheNewIdea.Manager;
using System.Net.Http;

namespace ShufflerWPF.Manager;

public static class DoTrueBoxActionManager
{
    /*
     * TrueBox action API orchestration.
     * Flow: build payload -> serialize JSON -> 3DES encrypt (encryptedMessage) -> form POST -> parse response.
     * Success criterion: response JSON code == 201.
     */

    public enum TrueBoxAction
    {
        Destroy = -1,        // Permanently destroyed
        Create = 0,          // Create new TrueID
        ManualFinished = 1,  // Manual shuffle finished
        AutoShuffling = 2,   // Auto shuffling in progress
        AutoFinished = 3,    // Auto shuffling finished
        Printed = 4,         // Printing finished
        TakeOut = 5,       // Pull-out finished
        Confirmrecept = 6,       // Pull-out finished
        Delete = 99 ,         // Logical delete
        OnTable = 7         // OnTable (for Dealer System, not used here)
    }
    public class TrueBoxActionRequest
    {
        public string trueId { get; set; }
        public int action { get; set; }
        public int? updater { get; set; }
        public string? shufflerIp { get; set; }
    }

    private static string NormalizeTrueId(string id) => id.StartsWith("QC-") ? id[3..] : id;
    
    /// <summary>
    /// Get TrueBox info (does not mutate global state).
    /// </summary>
    /// <param name="newTrueId">Scanned or entered trueId (QC- prefix optional)</param>
    /// <returns>TrueBoxModel when found; otherwise null</returns>
    public static async Task<(TrueBoxModel?,string? error)> GetTrueIdUpdateCurrentBox(string newTrueId)
    {
        string url = DataCenter.MyShufflerSettings.GetTrueIdApi;
        try
        {
            // Encrypt the command using 3DES
            // Serialize again to JSONstring
            // var payload = new {key="encryptedMessage",value=jsoncommanddes};
            // string jsondes = JsonSerializer.Serialize(payload);
            
            // 1.Check and remove QC- prefix if any
            string realtrueID = newTrueId.StartsWith("QC-") ? newTrueId.Substring(3) : newTrueId;
            
            // 2. new payload
            var nowtrueid = new
            {
                trueId = realtrueID
            };
            
            // 3.Shuffler Don't need header. But ApiProxy need uuid header.
            var customHeaders = new Dictionary<string, string>
            {
                { "request-source-uuid", Guid.NewGuid().ToString() }
            };
            
            // 4. Create APIProxy method APIProxyRequestItem
            var apirequest = new DataCenter.APIProxyRequestItem
            (
                DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                url,
                HttpMethod.Post,
                nowtrueid,
                customHeaders,
                WebserviceSingleTonManager.RequestModelType.ApiProxy,
                WebserviceSingleTonManager.ApiProxyType.Parallel
            );
            apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
            //apirequest.EndpointContentType = "application/json";
            
            // 5. Send APIProxy request
            var (externalRaw,outerErrorMsg) = await DataCenter.APIProxySenderByItem(apirequest);
            
            // 6. errMsg is the inner or outer error message.
            if (externalRaw != null && string.IsNullOrEmpty(outerErrorMsg))
            {
                var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
                //var responseTrueBoxModel = externalRaw.Deserialize<ShufflerJsonModel>();
                return (responseTrueBoxModel?.data, responseTrueBoxModel?.errMsg);
            }
            
            // 7. externalRaw is null and outerErrorMsg is not null
            return (null, !string.IsNullOrEmpty(outerErrorMsg) 
                ? outerErrorMsg 
                : "Apiproxy externalRaw failed");

            // old method
            // //debug demo!
            // string responsejson = await WebserviceSingleTonManager.Instance.SendFormPostAsync(url, formData);
            // //Log4netManager.Logger.Info($"GetTrueIdUpdateCurrentBox response :[{responsejson}]");
            // // int state = 0;
            // // if (newTrueId == "QC-1c6b27f2-74d1-4e58-99b9-8d5e88c93d48")
            // // {
            // //     state = 0;
            // // }
            // // else if (newTrueId =="QC-1c6b27f2-74d1-4e58-99b9-8d5e88c9777")
            // // {
            // //     state = 1;
            // //
            // // }
            // // else if (newTrueId == "QC-1c6b27f2-74d1-4e58-99b9-8d5e88c9888")
            // // {
            // //     state = 3;
            // // }
            // //string responsejson = "{\"code\":201,\"errMsg\":\"\",\"data\":{\"trueId\":\"" + newTrueId + "\",\"domain\":\"MDMD\",\"tableId\":\"M111\",\"colorType\":\"R\",\"isMain\":true,\"count\":5,\"updater\":\"" + DataCenter.UserId + "\",\"state\":" + state.ToString() + "}}";
            // //Console.WriteLine($"API Feeedback successufully: {responsejson}");
            //
            // // Notice this TrueBoxWebServiceResponseModel is the model for the response from the web service
            // TrueBoxWebServiceResponseModel responsemodel = JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(responsejson);
            // responsemodel.data.TagMessage = responsemodel.errMsg;
            //
            // if (responsemodel != null && responsemodel.data != null)
            // {
            //     return (responsemodel.data,string.Empty);
            // }
            // // App.Current.Dispatcher.Invoke(() =>
            // // {
            // //     DataCenter.CurrentTrueBox = responsemodel.data;
            // //     DataCenter.CurrentTrueBox.Creator = DataCenter.UserId;
            // // });
            //
            // return (null,"responsemodel is null");

        }
        catch (HttpRequestException ex)
        {
            //Console.WriteLine($"HTTP Error: {ex.Message}");
            Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
            return (null,ex.Message);
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"GetTrueId exception: {ex.Message}");
            return (null,ex.Message);
        }
    }
  
    /// <summary>
    /// Create TrueID (action = Create).
    /// </summary>
    /// <param name="selectfromui">User selection model</param>
    /// <returns>TrueId on success; empty string on failure</returns>
    public static async Task<(TrueBoxModel?,string? error)> DoTrueBoxActionZeroAsync(CreateIDPageViewModel selectfromui)
    {
        string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
        try
        {
            // 1. Get selected values from UI ViewModel
            string colorCode = selectfromui.SelectedColorType == "Red" ? "R" : "B";
            bool isMain = selectfromui.SelectedIsMain == "Regular"? true : false;
            
            // 2. Build payload
            var payload = new
            {
                domain = DataCenter.MyShufflerSettings.DoMainNumber,
                tableId = selectfromui.SelectedTableId,
                colorType = colorCode,
                action = (int)TrueBoxAction.Create,
                isMain,
                maxCount = selectfromui.maxCount,
                updater = DataCenter.CurrentMember?.id,
            };
            
            // 3. Create custom headers for APIProxy
            var customHeaders = new Dictionary<string, string>
            {
                { "request-source-uuid", Guid.NewGuid().ToString() }
            };
            
            // 4. Create APIProxy method APIProxyRequestItem
            var apirequest = new DataCenter.APIProxyRequestItem
            (
                DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                url,
                HttpMethod.Post,
                payload,
                customHeaders,
                WebserviceSingleTonManager.RequestModelType.ApiProxy,
                WebserviceSingleTonManager.ApiProxyType.Parallel
            );
            apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
            
            // 5. Send APIProxy request
            var (externalRaw,outerErrorMsg) = await DataCenter.APIProxySenderByItem(apirequest);
            
            
            // 6. errMsg is the inner or outer error message.
            if (externalRaw != null && string.IsNullOrEmpty(outerErrorMsg))
            {
                var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);

                return (responseTrueBoxModel?.data, responseTrueBoxModel?.errMsg);
            }
            
            // 7. externalRaw is null and outerErrorMsg is not null
            return (null, !string.IsNullOrEmpty(outerErrorMsg) 
                ? outerErrorMsg 
                : "Apiproxy externalRaw failed");

        }
        catch (HttpRequestException ex)
        {
            Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
            return (null,ex.Message);
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"CreateTrue exception: {ex.Message}");
            return (null,ex.Message);
        }
       
    }
    
    /// <summary>
    /// Do Shuffler action from 1 to 5.
    /// Action 2 need shufflerIp parameter.
    /// </summary>
    /// <param name="scanid"></param>
    /// <param name="actionNumber"></param>
    /// <param name="shufflerIp"></param>
    /// <returns></returns>
    public static async Task<string> DoTrueBoxActionFirstToSixAsync(string? scanid, TrueBoxAction actionNumber,string? shufflerIp = null)
    {
        if (scanid == null) return "Box id is null";
        string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
        try
        {
            // 1. Build endpoint payload
            var payload = new TrueBoxActionRequest
            {
                trueId = NormalizeTrueId(scanid),
                action = (int)actionNumber,
                updater = DataCenter.CurrentMember.id,
                shufflerIp = shufflerIp   // 為 null 時 JSON 會忽略
            };
            
            // 2.Create EndPoint header. Shuffler Don't need header. But ApiProxy need uuid header.
            // So we always add uuid header for ApiProxy.
            var customHeaders = new Dictionary<string, string>
            {
                { "request-source-uuid", Guid.NewGuid().ToString() }
            };
            
            // 3. Create APIProxy method APIProxyRequestItem
            var apirequest = new DataCenter.APIProxyRequestItem
            (
                DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                url,
                HttpMethod.Post,
                payload,
                customHeaders,
                WebserviceSingleTonManager.RequestModelType.ApiProxy,
                WebserviceSingleTonManager.ApiProxyType.Parallel
            );
            apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
            
            //if apiProxy need another content type,uncomment the below line and set it.
            //apirequest.EndpointContentType = "application/json";
            
            // 4. Send APIProxy request
            var (externalRaw,ErrorMsg) = await DataCenter.APIProxySenderByItem(apirequest);

            // 5. ErrorMsg is the inner or outer error message.
            if (externalRaw != null)
            {
                // Shuffler response body is TrueBoxWebServiceResponseModel
                var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
           
                // Return inner error message
                return responseTrueBoxModel.errMsg;
            }
          
            // 6. externalRaw is null and outerErrorMsg is not null
            return (!string.IsNullOrEmpty(ErrorMsg) 
                ? ErrorMsg 
                : $"Apiproxy Action {(int)actionNumber} externalRaw null failed");
            
        }
        catch (HttpRequestException ex)
        {
            Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
            return ex.Message;
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"AutoShuffling exception: {ex.Message}");
            return ex.Message;
        }
    }

    /// <summary>
    /// Mark manual shuffle finished (action = ManualFinished).
    /// </summary>
    /// <param name="scanid">TrueId (QC- prefix optional)</param>
    /// <returns>Empty string on success; error message otherwise</returns>
    

    /// <summary>
    /// Logical delete (action = Delete).
    /// </summary>
    /// <param name="scanid">TrueId</param>
    /// <returns>Empty string on success; error message otherwise</returns>
    public static async Task<string> DoTrueBoxActionDeleteAsync(string? scanid)
    {
        if (scanid == null) return "Box id is null";
        string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
        
        int action = (int)TrueBoxAction.Delete;
        var payload = new
        {
            trueId = NormalizeTrueId(scanid),
            action,
            deleteReason = "delete from shuffler app",
            updater = DataCenter.CurrentMember.id,
        };
        try
        {
            var customHeaders = new Dictionary<string, string>
            {
                { "request-source-uuid", Guid.NewGuid().ToString() }
            };
            
            var apirequest = new DataCenter.APIProxyRequestItem
            (
                DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                url,
                HttpMethod.Post,
                payload,
                customHeaders,
                WebserviceSingleTonManager.RequestModelType.ApiProxy,
                WebserviceSingleTonManager.ApiProxyType.Parallel
            );
            apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
            //apirequest.EndpointContentType = "application/json";
            
            
            var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
            
            // var (externalRaw,errMsg) = await DataCenter.APIProxySender(url, payload, 
            //     EncryptWithJava.EncryKeyShuffler,  
            //     DataCenter.ShufflercontentType,
            //     WebserviceSingleTonManager.ApiProxyType.Parallel,
            //     null,
            //     HttpMethod.Post
            // );
            if (externalRaw != null)
            {
                var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
                //var responseTrueBoxModel = externalRaw.Value.Deserialize<ShufflerJsonModel>();
                return responseTrueBoxModel.errMsg;
            }
            else
            {
                return (!string.IsNullOrEmpty(errMsg)? errMsg:"Apiproxy externalRaw failed");
            }
            // string json = JsonSerializer.Serialize(payload);
            // Log4netManager.Logger.Info($"Delete DoTrueBoxActionDeleteAsync request:{json}");
            // string? encrypted = EncryptWithJava.Encrypt3DesECB(json);
            // var form = new Dictionary<string, string> { { "encryptedMessage", encrypted } };
            // string resp = await WebserviceSingleTonManager.Instance.SendFormPostAsync(url, form);
            // var model = JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(resp);
            // if (model?.code == 201) return string.Empty;
            // return model?.errMsg ?? "Delete unknown error";
        }
        catch (HttpRequestException ex)
        {
            Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
            return ex.Message;
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"Delete exception: {ex.Message}");
            return ex.Message;
        }
    }

    /// <summary>
    /// Permanently destroy (action = Destroy).
    /// </summary>
    /// <param name="scanid">TrueId</param>
    /// <returns>Empty string on success; error message otherwise</returns>
    public static async Task<string> DoTrueBoxActionDestroyAsync(string? scanid)
    {
        if (scanid == null) return "Box id is null";
        string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
        
        int action = (int)TrueBoxAction.Destroy;
        var payload = new
        {
            trueId = NormalizeTrueId(scanid),
            action,
            updater = DataCenter.CurrentMember.id,
        };
        try
        {
            
            var customHeaders = new Dictionary<string, string>
            {
                { "request-source-uuid", Guid.NewGuid().ToString() }
            };
            
            var apirequest = new DataCenter.APIProxyRequestItem
            (
                DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                url,
                HttpMethod.Post,
                payload,
                customHeaders,
                WebserviceSingleTonManager.RequestModelType.ApiProxy,
                WebserviceSingleTonManager.ApiProxyType.Parallel
            );
            apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
            //apirequest.EndpointContentType = "application/json";
            
            
            var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
            // var (externalRaw,errMsg) = await DataCenter.APIProxySender(url, payload, 
            //     EncryptWithJava.EncryKeyShuffler,  
            //     DataCenter.ShufflercontentType,
            //     WebserviceSingleTonManager.ApiProxyType.Parallel,
            //     null,
            //     HttpMethod.Post
            // );
            if (externalRaw != null)
            {
                var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
                //var responseTrueBoxModel = externalRaw.Value.Deserialize<ShufflerJsonModel>();
                return responseTrueBoxModel.errMsg;
            }
            else
            {
                return (!string.IsNullOrEmpty(errMsg)? errMsg:"Apiproxy externalRaw failed");
            }
            // string json = JsonSerializer.Serialize(payload);
            // Log4netManager.Logger.Info($"Destroy DoTrueBoxActionDestroyAsync request:{json}");
            // string? encrypted = EncryptWithJava.Encrypt3DesECB(json);
            // var form = new Dictionary<string, string> { { "encryptedMessage", encrypted } };
            // string resp = await WebserviceSingleTonManager.Instance.SendFormPostAsync(url, form);
            // var model = JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(resp);
            // if (model?.code == 204) return string.Empty;
            // return model?.errMsg ?? "Destroy unknown error";
        }
        catch (HttpRequestException ex)
        {
            Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
            return ex.Message;
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"Destroy exception: {ex.Message}");
            return ex.Message;
        }
    }

    /// <summary>
    /// Notice! This function is for Demo. Action 6 is used to Dealer System.
    /// </summary>
    /// <param name="scanid"></param>
    /// <returns></returns>
    
    
    public static async Task<string> DoTrueBoxActionSeven(string? scanid)
    {
        if (scanid == null) return "Box id is null";
        string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;

        int action = (int)TrueBoxAction.OnTable;
        var payload = new
        {
            tableId = "5031",
            trueId = NormalizeTrueId(scanid),
            action,
            updater = DataCenter.CurrentMember.id,
            shoe = 123
        };
        try
        {
            var customHeaders = new Dictionary<string, string>
            {
                { "request-source-uuid", Guid.NewGuid().ToString() }
            };
            
            var apirequest = new DataCenter.APIProxyRequestItem
            (
                DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                url,
                HttpMethod.Post,
                payload,
                customHeaders,
                WebserviceSingleTonManager.RequestModelType.ApiProxy,
                WebserviceSingleTonManager.ApiProxyType.Parallel
            );
            apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
            //apirequest.EndpointContentType = "application/json";
            
            
            var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
            // var (externalRaw,errMsg) = await DataCenter.APIProxySender(url, payload, 
            //     EncryptWithJava.EncryKeyShuffler,  
            //     DataCenter.ShufflercontentType,
            //     WebserviceSingleTonManager.ApiProxyType.Parallel,
            //     null,
            //     HttpMethod.Post
            // );
            if (externalRaw != null)
            {
                var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
                //var responseTrueBoxModel = externalRaw.Value.Deserialize<ShufflerJsonModel>();
                return responseTrueBoxModel.errMsg;
            }
            else
            {
                return (!string.IsNullOrEmpty(errMsg)? errMsg:"Apiproxy externalRaw failed");
            }
            // string json = JsonSerializer.Serialize(payload);
            // Log4netManager.Logger.Info($"Action 6 DoTrueBoxActionSix request:{json}");
            // string? encrypted = EncryptWithJava.Encrypt3DesECB(json);
            // var form = new Dictionary<string, string> { { "encryptedMessage", encrypted } };
            // string resp = await WebserviceSingleTonManager.Instance.SendFormPostAsync(url, form);
            // var model = JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(resp);
            // if (model?.code == 204) return string.Empty;
            // return model?.errMsg ?? "Destroy unknown error";
        }
        catch (HttpRequestException ex)
        {
            Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
            return ex.Message;
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"Destroy exception: {ex.Message}");
            return ex.Message;
        }
    }
    
    public static async Task<string> DoTrueBoxActionDeleteByTableColorMainAsync(CreateIDPageViewModel? targetmodel)
    {
        if (targetmodel == null) return "Box id is null";
        string url = DataCenter.MyShufflerSettings.ActionDeleteby;
        string colorCode = targetmodel.SelectedColorType == "Red" ? "R" : "B";
        bool isMain = targetmodel.SelectedIsMain == "Regular"? true : false;
        
        //{"tableId":"M111","colorType":"R","isMain":true}
        
        var payload = new
        {
            tableId = targetmodel.SelectedTableId,
            colorType = colorCode,
            isMain,
            updater = DataCenter.CurrentMember.id,
        };
        try
        {
            var customHeaders = new Dictionary<string, string>
            {
                { "request-source-uuid", Guid.NewGuid().ToString() }
            };
            
            var apirequest = new DataCenter.APIProxyRequestItem
            (
                DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                url,
                HttpMethod.Post,
                payload,
                customHeaders,
                WebserviceSingleTonManager.RequestModelType.ApiProxy,
                WebserviceSingleTonManager.ApiProxyType.Parallel
            );
            apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
            //apirequest.EndpointContentType = "application/json";
            
            
            var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
            // var (TrueBoxModel,errMsg) = await DataCenter.APIProxySender(url, payload, 
            //     EncryptWithJava.EncryKeyShuffler,  
            //     DataCenter.ShufflercontentType,
            //     WebserviceSingleTonManager.ApiProxyType.Parallel,
            //     null,
            //     HttpMethod.Post
            // );
            return errMsg;
            
            // string json = JsonSerializer.Serialize(payload);
            // Log4netManager.Logger.Info($"Action DoTrueBoxActionDeleteByTableColorMainAsync request:{json}");
            // string? encrypted = EncryptWithJava.Encrypt3DesECB(json);
            // var form = new Dictionary<string, string> { { "encryptedMessage", encrypted } };
            // string resp = await WebserviceSingleTonManager.Instance.SendFormPostAsync(url, form);
            //
            // //reponseResult: {"code":400,"errMsg":"400-14: Card shoe has already been deactivated or destroyed。"}
            //
            // var model = JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(resp);
            // if (model?.code == 204) return string.Empty;
            // return model?.errMsg ?? "Delete by Table with unknown error";
        }
        catch (HttpRequestException ex)
        {
            Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
            return ex.Message;
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"Destroy exception: {ex.Message}");
            return ex.Message;
        }
    }
    
    

    #region [Back up old methods]

    //
    // public static async Task<string> DoTrueBoxActionFirstAsync(string? scanid)
    // {
    //     if (scanid == null) return "Box id is null";
    //     string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
    //     try
    //     {
    //         // 1. Build payload
    //         int action = (int)TrueBoxAction.ManualFinished;
    //         var payload = new
    //         {
    //             trueId = NormalizeTrueId(scanid),
    //             action,
    //             updater = DataCenter.CurrentMember.id,
    //         };
    //         
    //         // 2. Create custom headers for APIProxy
    //         var customHeaders = new Dictionary<string, string>
    //         {
    //             { "request-source-uuid", Guid.NewGuid().ToString() }
    //         };
    //         
    //         // 3. Create APIProxy method APIProxyRequestItem
    //         var apirequest = new DataCenter.APIProxyRequestItem
    //         (
    //             DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
    //             url,
    //             HttpMethod.Post,
    //             payload,
    //             customHeaders,
    //             WebserviceSingleTonManager.RequestModelType.ApiProxy,
    //             WebserviceSingleTonManager.ApiProxyType.Parallel
    //         );
    //         apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
    //         
    //         // 4. Send APIProxy request
    //         var (externalRaw,outerErrorMsg) = await DataCenter.APIProxySenderByItem(apirequest);
    //
    //         // 5. errMsg is the inner or outer error message.
    //         if (externalRaw != null)
    //         {
    //             var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
    //             //var responseTrueBoxModel = externalRaw.Value.Deserialize<ShufflerJsonModel>();
    //             return responseTrueBoxModel.errMsg;
    //         }
    //       
    //         // 6. externalRaw is null and outerErrorMsg is not null
    //         return (!string.IsNullOrEmpty(outerErrorMsg) 
    //             ? outerErrorMsg 
    //             : $"Apiproxy Action {action} externalRaw null failed");
    //         
    //     }
    //     catch (HttpRequestException ex)
    //     {
    //         Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
    //         return ex.Message;
    //     }
    //     catch (Exception ex)
    //     {
    //         Log4netManager.Logger.Error($"ManualFinished exception: {ex.Message}");
    //         return ex.Message;
    //     }
    // }
    //
    // /// <summary>
    // /// Mark entering auto shuffling (action = AutoShuffling).
    // /// </summary>
    // /// <param name="scanid">TrueId</param>
    // /// <param name="shufflerIP">Shuffler machine IP</param>
    // /// <returns>Empty string on success; error message otherwise</returns>
    // public static async Task<string> DoTrueBoxActionSecondAsync(string? scanid, string? shufflerIP)
    // {
    //     if (scanid == null) return "Box id is null";
    //     string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
    //     try
    //     {
    //         int action = (int)TrueBoxAction.AutoShuffling;
    //         var payload = new
    //         {
    //             trueId = NormalizeTrueId(scanid),
    //             action,
    //             updater = DataCenter.CurrentMember.id,
    //             shufflerIp = shufflerIP,
    //         };
    //         var customHeaders = new Dictionary<string, string>
    //         {
    //             { "request-source-uuid", Guid.NewGuid().ToString() }
    //         };
    //         
    //         var apirequest = new DataCenter.APIProxyRequestItem
    //         (
    //             DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
    //             url,
    //             HttpMethod.Post,
    //             payload,
    //             customHeaders,
    //             WebserviceSingleTonManager.RequestModelType.ApiProxy,
    //             WebserviceSingleTonManager.ApiProxyType.Parallel
    //         );
    //         apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
    //         //apirequest.EndpointContentType = "application/json";
    //         
    //         var (externalRaw,outerErrorMsg) = await DataCenter.APIProxySenderByItem(apirequest);
    //
    //         if (externalRaw != null)
    //         {
    //             var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
    //             //var responseTrueBoxModel = externalRaw.Value.Deserialize<ShufflerJsonModel>();
    //             return responseTrueBoxModel.errMsg;
    //         }
    //       
    //         // 6. externalRaw is null and outerErrorMsg is not null
    //         return (!string.IsNullOrEmpty(outerErrorMsg) 
    //             ? outerErrorMsg 
    //             : $"Apiproxy Action {action} externalRaw null failed");
    //         
    //     }
    //     catch (HttpRequestException ex)
    //     {
    //         Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
    //         return ex.Message;
    //     }
    //     catch (Exception ex)
    //     {
    //         Log4netManager.Logger.Error($"AutoShuffling exception: {ex.Message}");
    //         return ex.Message;
    //     }
    // }
    //
    // /// <summary>
    // /// Mark auto shuffle finished (action = AutoFinished).
    // /// </summary>
    // /// <param name="scanid">TrueId</param>
    // /// <returns>Empty string on success; error message otherwise</returns>
    // public static async Task<string> DoTrueBoxActionThirdAsync(string? scanid)
    // {
    //     if (scanid == null) return "Box id is null";
    //     string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
    //     try
    //     {
    //         int action = (int)TrueBoxAction.AutoFinished;
    //         var payload = new
    //         {
    //             trueId = NormalizeTrueId(scanid),
    //             action,
    //             updater = DataCenter.CurrentMember.id,
    //         };
    //         var customHeaders = new Dictionary<string, string>
    //         {
    //             { "request-source-uuid", Guid.NewGuid().ToString() }
    //         };
    //         
    //         var apirequest = new DataCenter.APIProxyRequestItem
    //         (
    //             DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
    //             url,
    //             HttpMethod.Post,
    //             payload,
    //             customHeaders,
    //             WebserviceSingleTonManager.RequestModelType.ApiProxy,
    //             WebserviceSingleTonManager.ApiProxyType.Parallel
    //         );
    //         apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
    //         //apirequest.EndpointContentType = "application/json";
    //         
    //         
    //         var (externalRaw,outerErrorMsg) = await DataCenter.APIProxySenderByItem(apirequest);
    //         // var (externalRaw,errMsg) = await DataCenter.APIProxySender(url, payload, 
    //         //     EncryptWithJava.EncryKeyShuffler,  
    //         //     DataCenter.ShufflercontentType,
    //         //     WebserviceSingleTonManager.ApiProxyType.Parallel,
    //         //     null,
    //         //     HttpMethod.Post
    //         // );
    //         if (externalRaw != null)
    //         {
    //             var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
    //             //var responseTrueBoxModel = externalRaw.Value.Deserialize<ShufflerJsonModel>();
    //             return responseTrueBoxModel.errMsg;
    //         }
    //         
    //         // 6. externalRaw is null and outerErrorMsg is not null
    //         return (!string.IsNullOrEmpty(outerErrorMsg) 
    //             ? outerErrorMsg 
    //             : $"Apiproxy Action {action} externalRaw null failed");
    //         
    //     }
    //     catch (HttpRequestException ex)
    //     {
    //         Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
    //         return ex.Message;
    //     }
    //     catch (Exception ex)
    //     {
    //         Log4netManager.Logger.Error($"AutoFinished exception: {ex.Message}");
    //         return ex.Message;
    //     }
    // }
    //
    // /// <summary>
    // /// Mark printing finished (action = Printed).
    // /// </summary>
    // /// <param name="scanid">TrueId</param>
    // /// <returns>Empty string on success; error message otherwise</returns>
    // public static async Task<string> DoTrueBoxActionForthAsync(string? scanid)
    // {
    //     if (scanid == null) return "Box id is null";
    //     string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
    //     try
    //     {
    //         int action = (int)TrueBoxAction.Printed;
    //         var payload = new
    //         {
    //             trueId = NormalizeTrueId(scanid),
    //             action,
    //             shufflerIP = DataCenter.CurrentTrueBox.shufflerIp,
    //             updater = DataCenter.CurrentMember.id,
    //         };
    //         var customHeaders = new Dictionary<string, string>
    //         {
    //             { "request-source-uuid", Guid.NewGuid().ToString() }
    //         };
    //         
    //         var apirequest = new DataCenter.APIProxyRequestItem
    //         (
    //             DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
    //             url,
    //             HttpMethod.Post,
    //             payload,
    //             customHeaders,
    //             WebserviceSingleTonManager.RequestModelType.ApiProxy,
    //             WebserviceSingleTonManager.ApiProxyType.Parallel
    //         );
    //         apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
    //         //apirequest.EndpointContentType = "application/json";
    //         
    //         
    //         var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
    //         // var (externalRaw,errMsg) = await DataCenter.APIProxySender(url, payload, 
    //         //     EncryptWithJava.EncryKeyShuffler,  
    //         //     DataCenter.ShufflercontentType,
    //         //     WebserviceSingleTonManager.ApiProxyType.Parallel,
    //         //     null,
    //         //     HttpMethod.Post
    //         // );
    //         if (externalRaw != null)
    //         {
    //             var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
    //             //var responseTrueBoxModel = externalRaw.Value.Deserialize<ShufflerJsonModel>();
    //             return responseTrueBoxModel.errMsg;
    //         }
    //         else
    //         {
    //             return (!string.IsNullOrEmpty(errMsg)? errMsg:"Apiproxy externalRaw failed");
    //         }
    //         // string json = JsonSerializer.Serialize(payload);
    //         // Log4netManager.Logger.Info($"Printed DoTrueBoxActionForthAsync request:{json}");
    //         // string encrypted = EncryptWithJava.Encrypt3DesECB(json);
    //         // var form = new Dictionary<string, string> { { "encryptedMessage", encrypted } };
    //         // string resp = await WebserviceSingleTonManager.Instance.SendFormPostAsync(url, form);
    //         // var model = JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(resp);
    //         // if (model?.code == 201) return string.Empty;
    //         // return model?.errMsg ?? "Printed unknown error";
    //     }
    //     catch (HttpRequestException ex)
    //     {
    //         Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
    //         return ex.Message;
    //     }
    //     catch (Exception ex)
    //     {
    //         Log4netManager.Logger.Error($"Printed exception: {ex.Message}");
    //         return ex.Message;
    //     }
    // }
    //
    // /// <summary>
    // /// Mark pull-out finished (action = PulledOut).
    // /// </summary>
    // /// <param name="scanid">TrueId</param>
    // /// <returns>Empty string on success; error message otherwise</returns>
    // public static async Task<string> DoTrueBoxActionFifthAsync(string? scanid)
    // {
    //     if (scanid == null) return "Box id is null";
    //     string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
    //     try
    //     {
    //         int action = (int)TrueBoxAction.TakeOut;
    //         var payload = new
    //         {
    //             trueId = NormalizeTrueId(scanid),
    //             action,
    //             updater = DataCenter.CurrentMember.id,
    //         };
    //         
    //         var customHeaders = new Dictionary<string, string>
    //         {
    //             { "request-source-uuid", Guid.NewGuid().ToString() }
    //         };
    //         
    //         var apirequest = new DataCenter.APIProxyRequestItem
    //         (
    //             DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
    //             url,
    //             HttpMethod.Post,
    //             payload,
    //             customHeaders,
    //             WebserviceSingleTonManager.RequestModelType.ApiProxy,
    //             WebserviceSingleTonManager.ApiProxyType.Parallel
    //         );
    //         apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
    //         //apirequest.EndpointContentType = "application/json";
    //         
    //         
    //         var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
    //         
    //         // var (externalRaw,errMsg) = await DataCenter.APIProxySender(url, payload, 
    //         //     EncryptWithJava.EncryKeyShuffler,  
    //         //     DataCenter.ShufflercontentType,
    //         //     WebserviceSingleTonManager.ApiProxyType.Parallel,
    //         //     null,
    //         //     HttpMethod.Post
    //         // );
    //         if (externalRaw != null)
    //         {
    //             var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
    //             //var responseTrueBoxModel = externalRaw.Value.Deserialize<ShufflerJsonModel>();
    //             return responseTrueBoxModel.errMsg;
    //         }
    //         else
    //         {
    //             return (!string.IsNullOrEmpty(errMsg)? errMsg:"Apiproxy externalRaw failed");
    //         }
    //         // string json = JsonSerializer.Serialize(payload);
    //         // Log4netManager.Logger.Info($"PulledOut DoTrueBoxActionFifthAsync request:{json}");
    //         // string encrypted = EncryptWithJava.Encrypt3DesECB(json);
    //         // var form = new Dictionary<string, string> { { "encryptedMessage", encrypted } };
    //         // string resp = await WebserviceSingleTonManager.Instance.SendFormPostAsync(url, form);
    //         // var model = JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(resp);
    //         // if (model?.code == 201) return string.Empty;
    //         // return model?.errMsg ?? "PulledOut unknown error";
    //     }
    //     catch (HttpRequestException ex)
    //     {
    //         Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
    //         return ex.Message;
    //     }
    //     catch (Exception ex)
    //     {
    //         Log4netManager.Logger.Error($"PulledOut exception: {ex.Message}");
    //         return ex.Message;
    //     }
    // }
    //
    
    // public static async Task<string> DoTrueBoxActionSix(string? scanid)
    // {
    //     if (scanid == null) return "Box id is null";
    //     string url = DataCenter.MyShufflerSettings.ActionTrueIdApi;
    //     
    //     int action = (int)TrueBoxAction.Confirmrecept;
    //     var payload = new
    //     {
    //         trueId = NormalizeTrueId(scanid),
    //         action = (int)TrueBoxAction.Confirmrecept,
    //         updater = DataCenter.CurrentMember.id,
    //     };
    //     try
    //     {
    //         var customHeaders = new Dictionary<string, string>
    //         {
    //             { "request-source-uuid", Guid.NewGuid().ToString() }
    //         };
    //         
    //         var apirequest = new DataCenter.APIProxyRequestItem
    //         (
    //             DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
    //             url,
    //             HttpMethod.Post,
    //             payload,
    //             customHeaders,
    //             WebserviceSingleTonManager.RequestModelType.ApiProxy,
    //             WebserviceSingleTonManager.ApiProxyType.Parallel
    //         );
    //         apirequest.EndpointencryKey = EncryptWithJava.EncryKeyShuffler;
    //         //apirequest.EndpointContentType = "application/json";
    //         
    //         
    //         var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
    //         // var (externalRaw,errMsg) = await DataCenter.APIProxySender(url, payload, 
    //         //     EncryptWithJava.EncryKeyShuffler,  
    //         //     DataCenter.ShufflercontentType,
    //         //     WebserviceSingleTonManager.ApiProxyType.Parallel,
    //         //     null,
    //         //     HttpMethod.Post
    //         // );
    //         if (externalRaw != null)
    //         {
    //             var responseTrueBoxModel =JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(externalRaw);
    //             //var responseTrueBoxModel = externalRaw.Value.Deserialize<ShufflerJsonModel>();
    //             return responseTrueBoxModel.errMsg;
    //         }
    //         else
    //         {
    //             return (!string.IsNullOrEmpty(errMsg)? errMsg:"Apiproxy externalRaw failed");
    //         }
    //         // string json = JsonSerializer.Serialize(payload);
    //         // Log4netManager.Logger.Info($"Action 6 DoTrueBoxActionSix request:{json}");
    //         // string? encrypted = EncryptWithJava.Encrypt3DesECB(json);
    //         // var form = new Dictionary<string, string> { { "encryptedMessage", encrypted } };
    //         // string resp = await WebserviceSingleTonManager.Instance.SendFormPostAsync(url, form);
    //         // var model = JsonSerializer.Deserialize<TrueBoxWebServiceResponseModel>(resp);
    //         // if (model?.code == 204) return string.Empty;
    //         // return model?.errMsg ?? "Destroy unknown error";
    //     }
    //     catch (HttpRequestException ex)
    //     {
    //         Log4netManager.Logger.Error($"HTTP Error: {ex.Message}");
    //         return ex.Message;
    //     }
    //     catch (Exception ex)
    //     {
    //         Log4netManager.Logger.Error($"Destroy exception: {ex.Message}");
    //         return ex.Message;
    //     }
    //}
    
    #endregion

}