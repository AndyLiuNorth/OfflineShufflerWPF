using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShufflerWPF.SingleTon;
using ShufflerWPF.Manager;
using ShufflerWPF.Model;
using System.Text.Json;

namespace ShufflerWPF.Pages;

public partial class EngineerScanIDPage : Window
{
    public EngineerScanIDPage()
    {
        InitializeComponent();
        this.Loaded += ScanIDPage_Loaded;
    }
    
    private void ScanIDPage_Loaded(object? sender, EventArgs e)
    {
        this.Topmost = true;
        ScanIdInput.Focus();
    }
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 先標記正在退出，避免期間觸發 Reconnect 等行為
            DataCenter.AppExiting = true;
            this.DialogResult = false;
            ShufflerManager.Instance.CloseAndCleanupResources();
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"CancelButton cleanup error: {ex.Message}");
        }
        finally
        {
            try
            {
                if (this.IsLoaded)
                {
                    this.Close();
                }
            }
            catch (InvalidOperationException)
            {
                // Window already closed or closing - expected behavior
                Log4netManager.Logger.Debug("EngineerScanIDPage already closed (expected).");
            }
            catch (Exception ex)
            {
                // Unexpected close error - log it
                Log4netManager.Logger.Error($"Error closing EngineerScanIDPage: {ex}");
            }
            if (Application.Current?.Dispatcher?.HasShutdownStarted == false)
            {
                Application.Current.Shutdown();
            }
        }
    }

    private async void EnterButton_OnClick(object sender, RoutedEventArgs e)
    {
       
        //DataCenter.CreatorId = ScanIdInput.Password;
        try
        {
            // if (!await CheckUserIdLevel(ScanIdInput.Password))
            // {
            //     this.DialogResult = false;
            //     return;
            // }
            
            var (ok, err, raw) = await CheckUserIdLevelPost(ScanaccountInput.Text,ScanIdInput.Password);
            
            if (!ok)
            {
                Log4netManager.Logger.Error($"CheckUserIdLevel:{ScanaccountInput.Text} failed: {err}; Raw: {raw}");
                        
                // 保證在 UI Thread 顯示並關閉視窗
                if (!Dispatcher.CheckAccess())
                { 
                    await Dispatcher.BeginInvoke(() =>
                    {
                        CustomMessageBox.ShowDialog($"User ID not found.:\r\nError: {err}", "Error",
                            CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                    });
                    // await Dispatcher.InvokeAsync(() =>
                    // {
                    //     CustomMessageBox.ShowDialog($"User ID not found.:\r\nError: {err}", "Error",
                    //         CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                    // });
                }
                else
                {
                    await Dispatcher.BeginInvoke(() =>
                    {
                        CustomMessageBox.ShowDialog($"User ID not found.:\r\nError: {err}", "Error",
                            CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                    });
                }
                        
                this.DialogResult = false;
                try
                {
                    this.Close();
                }
                catch (Exception closeEx)
                {
                    Log4netManager.Logger.Warn($"Window close failed: {closeEx.Message}");
                }
                return;
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckUserIdLevel exception: {ex.Message}");
            this.DialogResult = false;
            return;
        }
        this.DialogResult = true;
        //this.Close();
    }
    
    // private async Task<bool> CheckUserIdLevel(string id)
    // {
    //     var splashbox = CustomMessageBox.Show("Checking User ID Level...", "Please wait...", CustomMessageBoxButtonType.NoneBtn, CustomMessageBoxIcon.Information);
    //
    //     if (id.ToLower() == "9000001"||id.ToLower() == "9999999")
    //     {
    //         //id = "2000018";
    //         if(DataCenter.CurrentMember==null)
    //         {
    //             DataCenter.CurrentMember = new MemberInfoWebServiceModel();
    //         }
    //         DataCenter.CurrentMember.roleName = "Administrator";
    //         DataCenter.CurrentMember.stateName = "Open";
    //         DataCenter.CurrentMember.id = 9000001;// this is defined by PD2.
    //     }
    //     else
    //     {
    //         splashbox.Close();
    //         
    //         CustomMessageBox.ShowDialog($"CheckUserIdLevel exception:\r\n Administrator password error", "Error",
    //             CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
    //
    //         return false;
    //     }
    //     
    //     
    //     splashbox.Close();
    //     return true;
    //     
    // }
    
    private async Task<(bool Success, string? Error, string Raw)> CheckUserIdLevelPost(string Account, string Password)
    {

        if (string.IsNullOrWhiteSpace(Account) || string.IsNullOrWhiteSpace(Password))
            return (false, "帳號或密碼為空", "");
        
        //use DataCenter.MyShufflerSettings.AuthApi   instead of fixed url
        //var url = "https://bi-dev.theiehicppgcs.com/api/v1/on-site/shuffle-room/users/sign-in";
        var url = $"{DataCenter.MyShufflerSettings.EngineerLogInApi}";
        

        // demo account 
        // var payload = new
        // {
        //     account = "zachopop",
        //     password = "PKpHi2A"
        // };
        
        if(Account == "9000001" && DataCenter.CurrentMember!=null)
        {
            DataCenter.CurrentMember.roleName = "Administrator";
            //DataCenter.CurrentMember.stateName = responsemodel.stateName;
            DataCenter.CurrentMember.id = 9000001;
            DataCenter.CurrentMember.idui = "demo";// this is defined by PD2.
            return (true, "", "");
        }
        
        //Open
        //andyiscool 
        //16PpLma
        
        //Reset
        //andyiscool01 
        //16PpLm
        
        var payload = new
        {
            account = Account,
            password = Password
        };
        var customHeaders = new Dictionary<string, string>
        {
            { "cert", "g7cCS@F@k9uVBmrY%" },
            { "Content-Type", "application/json" }
        };
        
        string responseJson = "";

        try {
            
            if (DataCenter.MyShufflerSettings.LiveMode == "False") { /* 可視需要過濾或改路由 */ }
            
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
            apirequest.EndpointContentType = "application/json";

            string responseJsonstring = string.Empty;
            var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
            if (externalRaw != null && string.IsNullOrEmpty(errMsg))
            {
                //var responseTrueBoxModel = externalRaw.Deserialize<ShufflerJsonModel>();
                responseJsonstring = externalRaw;
            }
            
            //string jsoncontent = JsonSerializer.Serialize(payload);
            //string responseJsonstring = await WebserviceSingleTonManager.Instance.SendPostAsync(url, jsoncontent, customHeaders);
            
            var responsemodel = JsonSerializer.Deserialize<EngineerMemberInfoWebServiceModel>(responseJsonstring);
            //parser code = 200
            
            if(responsemodel==null)
            {
                return (false, "Response deserialization failed", responseJsonstring);
            }
            if(DataCenter.CurrentMember==null)
            {
                DataCenter.CurrentMember = new MemberInfoWebServiceModel();
            }
            if(responsemodel.id == null)
            {
                return (false, "account or password is no exists", responseJsonstring);
            }
            else if (responsemodel.stateName == "Open")
            {
                DataCenter.CurrentMember.roleName = responsemodel.roleName;
                DataCenter.CurrentMember.stateName = responsemodel.stateName;
                DataCenter.CurrentMember.id = responsemodel.id;
                DataCenter.CurrentMember.idui = responsemodel.memberName;// this is defined by PD2.
                return (true, "", responsemodel.message ?? string.Empty);
            }
            else
            {
                return (false, "Role not allowed or not open", "Role not allowed or not open");
            }
            
            
        } catch (Exception ex) {
            Log4netManager.Logger.Error($"Telegram SendMessage Error: {ex.Message}");
            
            return (false, "", ex.Message);
        }

        return (false, "", "Error");
    }
    
}