using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShufflerWPF.Model;
using ShufflerWPF.Manager;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Pages;

public partial class DeletePage : Window
{
    public DeletePage(List<string>? gametablelist, DataCenter.ScanType? scantype=null)
    {
        InitializeComponent();

        if (gametablelist == null)
        {
            TableBorder.Visibility = Visibility.Hidden;
            return;
        }

        if (scantype == null || scantype == DataCenter.ScanType.normal)
        {
            
            
            return;
        }
        
        if (DataContext is CreateIDPageViewModel vm)
        {
            foreach (var tableidid in gametablelist)
            {
                vm.TableIDList.Add(tableidid);
            }
            
            vm.ColorType.Add("Red");
            vm.ColorType.Add("Blue");
            
            vm.IsMain.Add("Regular");
            vm.IsMain.Add("Extra");

            vm.OnCloseDeletePageDialog = tuple =>
            {
                this.DialogResult = tuple.Item1;
                this.errormessage = tuple.Item2;
            };

        }
    }
     // --- WinAPI 導入 ---
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, DeletePage.LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
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
    private static DeletePage.LowLevelKeyboardProc _proc = HookCallback; // Static method
    private static IntPtr _hookID = IntPtr.Zero; // Hook pointer
    
    private const int SCANNER_INPUT_THRESHOLD_MS = 300; // <--- 關鍵閾值：毫秒
    private Stopwatch _scanTimer = new Stopwatch();
    private StringBuilder _scannedDataBuilder = new StringBuilder();
    
    [DllImport("user32.dll")]
    static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll")]
    static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

    const uint KLF_SETFORPROCESS = 0x00000100;
    public TrueBoxModel? ScannedData { get; private set; }
    public string? scannedData = string.Empty;
    public string? errormessage = string.Empty;

    // private async void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    // {
    //     //clear scan data
    //     ScannedData = null;
    //     
    //     if (DataContext is CreateIDPageViewModel vm)
    //     {
    //         if (vm.SelectedTableId == null || vm.SelectedColorType == null ||
    //             vm.SelectedIsMain == null)
    //         {
    //             CustomMessageBox.ShowDialog("Ensure to complete all the fields.","Create True Shoe",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Warning);
    //             this.DialogResult = false;
    //             return;
    //         }
    //        
    //         try
    //         {
    //             var contentdata =
    //                 $"Is the printing info below correct??\r\n\r\n🔹Table No. = {vm.SelectedTableId}\r\n\r\n🔹Card Color={vm.SelectedColorType}\r\n\r\n🔹Purpose={vm.SelectedIsMain}";
    //             CustomMessageBoxResult boxresult = CustomMessageBox.ShowDialog(contentdata, "Delete ID",
    //                 CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Warning);
    //
    //             if (boxresult == CustomMessageBoxResult.Yes)
    //             {
    //                 this.errormessage = await DoTrueBoxActionManager.DoTrueBoxActionDeleteByTableColorMainAsync(vm);
    //                 this.DialogResult = true;
    //             }
    //             
    //         }
    //         catch (Exception ex)
    //         {
    //             Console.WriteLine(ex.Message);
    //             this.DialogResult = false;
    //             throw;
    //         }
    //         
    //         
    //     }
    // }

    private async void DeleteIDPage_PreviewKeyDown(object sender, KeyEventArgs e)
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
                scannedData = _scannedDataBuilder.ToString();
                _scannedDataBuilder.Clear();
                
                if (string.IsNullOrWhiteSpace(scannedData))
                {
                    Console.WriteLine("掃描內容為空，不處理。");
                    return; 
                }
                try
                {
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
        Log4netManager.Logger.Info($"DeletePage CancelButton Click");
        this.errormessage ="cancel";
        this.DialogResult = false;
        //this.Close();
    }
    
}