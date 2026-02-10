using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ShufflerWPF.SingleTon;
using ShufflerWPF.Manager;
using System.Text.Json;
using ShufflerWPF.Model;
using System.Globalization;
using System.Net.Http;

namespace ShufflerWPF.Pages;

public partial class UserScanIdPage : Window
{
    #region [HookKeyboardApi]

   
    // --- WinAPI 導入 ---
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll", SetLastError = true)]
    static extern int ToAscii(uint uVirtKey, uint uScanCode, byte[] lpKeyState, StringBuilder lpChar, int cchBuff, uint uFlags);
    
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetKeyboardState(byte[] lpKeyState);
    
    [DllImport("user32.dll")]
    static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("user32.dll")]
    private static extern int ToUnicode(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] 
        StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    // --- WinAPI constant ---
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const UInt32 SWP_NOSIZE = 0x0001;
    const UInt32 SWP_NOMOVE = 0x0002;
    const UInt32 SWP_SHOWWINDOW = 0x0040;

    // --- Hook constant and delegate ---
    private const int WH_KEYBOARD_LL = 13; // 低階鍵盤 Hook
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104; //
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static LowLevelKeyboardProc _proc = HookCallback; // Static method
    private static IntPtr _hookID = IntPtr.Zero; // Hook pointer
    
    private const int SCANNER_INPUT_THRESHOLD_MS = 300; // <--- 關鍵閾值：毫秒
    private Stopwatch _scanTimer = new Stopwatch();
    private StringBuilder _scannedDataBuilder = new StringBuilder();
    
    [DllImport("user32.dll")]
    static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll")]
    static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

    const uint KLF_SETFORPROCESS = 0x00000100;
    #endregion
    private static bool _hookActive = false; // P2: 防重複掛載旗標
    private static readonly object _hookLock = new(); // P2: 鎖保護多執行緒同時開頁
    public UserScanIdPage(int? scanmode=null)
    {
        InitializeComponent();
        this.Topmost = true;
        
        // Focus on text box.
        this.Loaded += ScanIDPage_Loaded;
        
        // Keep focus on. if recover again.
        this.Activated += ScanIDPage_Activated;
        
        this.Closing += ScanIDPage_Closing; // remove Hook when cloing

        if (scanmode != null && scanmode == 1)
        {
            CancelButton.Visibility = Visibility.Visible;
        }
        else
        {
            CancelButton.Visibility = Visibility.Hidden;
        }

    }
    
    private void ScanIDPage_Loaded(object sender, RoutedEventArgs e)
    {
        ForceWindowToTopmost(); 
        ScanIdInput.Focus();
        // P2: 掛載 Hook 前確保未重複
        lock (_hookLock)
        {
            if (_hookActive && _hookID != IntPtr.Zero)
            {
                try
                {
                    if (!UnhookWindowsHookEx(_hookID))
                    {
                        // UnhookWindowsHookEx returns false on failure
                        int errorCode = Marshal.GetLastWin32Error();
                        Log4netManager.Logger.Warn($"Failed to unhook keyboard hook. Error code: {errorCode}");
                    }
                }
                catch (Exception ex)
                {
                    Log4netManager.Logger.Error($"Exception unhooking keyboard hook: {ex}");
                }
                _hookID = IntPtr.Zero;
                _hookActive = false;
            }
            _hookID = SetHook(_proc); // Set Keyboard Hook
            _hookActive = _hookID != IntPtr.Zero;
        }
        Console.WriteLine("ScanIDPage_Loaded Finish");
        Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
        {
            // Switch to English Input
            bool ok = SwitchToEnglishInput();
            if (!ok)
            {
                Log4netManager.Logger.Warn("SwitchToEnglishInput 初次失敗，啟動重試...");
                await RetryEnsureEnglishLayoutAsync();
            }
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        // switch to English Input when focus
        ScanIdInput.GotKeyboardFocus += (s, ea) =>
        {
            if (!IsEnglishInput())
            {
                bool ok2 = SwitchToEnglishInput();
                if (!ok2)
                {
                    _ = RetryEnsureEnglishLayoutAsync(3, 150);
                }
            }
        };
    }
    private void ScanIDPage_Activated(object? sender, EventArgs e)
    {
        ForceWindowToTopmost();
        ScanIdInput.Focus();
        Application.Current.Dispatcher.Invoke(() =>
        {
            SwitchToEnglishInput();
        });
    }
    private void ForceWindowToTopmost()
    {
        WindowInteropHelper wih = new WindowInteropHelper(this);
        IntPtr hWnd = wih.Handle; 

        if (hWnd != IntPtr.Zero)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (System.Diagnostics.Process curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (System.Diagnostics.ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }
    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam); 
            bool isAltPressed = (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt));
            bool isCtrlPressed = (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));
            bool isShiftPressed = (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));

            if ((isAltPressed && (vkCode == (int)Key.Tab || vkCode == (int)Key.Escape)) ||
                (isCtrlPressed && vkCode == (int)Key.Escape) ||
                (vkCode == 91 || vkCode == 92) ||
                (isCtrlPressed && isShiftPressed && vkCode == (int)Key.Escape))
            {
                return (IntPtr)1;
            }
        }
        // Keep Hook to next Hook
        return CallNextHookEx(_hookID, nCode, wParam, lParam); 
    }
    private void ScanIDPage_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        lock (_hookLock)
        {
            if (_hookActive && _hookID != IntPtr.Zero)
            {
                try { UnhookWindowsHookEx(_hookID); } catch { }
                _hookID = IntPtr.Zero;
                _hookActive = false;
            }
        }
        Console.WriteLine("ScanIDPage: Remove Hook Finish.");
    }
    
    //Scan handle
    private async void ScanIDPage_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 處理 = 鍵，允許退出，進入手動模式
        if (e.Key == Key.OemPlus || e.Key == Key.Add)
        {
            this.DialogResult = true;
            this.Close();
            e.Handled = true;
            DataCenter.AdministratorMode = true;
            DataCenter.CurrentMember.id = 0;
            return;
        }

        // --- 偵測掃描槍的輸入 ---
        bool isScannerInput = true;
        long elapsed = _scanTimer.ElapsedMilliseconds;

        // 超過閒置時間 => 清空 buffer
        if (elapsed > SCANNER_INPUT_THRESHOLD_MS)
        {
            _scannedDataBuilder.Clear();
            Console.WriteLine($"[Timeout] 超過 {SCANNER_INPUT_THRESHOLD_MS}ms，清空輸入內容。");
        }

        //// 重置計時器，為下一次按鍵做準備
        _scanTimer.Restart();

        // 如果是掃描槍輸入，或���按鍵是 Enter，就處理
        if (isScannerInput || e.Key == Key.Enter)
        {
            e.Handled = true; // 標記為已處理，防止按鍵被系統處理

            if (e.Key == Key.Enter)
            {
                string scannedData = _scannedDataBuilder.ToString();
                _scannedDataBuilder.Clear();
                
                if (string.IsNullOrWhiteSpace(scannedData))
                {
                    Console.WriteLine("掃描內容為空，不處理。");
                    return; 
                }
                
                // try
                // {
                
                    // avoid multiple click
                    ScanIdInput.IsEnabled = false;
                    var (ok, err, raw) = await CheckUserIdLevel(scannedData);
                    //var (ok, err, raw) = await CheckUserIdLevelPost(scannedData);
                    if (IsLoaded)
                    {
                        ScanIdInput.IsEnabled = true;
                    }

                    if (!ok)
                    {
                        Log4netManager.Logger.Error($"CheckUserIdLevel:{scannedData} failed: {err}; Raw: {raw}");
                        
                        
                        // 保證在 UI Thread 顯示並關閉視窗
                        if (!Dispatcher.CheckAccess())
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                CustomMessageBox.ShowDialog($"User ID not found.:\r\nError: {err}", "Error",
                                    CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                            });
                        }
                        else
                        {
                            CustomMessageBox.ShowDialog($"User ID not found.:\r\nError: {err}", "Error",
                                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
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
                //}
                // catch (Exception ex)
                // {
                //     Log4netManager.Logger.Error($"CheckUserIdLevel exception: {ex.Message}");
                //     Log4netManager.Logger.Error($"Scan Again!!!");
                //     Console.WriteLine($"CheckUserIdLevel exception: {ex.Message}");
                //     //Scan Again!!!!
                //     this.DialogResult = false;
                //     this.Close();
                //     return;
                // }
                
                Console.WriteLine($"[Scan Finish] Final value is: {scannedData}");
                
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                // 將按鍵轉換為字元並添加到 StringBuilder
                string keyChars = GetCharsFromKeys(e.Key, Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
                _scannedDataBuilder.Append(keyChars);
                Console.WriteLine($"[Scan Finish] Key: {e.Key} -> '{keyChars}'。Current Value is: '{_scannedDataBuilder}'");
            }
        }
        else
        {
            // 如果是人類輸入，則攔截它，不讓它影響程式
            e.Handled = true;
            Console.WriteLine($"[Normal Keyboard] Key: {e.Key}。Blocked。");
        }
    }
    private async Task<(bool Success, string? Error, string Raw)> CheckUserIdLevel(string id)
    {
        // {
        //     "id": 2000018,
        //     "siteDomainName": "Global",
        //     "roleName": "Supervisor",
        //     "stateName": "PasswordDefault"
        // }
        // 3000022:
        // {
        //     "id": 3000022,
        //     "siteDomainName": "MX",
        //     "roleName": "OP",
        //     "stateName": "Close"
        // }
        // 3000026:
        // {
        //     "id": 3000026,
        //     "siteDomainName": "CB",
        //     "roleName": "OP",
        //     "stateName": "Open"
        // }
        // 4000023:
        // {
        //     "id": 4000023,
        //     "siteDomainName": "MX",
        //     "roleName": "Shuffler",
        //     "stateName": "Close"
        // }
        // 4000028:
        // {
        //     "id": 4000028,
        //     "siteDomainName": "CB",
        //     "roleName": "Shuffler",
        //     "stateName": "Open"
        // }
        var splashbox = CustomMessageBox.Show("Checking User ID Level...", "Please wait...", CustomMessageBoxButtonType.NoneBtn, CustomMessageBoxIcon.Information);

        // reset AdministratorMode
        DataCenter.AdministratorMode = false;
        
        // if (id == "9000001")
        // {
        //     DataCenter.CurrentMember.roleName = "Administrator";
        //     DataCenter.CurrentMember.stateName = "Open";
        //     DataCenter.CurrentMember.id = 9000001;
        //     // splashbox.Close();
        //     // return;
        //     id = "2000018"; // for Zack test
        // }
        string bicert = "g7cCS@F@k9uVBmrY%";
        if (DataCenter.MyShufflerSettings.LiveMode == "True")
        {
            bicert = DataCenter.MyShufflerSettings.BiOpToken;
        }
        
        string responseJsonstring = string.Empty;
        var customHeaders = new Dictionary<string, string>
        {
            { "cert", bicert },
            { "request-source-uuid", Guid.NewGuid().ToString() }
        };

        

        try
        {
            var apirequest = new DataCenter.APIProxyRequestItem
            (
                DataCenter.MyShufflerSettings.ApiProxyUrl.Split(';', StringSplitOptions.RemoveEmptyEntries),
                $"{DataCenter.MyShufflerSettings.AuthApi}/{id}?siteDomainName={DataCenter.MyShufflerSettings.Domain}",
                HttpMethod.Get,
                null,
                customHeaders,
                WebserviceSingleTonManager.RequestModelType.ApiProxy,
                WebserviceSingleTonManager.ApiProxyType.Parallel
            );
            //apirequest.EndpointContentType = "application/json";
            
            
            var (externalRaw,errMsg) = await DataCenter.APIProxySenderByItem(apirequest);

            //var (externalRaw,errMsg) = await DataCenter.APIProxySender($"{DataCenter.MyShufflerSettings.AuthApi}/{id}?siteDomainName={DataCenter.MyShufflerSettings.Domain}", null,  null,"application/json",WebserviceSingleTonManager.ApiProxyType.Parallel, customHeaders,HttpMethod.Get);
            if (externalRaw != null && string.IsNullOrEmpty(errMsg))
            {
                //var responseTrueBoxModel = externalRaw.Deserialize<ShufflerJsonModel>();
                responseJsonstring = externalRaw;
            }
            //string responseJsonstring = await WebserviceSingleTonManager.Instance.SendGetAsync($"https://bi-dev.theiehicppgcs.com/api/v1/on-site/shuffle-room/users/{id}?siteDomainName=MX",customHeaders);
            // responseJsonstring = await WebserviceSingleTonManager.Instance.SendGetAsync(
            //     $"{DataCenter.MyShufflerSettings.AuthApi}/{id}?siteDomainName={DataCenter.MyShufflerSettings.Domain}",
            //     customHeaders
            // );
            
            if (responseJsonstring == string.Empty || !string.IsNullOrEmpty(errMsg))                              
            {
                
                return (false, string.IsNullOrEmpty(errMsg)?"Response is Empty.Connect Server Fail.":errMsg, responseJsonstring);
            }
            
            //Console.WriteLine($"SendGetAsync CheckUserIdLevel success:{responseJsonstring}");
            Log4netManager.Logger.Info($"SendGetAsync CheckUserIdLevel success:{responseJsonstring}");

            var member = JsonSerializer.Deserialize<MemberInfoWebServiceModel>(responseJsonstring);
            if (member != null && member.code == 200)
            {
                DataCenter.CurrentMember = member;
                DataCenter.CurrentMember.idui = DataCenter.CurrentMember.id.ToString();
                Log4netManager.Logger.Info($"User ID: {DataCenter.CurrentMember.id}\r\n" +
                                           $"Role: {DataCenter.CurrentMember.roleName}\r\n" +
                                           $"State: {DataCenter.CurrentMember.stateName}");
                
                return (true, "", responseJsonstring);

            }
            else
            {
                Console.WriteLine("CheckUserIdLevel Deserialize data null");
                Log4netManager.Logger.Info("CheckUserIdLevel Deserialize data null");

                string? errormessage = string.Empty;

                if (member == null)
                {
                    errormessage = "CheckUserIdLevel Deserialize data null";
                }
                else
                {
                    errormessage = member.message;
                }
                return (false, errormessage, responseJsonstring);
            }
        }
        catch (Exception ex)
        {
        //     Console.WriteLine($"Deserialize Error: {ex.Message}");
        //     CustomMessageBox.ShowDialog($"CheckUserIdLevel exception:\r\n {ex.Message}", "Error",
        //         CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
        //     throw new Exception($"CheckUserIdLevel Error: {ex.Message}\r\nResponse: {responseJsonstring}");
            return (false, ex.Message, responseJsonstring);
        }
        finally
        {
            try
            {
                if (splashbox != null)
                {
                    if (!Dispatcher.CheckAccess())
                    {
                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (splashbox?.IsLoaded == true)
                                {
                                    splashbox.Close();
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                // Window already closing/closed - expected
                            }
                            catch (Exception ex)
                            {
                                Log4netManager.Logger.Warn($"Error closing splash box: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        splashbox.Close();
                    }
                }
            }
            catch (Exception closeEx)
            {
                Log4netManager.Logger.Warn($"Splashbox close failed: {closeEx.Message}");
            }
        }
    }
    
    
    
    
    private static string GetCharsFromKeys(Key key, bool isShift)
    {
        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        byte[] keyboardState = new byte[256];
        GetKeyboardState(keyboardState);

        if (isShift)
        {
            keyboardState[(int)Key.LeftShift] = 0x80;
            keyboardState[(int)Key.RightShift] = 0x80;
        }

        uint scanCode = MapVirtualKey((uint)virtualKey, 0);
        StringBuilder sb = new StringBuilder(10); // 多預留空間

        int result = ToUnicode((uint)virtualKey, scanCode, keyboardState, sb, sb.Capacity, 0);

        if (result > 0)
        {
            return sb.ToString();
        }
        else
        {
            return "";
        }
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
                this.Close();
            }
            catch (InvalidOperationException)
            {
                // Window already closed - not an error
            }
            catch (Exception ex)
            {
                Log4netManager.Logger.Error($"Error closing UserScanIdPage: {ex}");
            }
            if (Application.Current?.Dispatcher?.HasShutdownStarted == false)
            {
                Application.Current.Shutdown();
            }
        }
    }
    private bool SwitchToEnglishInput()
    {
        try
        {
            // 美國英文鍵盤代碼是 "00000409"
            IntPtr hkl = LoadKeyboardLayout("00000409", KLF_SETFORPROCESS);
            if (hkl != IntPtr.Zero)
            {
                IntPtr result = ActivateKeyboardLayout(hkl, 0);
                if (result != IntPtr.Zero)
                {
                    Log4netManager.Logger.Info("ActivateKeyboardLayout 切換至 00000409 成功");
                    return true;
                }
            }
            // Fallback 使用 WPF InputLanguageManager
            var en = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
                .FirstOrDefault(c => c.Name.Equals("en-US", StringComparison.OrdinalIgnoreCase));
            if (en != null)
            {
                System.Windows.Input.InputLanguageManager.Current.CurrentInputLanguage = en;
                Log4netManager.Logger.Info("InputLanguageManager 切換至 en-US 成功(Fallback)");
                return true;
            }
            Log4netManager.Logger.Warn("SwitchToEnglishInput 失敗：未找到 en-US 文化");
            return false;
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"SwitchToEnglishInput 發生例外: {ex.Message}");
            return false;
        }
    }

    private bool IsEnglishInput()
    {
        try
        {
            var cur = System.Windows.Input.InputLanguageManager.Current.CurrentInputLanguage;
            return cur != null && (cur.Name.Equals("en-US", StringComparison.OrdinalIgnoreCase) || cur.TwoLetterISOLanguageName == "en");
        }
        catch { return false; }
    }

    private async Task RetryEnsureEnglishLayoutAsync(int retries = 5, int delayMs = 200)
    {
        for (int i = 1; i <= retries; i++)
        {
            await Task.Delay(delayMs);
            if (SwitchToEnglishInput())
            {
                Log4netManager.Logger.Info($"RetryEnsureEnglishLayout 第{i}次重試成功");
                return;
            }
        }
        Log4netManager.Logger.Error("RetryEnsureEnglishLayout 多次重試仍無法切換到英文鍵盤");
    }
    
}
