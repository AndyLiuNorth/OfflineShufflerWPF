using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Text.Json;
using ShufflerWPF.Manager;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;
using TheNewIdea.Manager;

namespace ShufflerWPF.Pages;

public partial class ScanBoxIDPage : Window
{
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
    private bool _isProcessingScan = false;
    
    [DllImport("user32.dll")]
    static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll")]
    static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

    const uint KLF_SETFORPROCESS = 0x00000100;
    public TrueBoxModel? ScannedData { get; private set; }
    public string? scannedData = string.Empty;
    public string? errormessage = string.Empty;
    
    public ScanBoxIDPage()
    {
        InitializeComponent();
        DataCenter.CurrentTrueBox = new TrueBoxModel();
        this.Loaded += ScanTrueIdPage_Loaded;
    }
    private void ScanTrueIdPage_Loaded(object sender, RoutedEventArgs e)
    {
        ScanIdInput.Focus();
        _hookID = SetHook(_proc); // Set Keyboard Hook
        Console.WriteLine("ScanIDPage_Loaded Finish");
    }
    //  Keyboard Hook
    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (System.Diagnostics.Process curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (System.Diagnostics.ProcessModule curModule = curProcess.MainModule)
        {
            // Hook set to current process software
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
                // 攔截系統熱鍵，不讓它傳播
                return (IntPtr)1;
            }
        }
        // Keep Hook to next Hook
        return CallNextHookEx(_hookID, nCode, wParam, lParam); 
    }
    private async void ScanIDPage_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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

        // 如果是掃描槍輸入，或者按鍵是 Enter，就處理
        if (isScannerInput || e.Key == Key.Enter)
        {
            e.Handled = true; // 標記為已處理，防止按鍵被系統處理

            if (e.Key == Key.Enter)
            {
                // 若已在處理一次掃描，直接略過第二次 Enter
                if (_isProcessingScan)
                    return;
                
                _isProcessingScan = true;
                
                ScanIdInput.IsEnabled = false;
                scannedData = _scannedDataBuilder.ToString();
                _scannedDataBuilder.Clear();
                
                try
                {
                    if (string.IsNullOrWhiteSpace(scannedData))
                    {
                        Console.WriteLine("掃描內容為空，不處理。");
                        throw new Exception("Scan Data is null。");
                    }
                    
                    this.errormessage = string.Empty;
                    //DataCenter.WebServiceUrl = "http://3.114.71.1:8075/event/";
                    var(scanmodel, error) = await DoTrueBoxActionManager.GetTrueIdUpdateCurrentBox(scannedData);
                    this.ScannedData = scanmodel;
                    this.errormessage = error;
                    this.DialogResult = (this.ScannedData != null);
                    //Log4netManager.Logger.Info($"Scan Id Result:[{this.ScannedData.}]");
                    // lock DataCenter 
                    // lock (DataCenter.GlobalListLock)
                    // {
                    //     // Record evey Trueid to TrueBoxGlobalList.
                    //     // backup. TrueBoxGlobalList no use right now.
                    //     if (!DataCenter.TrueBoxGlobalList.Any(t => t.trueId == DataCenter.CurrentTrueBox.trueId))
                    //     {
                    //         DataCenter.TrueBoxGlobalList.Add(DataCenter.CurrentTrueBox);
                    //         Console.WriteLine($"[Scan Finish] Add new TrueBox to list with ID: {DataCenter.CurrentTrueBox.trueId}");
                    //     }
                    //     else
                    //     {
                    //         // 如果 trueId 已存在，可以選擇不執行任何操作，或者更新現有的項目
                    //         Console.WriteLine($"[Scan Finish] TrueBox with ID: {DataCenter.CurrentTrueBox.trueId} already exists. Skip adding.");
                    //     }
                    // }
                    //await RefreshTrueIdInformation(scannedData);
                }
                catch (Exception ex)
                {
                    errormessage = $"Refresh TrueID Information exception: {ex.Message}";
                    this.DialogResult = false;
                }
                finally
                {
                    ScanIdInput.IsEnabled = true;
                    _isProcessingScan = false;
                }
                //this.Close();
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
    
    private string GetCharsFromKeys(Key key, bool isShift)
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
            return ""; // 無法轉換
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Log4netManager.Logger.Info($"ScanBoxIDPage CancelButton Click");
        this.errormessage ="cancel";
        this.DialogResult = false;
        //this.Close();
    }
}