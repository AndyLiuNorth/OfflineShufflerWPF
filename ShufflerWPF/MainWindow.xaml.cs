using System.Buffers.Binary;
using System.Windows;
using ShufflerWPF.Manager;
using ShufflerWPF.SingleTon;
using ShufflerWPF.Pages;
using ShufflerWPF.Model;
using System.Text.Json;
using System.ComponentModel; 
using System.Net.Http;
using System.Windows.Input;

namespace ShufflerWPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{

    private CreateBoxIDPage? _createIdPage;
    private DeletePage? _deletePage;
    private ScanBoxIDPage? _scanTrueIdPage;
    private ShuffleManualPage? _shuffleManualPage;
    private RegisterOutPage? _registerOutPage;
    private readonly Lazy<ShuffleAutoPage> _shuffleAutoPage;
    private ShuffleFinishPage? _shuffleFinishPage;
    private readonly Lazy<ShufflerSettingPage> _shufflerSettingPage;
    private AdministratorSettingPage? _administratorSettingPage;
    private ShufflerAutoPageViewModel _vmauto;
    private ClarkShufflerManager? _clark;
    private bool _isCreateTrueInProgress; // 防止重複開啟 Create True 對話流程
    private bool _UserScanJobGoing = false;

    public MainWindow()
    {
        Log4netManager.Logger.Info("MainWindow: Enter initial");
        //Console.WriteLine("MainWindow: Enter initial");
        //this.Closing += Window_Closing;
        this.Loaded += Window_Loaded;
        this.IsVisibleChanged += Window_IsVisibleChanged;
        try
        {
            InitializeComponent();
            Application.Current.MainWindow = this;
            this.Topmost = true;
            Log4netManager.Logger.Info("MainWindow: InitializeComponent() finished!!");
            //Console.WriteLine("MainWindow: InitializeComponent() finish!!");
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Info($"MainWindow: Error: {ex.Message}\n{ex.StackTrace}");
            //Console.WriteLine($"MainWindow: Error: {ex.Message}\n{ex.StackTrace}");
            MessageBox.Show($"MainWindow Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }

        // 防護: 若 CurrentMember 尚未建立，建立一個 placeholder，避免後續 NullReference
        if (DataCenter.CurrentMember == null)
        {
            DataCenter.CurrentMember = new MemberInfoWebServiceModel();
            Log4netManager.Logger.Warn("DataCenter.CurrentMember was null. Created placeholder instance.");
        }

        if (DataCenter.CurrentMember.id != null)
        {
            this.DataContext = DataCenter.CurrentMember;
        }

        // -these ShuffleAutoPage & ShufflerSettingPage two pages share this vmauto.
        // -this is a reference method.

        _vmauto = new ShufflerAutoPageViewModel();
        
        var topStack = new TopStackUserControl(); 
        _shuffleAutoPage = new Lazy<ShuffleAutoPage>(() => new ShuffleAutoPage(_vmauto,topStack));
        _shufflerSettingPage = new Lazy<ShufflerSettingPage>(() => new ShufflerSettingPage(_vmauto));
        //this.Hide();
#if RELEASE
        PrintTestButton.Visibility = Visibility.Collapsed;
        TelegrameTestButton.Visibility = Visibility.Collapsed;
        webAPItestButton.Visibility = Visibility.Collapsed;
#endif

        
        // Define Shuffler Manager Instance
        ShufflerProvider.Instance.UseLegacy();
        
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        bool isVisible = (bool)e.NewValue;
        if (isVisible)
        {
            try
            {
                if(_UserScanJobGoing)
                {
                    Log4netManager.Logger.Info("MainWindow: User scan job is already going. Skip re-entrance.");
                    return;
                }
                _UserScanJobGoing = true;
                
                while (true)
                {

                    if (DataCenter.AppExiting) break;

                    // Mode is first enter mode
                    var scanIdPage = new UserScanIdPage(1);

                    // Cancel button
                    bool? dialogResult = scanIdPage.ShowDialog();

                    //Log4netManager.Logger.Info($"MainWindow: LiveMode:{DataCenter.MyShufflerSettings.LiveMode}");

                    // // Check LiveMode After administrator Page Close;
                    // if (DataCenter.MyShufflerSettings.LiveMode == "False")
                    // {
                    //     Log4netManager.Logger.Info("MainWindow: Enter offline mode!");
                    //     if (DataCenter.CurrentMember == null)
                    //     {
                    //         DataCenter.CurrentMember = new MemberInfoWebServiceModel();
                    //     }
                    //     DataCenter.CurrentMember.id = 9000001;
                    // }
                    
                    

                    if (dialogResult == false)
                    {
                        // Scan ID Fail
                        //TelegramBotTypeManager.Pd2GroupGb.SendMessageGB("Scan Fail Telegram Demo.\r\n 中文測試\r\n English Test");
                        continue;
                    }
                    else if (!DataCenter.AdministratorMode)
                    {
                        // Scan result is Operator ID
                        // Skip Administrator mode
                        break;
                    }
                    else if (DataCenter.CurrentMember?.id == 9000001)
                    {
                        // Scan result is Administrator ID
                        break;
                    }

                    if (dialogResult == true && DataCenter.AdministratorMode)
                    {
                        if (_shuffleAutoPage != null)
                        {
                            if (_shuffleAutoPage.Value.IsVisible)
                            {
                                _shuffleAutoPage.Value.Hide(); //_shuffleAutoPage is show() method page. So scanIDPage Can't hide this page.
                            }
                        }
                    }

                    EngineerScanIDPage manualPage = new EngineerScanIDPage();
                    bool? manualPageResult = manualPage.ShowDialog();
                    if (manualPageResult == false)
                    {
                        // this.Close();
                        // MessageBox.Show("Shuffler APP Close.");
                        if (DataCenter.AppExiting) break;
                        
                    }
                    else
                    {
                        break;
                    }
                    
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
            finally
            {
                _UserScanJobGoing = false;
                RefreshPermissions();
            }
        }
        else
        {

        }
    }

    private void Window_Loaded(object? sender, EventArgs e)
    {
        this.Topmost = true;
        RefreshPermissions();
    }

    private void RefreshPermissions()
    {
        var m = DataCenter.CurrentMember; // 可能為 placeholder

        if (DataCenter.CurrentMember?.id != null)
        {
            this.DataContext = DataCenter.CurrentMember;
        }

        if (m == null)
        {
            // 尚未登入/初始化，全部鎖定
            AdministratorBtn.IsEnabled = false;
            CreateButton.IsEnabled = false;
            DeleteTrueShoeBtn.IsEnabled = false;
            ShufflerSettingBtn.IsEnabled = false;
            return;
        }

        var role = m.roleName ?? string.Empty;
        //var id = m.id;

        // 重設 ToolTip 與預設啟用狀態
        AdministratorBtn.ToolTip = null;
        CreateButton.ToolTip = null;
        DeleteTrueShoeBtn.ToolTip = null;
        ShufflerSettingBtn.ToolTip = null;

        // 預設全部啟用 (之後再依角色關閉)
        AdministratorBtn.IsEnabled = true;
        CreateButton.IsEnabled = true;
        CreateShoeButton.IsEnabled = true;
        DeleteTrueShoeBtn.IsEnabled = true;
        DestroyTrueShoeBtn.IsEnabled = true;
        RegisterShoeBtn.IsEnabled = true;
        ShufflerSettingBtn.IsEnabled = true;

        // Reset Tooltip
        CreateShoeButton.ToolTip = "Create a shoe for True";
        CreateButton.ToolTip = "Create a new True Box";
        DeleteTrueShoeBtn.ToolTip = "Delete True Box";
        AdministratorBtn.ToolTip = "Administrator Setting";
        ShufflerSettingBtn.ToolTip = "Shuffler Stop and Unlock";
        RegisterShoeBtn.ToolTip = "Pull out the shoe";
        DestroyTrueShoeBtn.ToolTip = "Destroy True Box";

        if (role.ToLower() == "administrator" || role.ToLower() == "tech")
        {
            return;
        }
        else if (role.ToLower() == "supervisor")
        {
            AdministratorBtn.IsEnabled = false;
            AdministratorBtn.ToolTip = "Permission Denied";
            return;
        }
        else if (role.ToLower() == "shuffler")
        {
            CreateButton.IsEnabled = false;
            CreateButton.ToolTip = "Permission Denied";
            DeleteTrueShoeBtn.IsEnabled = false;
            DeleteTrueShoeBtn.ToolTip = "Permission Denied";
            AdministratorBtn.IsEnabled = false;
            AdministratorBtn.ToolTip = "Permission Denied";
            ShufflerSettingBtn.IsEnabled = false;
            ShufflerSettingBtn.ToolTip = "Permission Denied";
            return;
        }
        else if (role == "OP")
        {
            CreateShoeButton.IsEnabled = false;
            CreateShoeButton.ToolTip = "Permission Denied";
            CreateButton.IsEnabled = false;
            CreateButton.ToolTip = "Permission Denied";
            DeleteTrueShoeBtn.IsEnabled = false;
            DeleteTrueShoeBtn.ToolTip = "Permission Denied";
            AdministratorBtn.IsEnabled = false;
            AdministratorBtn.ToolTip = "Permission Denied";
            ShufflerSettingBtn.IsEnabled = false;
            ShufflerSettingBtn.ToolTip = "Permission Denied";
            RegisterShoeBtn.IsEnabled = false;
            RegisterShoeBtn.ToolTip = "Permission Denied";
            DestroyTrueShoeBtn.IsEnabled = false;
            DestroyTrueShoeBtn.ToolTip = "Permission Denied";
            return;
        }

        // 未知角色: 只鎖管理員鍵
        AdministratorBtn.IsEnabled = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        try
        {
            _shuffleAutoPage.Value.Close();
            _shufflerSettingPage.Value.Close();
            _administratorSettingPage?.Close();
        }
        catch(Exception ex)
        {
            Log4netManager.Logger.Error($"OnClosing cleanup error {ex.Message}");
        }

        try
        {
            DataCenter.AppExiting = true;
            ShufflerManager.Instance.CloseAndCleanupResources();
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"OnClosing cleanup error {ex.Message}");
        }

        base.OnClosing(e);
    }

    private void MainWindow_Closed(object sender, EventArgs e)
    {
        Console.WriteLine("MainWindow: CLOSED");
    }

    private async void CreateTrueButton_OnClick(object sender, RoutedEventArgs e)
    {
        Log4netManager.Logger.Info($"CreateTrueButton_OnClick Click");
        // avoid re-entrance this page
        if (_isCreateTrueInProgress)
        {
            Log4netManager.Logger.Warn("CreateTrueButton_OnClick ignored: previous operation still in progress.");
            return;
        }

        _isCreateTrueInProgress = true;
        if (CreateButton != null) CreateButton.IsEnabled = false;
        try
        {
            // var customHeaders = new Dictionary<string, string>
            // {
            //     { "cert", "g7cCS@F@k9uVBmrY%" }
            // };
            //
            // List<string>? gametablelist = null;
            // string? responseJsonstring = null;
            //
            // // 第一段：呼叫 Web API 取得桌台列表
            // try
            // {
            //
            //     responseJsonstring = await WebserviceSingleTonManager.Instance.SendGetAsync(
            //         $"{DataCenter.MyShufflerSettings.TableListApi}?siteDomainName={DataCenter.MyShufflerSettings.Domain}",
            //         customHeaders);
            //     Log4netManager.Logger.Info($"SendGetAsync success:{responseJsonstring}");
            //     var memberInfo = JsonSerializer.Deserialize<MemberInfoWebServiceModel>(responseJsonstring);
            //     gametablelist = memberInfo?.data?.tables;
            //
            //     if (gametablelist == null || gametablelist.Count == 0)
            //     {
            //         Log4netManager.Logger.Error(
            //             "Table not found due to network connection or API issue.\n(Error: 443)");
            //         CustomMessageBox.ShowDialog("Table not found due to network connection or API issue.", "Error",
            //             CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            //         return; // 中斷流程
            //     }
            // }
            // catch (HttpRequestException httpEx)
            // {
            //     Log4netManager.Logger.Error($"Fetch game tables HttpRequestException: {httpEx.Message}", httpEx);
            //     CustomMessageBox.ShowDialog($"Table not found due to network connection issue.\r\n{httpEx.Message}",
            //         "Network Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            //     return; // 中斷流程
            // }
            // catch (TaskCanceledException tcEx)
            // {
            //     Log4netManager.Logger.Error($"Fetch game tables timeout: {tcEx.Message}", tcEx);
            //     CustomMessageBox.ShowDialog($"Get Game Tables timeout. Please retry.\r\n{tcEx.Message}", "Timeout",
            //         CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            //     return; // 中斷流程
            // }
            // catch (Exception exFetch)
            // {
            //     Log4netManager.Logger.Fatal("Fetch game tables unexpected error", exFetch);
            //     CustomMessageBox.ShowDialog($"Get Game Tables unexpected error.\r\n{exFetch.Message}", "Fetch Error",
            //         CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            //     return; // 中斷流程
            // }

            // 第二段：建立並顯示 CreateTrueIDPage
            try
            {
                
                var gametable = await _getgametable();

                if (gametable == null)
                {
                    return;
                }
                
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Hide();
                }

                _createIdPage = new CreateBoxIDPage(gametable);
                bool? creatresult = _createIdPage.ShowDialog();
                if (creatresult != true)
                {
                    return; // 使用者取消或失敗
                }
                
            }
            catch (Exception pageEx)
            {
                Log4netManager.Logger.Fatal("Open CreateTrueIDPage error", pageEx);
                CustomMessageBox.ShowDialog($"Open Create Box ID page failed.\r\n{pageEx.Message}", "Page Error",
                    CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            }
            finally
            {
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Show();
                }
            }
        }
        finally
        {
            _isCreateTrueInProgress = false;
            if (CreateButton != null) CreateButton.IsEnabled = true; // 解鎖 UI
        }
    }

    /// <summary>
    /// Test Print Button
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void PrintTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        var splashbox = CustomMessageBox.Show("Print Shoe Information...", "Please wait...",
            CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Information);

        Ql810WManager q810WManager = new Ql810WManager();
        // q810WManager.PrintInfo.count = "3";
        // q810WManager.PrintInfo.maxcount = "10";
        // q810WManager.PrintInfo.createdate = DateTime.Now.ToString("yyyy-MM-dd");
        // q810WManager.PrintInfo.shufflerIp = "10.161.93.92";
        // q810WManager.PrintInfo.tableIDName = "KPD999BE";
        // q810WManager.PrintInfo.Printmode = Ql810WManager.printmode.ShoeInfo;
        await q810WManager.Ql810DoPrint("");

        //throw new NotImplementedException();
        splashbox.Close();
    }

    private async void CreateShoeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Hide();
        }

        Log4netManager.Logger.Info($"CreateShoeButton Click");

        //Refresh CurrentTrueBox
        _scanTrueIdPage = new ScanBoxIDPage();
        bool? scanresult = _scanTrueIdPage.ShowDialog();
        //bool checkTrueStatusbreak = false;

        if (scanresult == true && _scanTrueIdPage.ScannedData != null && string.IsNullOrEmpty(_scanTrueIdPage.errormessage!))
        { 
            DataCenter.CurrentTrueBox = _scanTrueIdPage.ScannedData;
        }
        else
        {
            
            // url fail timeout but not cancel
            if (scanresult==false && _scanTrueIdPage.ScannedData == null && _scanTrueIdPage.errormessage != "cancel")
            {
                Log4netManager.Logger.Warn($"CurrentTrueBox.state is null]");
                CustomMessageBox.ShowDialog(
                    $"Server connect fail:{_scanTrueIdPage.errormessage}",
                    "Action Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            }
            else if (!string.IsNullOrEmpty(_scanTrueIdPage.errormessage!) && _scanTrueIdPage.errormessage != "cancel")// url pass but endpoint result fail
            {
                Log4netManager.Logger.Warn($"CurrentTrueBox.state is null]");
                CustomMessageBox.ShowDialog(
                    $"Server connect fail:{_scanTrueIdPage.errormessage}",
                    "Action Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            }
            
            // success or cancel
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }

        Log4netManager.Logger.Info(
            $"True[{DataCenter.CurrentTrueBox.PrintName}] action is [{DataCenter.CurrentTrueBox.action}]");

        //  currentstate 狀態:
        //  0=可使用
        //  1=禁止使用 (MAX Count觸頂狀態，或現在時間大於Deadline)
        //  99=特殊禁用狀態，透過註銷方式處理
        //  -1=此牌靴已被銷毀
        double? currentstate = DataCenter.CurrentTrueBox.trueState;
        if (currentstate == null)
        {
            Log4netManager.Logger.Warn($"CurrentTrueBox.state is null]");
            CustomMessageBox.ShowDialog(
                $"🔹Ture ID:{_scanTrueIdPage.scannedData}\r\n\r\nCurrentTrueBox.state is null \r\n\r\n{DataCenter.CurrentTrueBox.TagMessage}",
                "Action Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }
        else if (currentstate == 4)
        {
            Log4netManager.Logger.Warn(
                $"CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}].state is [{currentstate}].");

            CustomMessageBox.ShowDialog(
                $"CurrentTrueBox.state is [{currentstate}]\r\nThis True[{DataCenter.CurrentTrueBox.PrintName}] has already been shuffled.",
                "Action Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }
        else if (currentstate == 99)
        {
            Log4netManager.Logger.Warn(
                $"CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}].state is [{currentstate}].");

            CustomMessageBox.ShowDialog(
                $"CurrentTrueBox.state is [{currentstate}]\r\nThis True[{DataCenter.CurrentTrueBox.PrintName}] has been deleted.",
                "Action Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }
        else if (currentstate == -1)
        {
            Log4netManager.Logger.Warn(
                $"CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}].state is [{currentstate}].");

            CustomMessageBox.ShowDialog(
                $"CurrentTrueBox.state is [{currentstate}]\r\nThis True[{DataCenter.CurrentTrueBox.PrintName}] has been destroyed.",
                "Action Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }

        //  currentaction狀態
        //  0:創建完成 1:手動洗牌完成 2:機械洗牌進行中 3:機械洗牌完成 4:列印資訊完成 5:Pull out完成
        double? currentaction = DataCenter.CurrentTrueBox.action;
        try
        {
            await _doRunShuffleProcess(currentaction);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }

        // action 1 Or action 2
        // action 1 is after manual finish. AutoShufflerPage is .show() method
        // action 2 is shuffling. show messagebox and still ths show AutoShufflerPage .show()
        // action 3 is shuffle finish. Show FinishPage dialog().
        if (currentaction != 1 && currentaction != 2)
        {
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }
        }

    }

    private void ExitButtonButton_OnClick(object sender, RoutedEventArgs e)
    {
        
        try
        {
            DataCenter.AppExiting = true;
            _shuffleAutoPage.Value.Close();
            _shufflerSettingPage.Value.Close();
            _administratorSettingPage?.Close();
        }
        catch(Exception ex)
        {
            Log4netManager.Logger.Fatal($"ExitButtonButton_OnClick {ex.Message}");
        }

        this.Close(); // ShutdownMode=OnMainWindowClose 自動結束
    }

    private async Task _doRunShuffleProcess(double? nowaction)
    {
        //  DataCenter.CurrentTrueBox refresh after scan true ID
        //  scantrueID Page run GetTrueIdUpdateCurrentBox() to refresh DataCenter.CurrentTrueBox
        TrueBoxViewModel vm = new TrueBoxViewModel();
        vm.OneBoxModel = DataCenter.CurrentTrueBox;

        if (nowaction == 0 || nowaction == 7) //尚未人工洗牌的階段，完成後狀態 1
        {
            vm.OneBoxModel.Statusdescreption = "No shoe state";

            bool? manualResult;
            _shuffleManualPage = new ShuffleManualPage(vm);
            manualResult = _shuffleManualPage.ShowDialog();
            Log4netManager.Logger.Info(
                $"_doRunShuffleProcess CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}]. Manual Shuffler Result: {manualResult}");

            //press cancel button
            if (manualResult == false)
            {
                return;
            }
        }
        else if (nowaction == 1 || nowaction == 2) //尚未機械洗牌的階段，進入之前為狀態1 進入洗牌中狀態2 洗牌完成狀態3
        {
            // Notice! this action use .show() of _shuffleAutoPage
            // the other use .ShowDialog()
            // _shuffleAutoPage can be set as  singleton or Lazy

            if (nowaction == 2) // 正在洗牌中，給提醒視窗，之後��然顯示出AutoShuffler頁面
            {
                Log4netManager.Logger.Warn(
                    $"action 2: Enter AutoShuffler Page. This True[{vm.OneBoxModel.PrintName}] is currently shuffling.");
                CustomMessageBox.ShowDialog(
                    $"Please wait. This shoe in Box ID: [{vm.OneBoxModel.PrintName}] is being shuffled now. \r\n" +
                    "But if the machine isn't working, delete and destroy the Box ID and recreate one."
                    , "Shuffling",
                    CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Warning);
            }
            else
            {
                Log4netManager.Logger.Info($"Enter action 2+3: True[{vm.OneBoxModel.PrintName}] Enter action 1");
            }

            // Connect shuffler server first.
            CustomMessageBox connectbox = CustomMessageBox.Show(
                $"Machine Server[{DataCenter.MyShufflerSettings.ShufflerMachineServerIp}] Connecting...", "Shuffler",
                CustomMessageBoxButtonType.NoneBtn, CustomMessageBoxIcon.Information);
            bool? shufflerConnect = await ShufflerManager.Instance.ConnectShuffler();
            connectbox.Close();
            if (shufflerConnect == false)
            {
                CustomMessageBox.ShowDialog(
                    $"Machine Server[{DataCenter.MyShufflerSettings.ShufflerMachineServerIp}] Connection Fail!", "Shuffler",
                    CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Show();
                }

                return;
            }

            // now true id give to _shuffleAutoPage and run task
            _vmauto.OneBoxModel = DataCenter.CurrentTrueBox;
            
            // Lazy init use Value
            _shuffleAutoPage.Value.updateTruebox(_vmauto);
            _shuffleAutoPage.Value.Show();


        }
        else if (nowaction == 3) //洗牌全部完成，但尚未烈印Shoe Information，印完完成後狀態4
        {

            string? nowTrueId = vm.OneBoxModel.trueId;

            //bool? printResult;
            _shuffleFinishPage = new ShuffleFinishPage(vm);
            var printResult = _shuffleFinishPage.ShowDialog();

            if (printResult != true && _shuffleFinishPage.errormessage != string.Empty) // error
            {
                Log4netManager.Logger.Error(
                    $"_doRunShuffleProcess CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}]. Print Result: {_shuffleFinishPage.errormessage}");
                CustomMessageBox.ShowDialog($"Print Error", "Error", CustomMessageBoxButtonType.Ok,
                    CustomMessageBoxIcon.Error);
                return;
            }
            
            // cancel
            if (printResult != true && _shuffleFinishPage.errormessage == string.Empty) 
            {
                return;
            }
            
            // pass
            // double check print
            CustomMessageBoxResult yesNoresult = CustomMessageBox.ShowDialog($"Is the Shoe information printed correct?",
                "Action", CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Question);
            if (yesNoresult == CustomMessageBoxResult.No || yesNoresult == CustomMessageBoxResult.Cancel)
            {
                return;
            }
            

            // print ok. go action 4
            //string action4Error = await DoTrueBoxActionManager.DoTrueBoxActionForthAsync(nowTrueId);
            string action4Error  = await DoTrueBoxActionManager.DoTrueBoxActionFirstToSixAsync(nowTrueId, DoTrueBoxActionManager.TrueBoxAction.Printed);
            if (!string.IsNullOrEmpty(action4Error))
            {

                Log4netManager.Logger.Error($"Print shoe. action 4 Fail \r\n{action4Error}\r\n Box ID:{nowTrueId}");
                CustomMessageBox.ShowDialog(
                    $"Print this shoe action Failed \r\n{action4Error}\r\n 🔹Box ID:{nowTrueId}", "Action 4 Fail",
                    CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                return;
                //throw new Exception($"Action 3 failed: {action5Error}");
            }
            else
            {
                CustomMessageBox.ShowDialog($"Print this shoe Action Finish.", "Action 4 OK",
                    CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Show();
                }
            }

        }
        else if (nowaction == 4) //洗牌完成了，也印出所有標籤了
        {
            Log4netManager.Logger.Warn(
                $"_doRunShuffleProcess CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}] This shoe has been shuffled completely..");
            CustomMessageBox.ShowDialog(
                $"🔹Box ID:[{DataCenter.CurrentTrueBox.PrintName}] \r\n This shoe has been shuffled completely.",
                "State 4", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Information);
            return;
        }
        else if (nowaction == 5)
        {
            CustomMessageBox.ShowDialog(
                $"_doRunShuffleProcess CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}] has Taken out already.",
                "Shuffling",
                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Information);
            return;
        }
        else if (nowaction == 6)
        {
            CustomMessageBox.ShowDialog(
                $"_doRunShuffleProcess CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}] has confirmed receipt already.",
                "Shuffling",
                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Information);
            return;
        }

        Console.WriteLine("Next round");

    }

    private async void DeleteTrueShoeBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Log4netManager.Logger.Info($"DeleteTrueShoeBtn Click");
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Hide();
        }


        // if (!_ScanTrueIDMethod())
        // {
        //     if (Application.Current.MainWindow != null)
        //     {
        //         Application.Current.MainWindow.Show();
        //     }
        //
        //     return;
        // }
        try
        {
            var gametable = await _getgametable();

            if (gametable == null)
            {
                return;
            }

            if (await _ScanTrueIDMethod(gametable,DataCenter.ScanType.delete))
            {
                
            }
            
            // _deletePage = new DeletePage(gametable);
            // var deleteresult = _deletePage.ShowDialog();
            //
            // // delete by result
            // if (_deletePage.errormessage=="cancel")
            // {
            //     return ;
            // }
            // if (deleteresult == true && _deletePage.ScannedData != null)
            // {
            //     if (_deletePage.ScannedData.TagMessage != string.Empty)
            //     {
            //         // Error from server
            //         CustomMessageBox.ShowDialog($"Box ID:{_deletePage.ScannedData.TagMessage} not found.", "Scan",
            //             CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
            //         
            //         return ;
            //     }
            //
            //     // PASS
            //     DataCenter.CurrentTrueBox = _deletePage.ScannedData;
            //     
            //     int? currentaction = DataCenter.CurrentTrueBox.action;
            //     string? nowPrintName = DataCenter.CurrentTrueBox.PrintName;
            //     string? nowscanid = DataCenter.CurrentTrueBox.trueId;
            //     
            //     CustomMessageBoxResult boxresult = CustomMessageBox.ShowDialog($"Delete🔹Box Id:{nowPrintName}", "Delete",
            //         CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Question);
            //
            //     if (boxresult == CustomMessageBoxResult.Yes)
            //     {
            //
            //         //run delete webservice
            //         try
            //         {
            //
            //             string actiondeleteError = await DoTrueBoxActionManager.DoTrueBoxActionDeleteAsync(nowscanid);
            //
            //             if (!string.IsNullOrEmpty(actiondeleteError))
            //             {
            //
            //                 Log4netManager.Logger.Info(
            //                     $"Cannot delete this shoe. \r\n{actiondeleteError}\r\n Box ID:{nowPrintName}");
            //                 CustomMessageBox.ShowDialog($"Cannot delete this shoe. \r\n{actiondeleteError}", "Delete shoe",
            //                     CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            //                 //throw new Exception($"Action 3 failed: {action5Error}");
            //             }
            //             else
            //             {
            //                 CustomMessageBox.ShowDialog($"🔹Box ID:{nowPrintName} \r\n\r\n" +
            //                                             $"It is deleted successfully.", "delete",
            //                     CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
            //                 Log4netManager.Logger.Info($"It is deleted successfully.\r\n Box ID:{nowPrintName}");
            //             }
            //         }
            //         catch (Exception ex)
            //         {
            //             Log4netManager.Logger.Info($"DeleteTrueShoeBtn exception:{ex.Message}");
            //             CustomMessageBox.ShowDialog($"🔹Box ID:{nowPrintName} \r\nDelete this shoe Fail \r\n{ex.Message}",
            //                 "Delete",
            //                 CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            //         }
            //     }
            //     
            // } // delete by table
            // else if (deleteresult == true && _deletePage.ScannedData == null)
            // {
            //     if (_deletePage.errormessage != string.Empty)
            //     {
            //         CustomMessageBox.ShowDialog($"Delete TrueID Fail! action 0 Fail:[{_deletePage.errormessage}]","Delete True Shoe",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
            //         this.DialogResult = false;
            //         return;
            //     }
            //
            //     CustomMessageBox.ShowDialog("Box ID is Deleted successfully.","Delete True Shoe",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Information);
            // }
            // else 
            // {
            //     // other exception from dialog. ex: connect fail.
            //     // normal cancel don't show ErrorBox.
            //     if (_scanTrueIdPage.errormessage != string.Empty)
            //     {
            //         CustomMessageBox.ShowDialog($"Scan Box ID:{_deletePage.errormessage}", "Delete Shoe",
            //             CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
            //     }
            //
            // }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }
        finally
        {
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }
        }
       
        


    }

    private async void DestroyTrueShoeBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Log4netManager.Logger.Info($"DestroyTrueShoeBtn Click");
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Hide();
        }

        // Destroy also use scan page to get true box
        if (!await _ScanTrueIDMethod(null,DataCenter.ScanType.normal))
        {
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }



        //bool checkTrueStatusbreak = false;
        //int? currentaction = DataCenter.CurrentTrueBox.action;
        string? nowPrintName = DataCenter.CurrentTrueBox.PrintName;

        CustomMessageBoxResult boxresult = CustomMessageBox.ShowDialog($"Destroy 🔹Box ID:{nowPrintName}", "Destroy",
            CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Question);


        if (boxresult == CustomMessageBoxResult.Yes)
        {

            try
            {
                string? nowscanid = DataCenter.CurrentTrueBox.trueId;
                string actionDestroyError = await DoTrueBoxActionManager.DoTrueBoxActionDestroyAsync(nowscanid);
                if (!string.IsNullOrEmpty(actionDestroyError))
                {

                    Log4netManager.Logger.Info(
                        $"Destroy this shoe Fail \r\n{actionDestroyError}\r\n Box ID:{nowPrintName}");
                    CustomMessageBox.ShowDialog($"🔹Box ID:{nowPrintName} \r\n" +
                                                $"{actionDestroyError}", "Destroy shoe",
                        CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                    //throw new Exception($"Action 3 failed: {action5Error}");
                }
                else
                {
                    Log4netManager.Logger.Info($"Destroy this True Successfully.Box ID:{nowPrintName}");
                    CustomMessageBox.ShowDialog($"🔹Box ID:{nowPrintName} \r\n\r\n" +
                                                $"It is destroyed successfully.", "Destroy shoe",
                        CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
                }
            }
            catch (Exception ex)
            {
                Log4netManager.Logger.Info($"DestroyTrueShoeBtn exception:{ex.Message}");
                CustomMessageBox.ShowDialog($"🔹Box ID:{nowPrintName}\r\nDestroy this shoe Fail \r\n{ex.Message}",
                    "Destroy shoe",
                    CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            }
            finally
            {
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Show();
                }
            }

        }
        else
        {
            // Cancel
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }
        }

    }

    private void AdministratorBtn_OnClick(object sender, RoutedEventArgs e)
    {

        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Hide();
        }

        try
        {
            // -Get SoftWare setting form instance and bind
            Log4netManager.Logger.Info($"AdministratorBtn Click");
            _administratorSettingPage = new AdministratorSettingPage();
            _administratorSettingPage.ShowDialog();
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"AdministratorBtn exception:{ex.Message}");
            CustomMessageBox.ShowDialog($"Administrator setting exception \r\n{ex.Message}", "Administrator setting",
                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
        }
        finally
        {
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }
        }

    }

    private async void RegisterShoeBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Log4netManager.Logger.Info($"RegisterShoeBtn_OnClick Click");
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Hide();
        }

        if (!await _ScanTrueIDMethod(null, DataCenter.ScanType.normal))
        {
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }

        //This True is Action 4?
        if (DataCenter.CurrentTrueBox.action == null)
        {
            CustomMessageBox.ShowDialog($" Scan ID No Data Record\r\n" +
                                        $"(Error: No data).\r\n" , "Register the shoe",
                CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }
            return;
        }
        double? currentaction = DataCenter.CurrentTrueBox.action;
        string? nowPrintName = DataCenter.CurrentTrueBox.PrintName;
        string? nowDomain = DataCenter.CurrentTrueBox.DomainUi;
        
        Log4netManager.Logger.Info($"RegisterShoeBtn CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}] action is [{DataCenter.CurrentTrueBox.action}]");

        if (DataCenter.MyShufflerSettings.Domain != nowDomain)
        {
            CustomMessageBox.ShowDialog($"🔹Box ID:{nowPrintName} \r\n" +
                                        $"Action not allowed."+
                                        $"(Error:Current Domain is [{nowDomain}].Box Domain is [{DataCenter.MyShufflerSettings.Domain}])\r\n" , "Register the shoe",
                CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }
            return;
        }
       
        
        if (currentaction != null && !(currentaction == 4 || currentaction == 5))
        {
            CustomMessageBox.ShowDialog($"🔹Box ID:{nowPrintName} \r\n" +
                                        $"Action not allowed."+
                                        $"(Error: Action {currentaction}).\r\n" , "Register the shoe",
                CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }
            return;
        }
        
        
        // Ready to Register out
        TrueBoxViewModel vm = new TrueBoxViewModel();
        vm.OneBoxModel = DataCenter.CurrentTrueBox;
        
        if (currentaction== 4 || currentaction== 5)//Ready to Register Out
        {
            vm.OneBoxModel.Statusdescreption = "no shoe status";
            
            //bool? registerResult = true;
            _registerOutPage = new RegisterOutPage(vm,currentaction);
            var registerResult = _registerOutPage.ShowDialog();
            Log4netManager.Logger.Info($"_doRunShuffleProcess CurrentTrueBox[{DataCenter.CurrentTrueBox.PrintName}]. Register Result: {registerResult}");

            // //press cancel button
            // if (pullResult == false)
            // {
            //     return;
            // }
        }
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Show();
        }

        
    }

    /// <summary>
    ///  Demo WebApi Button
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void webAPItestButton_OnClick(object sender, RoutedEventArgs e)
    {

        try
        {
            //Zack Api 1
            // string responseJsonstring = await WebserviceSingleTonManager.Instance.SendGetAsync("https://bi-dev.theiehicppgcs.com/api/v1/on-site/shuffle-room/users/2000018?siteDomainName=MX");
            // // Your code to handle the successful response
            // Console.WriteLine($"SendGetAsync success:{responseJsonstring}");
            //
            // var userInfo = JsonSerializer.Deserialize<MemberInfoModel>(responseJsonstring);
            // Console.WriteLine($"User ID: {userInfo.id}");
            // Console.WriteLine($"Role: {userInfo.roleName}");
            // Console.WriteLine($"State: {userInfo.stateName}");
            
            //Zack Api 2
            // string responseJsonstring = await WebserviceSingleTonManager.Instance.SendGetAsync("https://bi-dev.theiehicppgcs.com/api/v1/on-site/shuffle-room/game-tables?siteDomainName=CB");
            // // Your code to handle the successful response
            // Console.WriteLine($"SendGetAsync success:{responseJsonstring}");
            //
            // var memberInfo = JsonSerializer.Deserialize<MemberInfoModel>(responseJsonstring);
            //
            // List<string> gametablelist = memberInfo.data.tables;
            // foreach (var table in gametablelist)
            // {
            //     Console.WriteLine($"gameTables table: {table}");
            // }
            // Console.WriteLine($"gameTables code: {memberInfo.code}");
            
            // ////Bill Api
            // ////Get True ID Info
            // //TrueBoxModel demomodel = await DoTrueBoxActionManager.GetTrueIdUpdateCurrentBox("QC-891f5745-b155-4132-adb3-71ae0cad6bfc");
            // ////
            // string error = await DoTrueBoxActionManager.DoTrueBoxActionSeven("QC-49c4cdb9-0040-4257-b5ee-846508fce986");
            // //string error2 = await DoTrueBoxActionManager.DoTrueBoxActionSix("QC-592bdbf6-a15b-4b25-b5cd-d04eeb0603f4");
            // if (error != string.Empty)
            // {
            //     Log4netManager.Logger.Error($"DoTrueBoxActionSix error \r\n{error}");
            // }

            //modbusdemo();

            // await _clark.ConnectShuffler();
            // _clark.GetorAddClarkItem("127.0.0.1");
            
            ////Bill Api
            ////Get True ID Info
            //TrueBoxModel demomodel = await DoTrueBoxActionManager.GetTrueIdUpdateCurrentBox("QC-891f5745-b155-4132-adb3-71ae0cad6bfc");

            var tempcpndition = new CreateIDPageViewModel
            {
                SelectedTableId = "101",
                SelectedColorType = "Blue",
                SelectedIsMain = "Regular"
            };
            string error = await DoTrueBoxActionManager.DoTrueBoxActionDeleteByTableColorMainAsync(tempcpndition);
            //string error2 = await DoTrueBoxActionManager.DoTrueBoxActionSix("QC-592bdbf6-a15b-4b25-b5cd-d04eeb0603f4");
            if (error != string.Empty)
            {
                Log4netManager.Logger.Error($"DoTrueBoxActionSix error \r\n{error}");
            }
            

        }
        catch (HttpRequestException ex)
        {
            // 捕捉 HTTP 請求的例外，例如 400 Bad Request
            Console.WriteLine($"發生 HTTP 錯誤: {ex.Message}");
        }
        catch (Exception ex)
        {
            // 捕捉其他所有例外
            Console.WriteLine($"發生未預期的錯誤: {ex.Message}");
        }
    }

    private async void modbusdemo(int mode =0)
    {
        byte[]? response = null;
        
        // 讀取 unitId=1，起始位址 0x0000，數量 2 個保持暫存器
        // 讀取保持暫存器(0x03)  MBAP(7 bytes) +PDU 可變：功能碼 + 起始位址 + 讀取筆數。
        ushort transactionId = 1;
        ushort protocoakId = 0;
        byte unitId = 1;
        byte functionCode = 0x03;
        ushort startAddress = 0x0001;

        ushort lengthField = (ushort)(1 + 5);
        byte[] request = new byte[12];// 6(header) + unitId + PDU(5)
        
        // MBAP header (first 6 bytes)
        // transactionId  protocoakId  lengthField   unitId
        // 0001           0000         0006          01
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(0, 2), transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2, 2), protocoakId);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4, 2), lengthField);
        request[6] = unitId;
        
        if (mode == 0)//Read mode
        {
            // PDU
            // Fuctioncode     StartAddress    quantity
            // 03              0000            0002
            functionCode = 0x03;
            request[7] = functionCode;
            ushort quantity = 2;
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8,2),startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10,2),quantity);

           
            
        }
        else if(mode==1)//Write Single mode
        {
            // PDU
            // Fuctioncode     TargetAddress    WriteData
            // 06              0000            0002
            functionCode = 0x06;
            ushort writedata = 2;
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8,2),startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10,2),writedata);

        }
        else if (mode == 2)//Write Multiple mode
        {
            // PDU
            // Fuctioncode     TargetAddress  Quantity  Data1  Data2.....DataNN
            // 10              0000           00NN  
            
            functionCode = 0x10;
            ushort quantity = 2;
            ushort writedata = 11;
            ushort writedata2 = 22;
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8,2),startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10,2),quantity);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(12,2),writedata);
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(14,2),writedata2);
            
            // Feedback response need this
            // transactionId   protocoakId   TargetAddress UnitId  functionCode StartAddress Quantity
            // 0001            0000          0006          01      10           00C8         0002
        }
        
        // Send!!!!
        //response = await ModbusTCPManager.Instance.SendCommandAsync(request);
        
        // Analyze Response
        // (功能碼 0x03)成功回應: MBAP(7 bytes) + PDU MBAP:
        // Byte 0~1: Transaction Id
        // Byte 2~3: Protocol Id (0)
        // Byte 4~5: Length (= UnitId(1) + PDU 長度)
        // Byte 6: UnitId PDU:
        // Byte 7: Function Code (0x03)
        // Byte 8: ByteCount (後面資料區的總字節數，= 寄存器數量 * 2)
        // Byte 9~(9+ByteCount-1): 資料，每個寄存器 2 bytes, 高位在前(Big Endian)
        ushort respTransactionId = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0,2));
        byte respUnitId = response[6];
        byte respFunction = response[7];

        if (respFunction == functionCode)
        {
            byte byteCount = response[8];
            for (int i = 0; i < byteCount / 2; i++)
            {
                var regValue = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(9 + i * 2, 2));
            }
        }
        else if ((respFunction & 0x80) != 0)// 0x80 = 1000 0000
        {
            byte exceptionCode = response[8];
        }
        
    }
    
    private async void ShufflerSettingBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Log4netManager.Logger.Info($"ShufflerSettingBtn_OnClick Click");

        //bool? autoResult = true;
        
        CustomMessageBox connectbox = CustomMessageBox.Show($"Machine Server[{DataCenter.MyShufflerSettings.ShufflerMachineServerIp}] Connecting...","Shuffler",CustomMessageBoxButtonType.NoneBtn, CustomMessageBoxIcon.Information);

        bool?shufflerConnect =await ShufflerManager.Instance.ConnectShuffler();
        
        connectbox.Close();
        
        if (shufflerConnect==false)
        {
            CustomMessageBox.ShowDialog($"Machine Server[{DataCenter.MyShufflerSettings.ShufflerMachineServerIp}] Connection Failed!","Shuffler",CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }
        
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Hide();
        }
        
        //_shufflerSettingPage = new ShufflerSettingPage();
        _shufflerSettingPage.Value.Show();
            
    }

    private async void TelegrameTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        await TelegramBotTypeManager.Pd2GroupGb.SendMessageGbAsync("This is a test message from ShufflerWPF app.\r\n 中文測試\r\n English Test");
    }

    private void loginOutButtonButton_OnClick(object sender, RoutedEventArgs e)
    {
        Log4netManager.Logger.Info($"loginOutButonButton_OnClick");
        try
        {
            this.Hide();
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error("Error hide window.", ex);
        }
        finally
        {
            if (Application.Current.MainWindow!=null && Application.Current.MainWindow.IsVisible == false)
            {
                this.Show();
            }
        }
    }
    private void DragMe(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException ex)
        {
            Log4netManager.Logger.Error($"DragMove Fail: {ex.Message}");
        }
    }

    private async void PrintBoxQrCodeBtn_OnClick(object sender, RoutedEventArgs e)
    {
        // Scan
        Log4netManager.Logger.Info($"PrintBoxQrCodeBtn Click");
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Hide();
        }
        if (! await _ScanTrueIDMethod(null,DataCenter.ScanType.normal))
        {
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }
        //int? currentaction =DataCenter.CurrentTrueBox.action;
        //string? nowPrintName = DataCenter.CurrentTrueBox.PrintName;
        string? nowscanid = DataCenter.CurrentTrueBox.trueId;
        
        // Print
        var splashbox = CustomMessageBox.Show("Print Shoe Information...", "Please wait...", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Information);

        Ql810WManager q810WManager = new Ql810WManager();
        q810WManager.PrintInfo.Printmode = Ql810WManager.PrintMode.ShoeQRcode;
        await q810WManager.Ql810DoPrint(nowscanid);
        
        splashbox.Close();
        
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Show();
        }
    }

    private async void PrintBoxInformationBtn_OnClick(object sender, RoutedEventArgs e)
    {
        // Scan
        Log4netManager.Logger.Info($"Re PrintBoxInformationBtn Click");
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Hide();
        }
        if (! await _ScanTrueIDMethod(null,DataCenter.ScanType.normal))
        {
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Show();
            }

            return;
        }
        // int? currentaction =DataCenter.CurrentTrueBox.action;
        // string? nowPrintName = DataCenter.CurrentTrueBox.PrintName;
        // string? nowscanid = DataCenter.CurrentTrueBox.trueId;
        
        // Print
        var splashbox = CustomMessageBox.Show("Print Shoe Information...", "Please wait...", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Information);

        var box = DataCenter.CurrentTrueBox;
        if (box?.action < (int)DoTrueBoxActionManager.TrueBoxAction.Printed || box?.action==(int)DoTrueBoxActionManager.TrueBoxAction.Delete)
        {
            Log4netManager.Logger.Info($"Re PrintBoxInformationBtn Click:TrueId is [{box?.trueId}] action is [{box?.action}] Not allowed to print Shoe Information.");
            splashbox.Close();
            
            CustomMessageBox.ShowDialog($"Re PrintBoxInformationBtn Click:TrueId is [{box?.trueId}] action is [{box?.action}] Not allowed to print Shoe Information.", "Print",
                CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
            
        }
        else// allowed to print
        {
            Ql810WManager q810WManager = new Ql810WManager();
            q810WManager.PrintInfo.Printmode = Ql810WManager.PrintMode.ShoeInfo;
            await q810WManager.Ql810DoPrint("");
        
            splashbox.Close();
        }
        
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Show();
        }
    }
    
    /// <summary>
    /// Show Scan True ID Page and Run API GetTrueIdUpdateCurrentBox.
    /// </summary>
    /// <returns></returns>
    private async Task<bool> _ScanTrueIDMethod(List<string>? gametable = null, DataCenter.ScanType? scantype =null)
    {
        
        if (scantype == DataCenter.ScanType.normal)
        {
            _scanTrueIdPage = new ScanBoxIDPage();
            bool? scanresult = _scanTrueIdPage.ShowDialog();
            if (_scanTrueIdPage.errormessage=="cancel")
            {
                return false  ;
            }
            // server feedback OK(not null)
            if (scanresult == true && _scanTrueIdPage.ScannedData != null)
            {
                if (!string.IsNullOrEmpty(_scanTrueIdPage.ScannedData.TagMessage))
                {
                    //Error from server
                    CustomMessageBox.ShowDialog($"Box ID:{_scanTrueIdPage.ScannedData.TagMessage} not found.", "Scan",
                        CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
                    return false;
                }
                
                //PASS Update here~~~~~~~~~~~
                DataCenter.CurrentTrueBox = _scanTrueIdPage.ScannedData;
                return true;
                
            }
            
            // other exception from dialog. ex: connect fail.
            // normal cancel don't show ErrorBox.
            if (_scanTrueIdPage.errormessage != string.Empty)
            {
                CustomMessageBox.ShowDialog($"Scan Box ID:{_scanTrueIdPage.errormessage}", "Scan ID",
                    CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
            }
            return false;
            
        }
        
        if (scantype == DataCenter.ScanType.delete)
        {
            _deletePage = new DeletePage(gametable, scantype);
            var deleteresult = _deletePage.ShowDialog();
            
            // delete by result
            if (_deletePage.errormessage=="cancel")
            {
                return false  ;
            }
            if (deleteresult == true && _deletePage.ScannedData != null)
            {
                if (!string.IsNullOrEmpty(_deletePage.ScannedData.TagMessage))
                {
                    // Error from server
                    CustomMessageBox.ShowDialog($"Box ID:{_deletePage.ScannedData.TagMessage} not found.", "Scan",
                        CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
                    
                    return false ;
                }

                // PASS
                DataCenter.CurrentTrueBox = _deletePage.ScannedData;
                
                //int? currentaction = DataCenter.CurrentTrueBox.action;
                string? nowPrintName = DataCenter.CurrentTrueBox.PrintName;
                string? nowscanid = DataCenter.CurrentTrueBox.trueId;
                
                CustomMessageBoxResult boxresult = CustomMessageBox.ShowDialog($"Delete🔹Box Id:{nowPrintName}", "Delete",
                    CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Question);

                if (boxresult == CustomMessageBoxResult.Yes)
                {

                    //run delete webservice
                    try
                    {

                        string actiondeleteError = await DoTrueBoxActionManager.DoTrueBoxActionDeleteAsync(nowscanid);

                        if (!string.IsNullOrEmpty(actiondeleteError))
                        {

                            Log4netManager.Logger.Info(
                                $"Cannot delete this shoe. \r\n{actiondeleteError}\r\n Box ID:{nowPrintName}");
                            CustomMessageBox.ShowDialog($"Cannot delete this shoe. \r\n{actiondeleteError}", "Delete shoe",
                                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                            //throw new Exception($"Action 3 failed: {action5Error}");
                        }
                        else
                        {
                            CustomMessageBox.ShowDialog($"🔹Box ID:{nowPrintName} \r\n\r\n" +
                                                        $"It is deleted successfully.", "delete",
                                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
                            Log4netManager.Logger.Info($"It is deleted successfully.\r\n Box ID:{nowPrintName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log4netManager.Logger.Info($"DeleteTrueShoeBtn exception:{ex.Message}");
                        CustomMessageBox.ShowDialog($"🔹Box ID:{nowPrintName} \r\nDelete this shoe Fail \r\n{ex.Message}",
                            "Delete",
                            CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                    }
                }
                
            } // delete by table
            else if (deleteresult == true && _deletePage.ScannedData == null)
            {
                if (_deletePage.errormessage != string.Empty)
                {
                    CustomMessageBox.ShowDialog($"Delete TrueID Fail! action 0 Fail:[{_deletePage.errormessage}]","Delete True Shoe",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
                    //this.DialogResult = false;
                    return false;
                }

                CustomMessageBox.ShowDialog("Box ID is Deleted successfully.","Delete True Shoe",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Information);
                return true;
            }
            else 
            {
                // other exception from dialog. ex: connect fail.
                // normal cancel don't show ErrorBox.
                var error = _deletePage?.errormessage;
                if (!string.IsNullOrEmpty(error))
                {
                    CustomMessageBox.ShowDialog($"Scan Box ID:{error}", "Scan ID",
                        CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                }
                return false;
            }
        }
        return false;
    }

    private async Task<List<string>?> _getgametable()
    {
        string? responseJsonstring = string.Empty;
        
        // 1.Use BICert to get Token. From environment parameter or default value.
        string bicert = "g7cCS@F@k9uVBmrY%";
        if (DataCenter.MyShufflerSettings.LiveMode == "True")
        {
            bicert = DataCenter.MyShufflerSettings.BiOpToken;
        }
        
        // 2.Build custom header
        var customHeaders = new Dictionary<string, string>
        {
            { "cert", bicert },
            { "request-source-uuid", Guid.NewGuid().ToString() }
        };
        
        try
        {
                // 3. Create APIProxy Request Item
                var apirequest = new DataCenter.APIProxyRequestItem
                (
                    DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                    $"{DataCenter.MyShufflerSettings.TableListApi}?siteDomainName={DataCenter.MyShufflerSettings.Domain}",
                    HttpMethod.Get,
                    null,
                    customHeaders,
                    WebserviceSingleTonManager.RequestModelType.ApiProxy,
                    WebserviceSingleTonManager.ApiProxyType.Parallel
                );
                
                // Set content type if needed
                //apirequest.EndpointContentType = "application/json";
                
                // 4. Send APIProxy Request
                var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);
                //var (externalRaw,errMsg) = await DataCenter.APIProxySender($"{DataCenter.MyShufflerSettings.AuthApi}/{id}?siteDomainName={DataCenter.MyShufflerSettings.Domain}", null,  null,"application/json",WebserviceSingleTonManager.ApiProxyType.Parallel, customHeaders,HttpMethod.Get);
                
                // 5. Analyze Response
                if (externalRaw != null && string.IsNullOrEmpty(errMsg))
                {
                    //var responseTrueBoxModel = externalRaw.Deserialize<ShufflerJsonModel>();
                    responseJsonstring = externalRaw;
                    
                    var memberInfo = JsonSerializer.Deserialize<MemberInfoWebServiceModel>(responseJsonstring);
                    var gametablelist = memberInfo?.data.tables;
                    if (gametablelist == null || gametablelist.Count == 0)
                    {
                        Log4netManager.Logger.Error(
                            "Table not found due to network connection or API issue.\n(Error: 443)");
                        CustomMessageBox.ShowDialog("Table not found due to network connection or API issue.", "Error",
                            CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                        return null; // 中斷流程
                    }

                    return gametablelist;
                    
                }
                return null;
        }
        catch (HttpRequestException httpEx)
        {
            Log4netManager.Logger.Error($"Fetch game tables HttpRequestException: {httpEx.Message}", httpEx);
            CustomMessageBox.ShowDialog($"Table not found due to network connection issue.\r\n{httpEx.Message}",
                "Network Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            return null; // 中斷流程
        }
        catch (TaskCanceledException tcEx)
        {
            Log4netManager.Logger.Error($"Fetch game tables timeout: {tcEx.Message}", tcEx);
            CustomMessageBox.ShowDialog($"Get Game Tables timeout. Please retry.\r\n{tcEx.Message}", "Timeout",
                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            return null; // 中斷流程
        }
        catch (Exception exFetch)
        {
            Log4netManager.Logger.Fatal("Fetch game tables unexpected error", exFetch);
            CustomMessageBox.ShowDialog($"Get Game Tables unexpected error.\r\n{exFetch.Message}", "Fetch Error",
                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            return null; // 中斷流程
        }

    }
    
    
}
