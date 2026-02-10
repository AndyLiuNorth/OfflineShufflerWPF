using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.Intrinsics.X86;
using System.Windows;
using MaterialDesignThemes.Wpf;
using ShufflerWPF.Manager;
using ShufflerWPF.Pages;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class  App : Application
{
    

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Load for Login or splash
        // 
        // // 1. 優先檢查更新
        // if (CheckForUpdate())
        // {
        //     // 如果有更新，CheckForUpdate 會啟動更新腳本並關閉目前程式
        //     return; 
        // }

        
        bool? dialogResult = true;
        
        //start log4net
        Log4netManager.Configure();
        
        // Read Setting first. load once when new FileProcessManager;

        try
        {
            DataCenter.MyShufflerSettings = FileProcessManager.Instance.Settings;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
      
        
        
        
        // // 1. for Scan Load ID 
        // while (DataCenter.CurrentMember.stateName != "Open" && !DataCenter.AdministratorMode)
        // {
        //     // mode is first enter mode
        //     UserScanIDPage scanIdPage = new UserScanIDPage(1);
        //     /// Cancle button
        //     dialogResult = scanIdPage.ShowDialog();
        //     if (dialogResult == false)
        //     {
        //         this.Shutdown();
        //         MessageBox.Show("Shuffler APP Close.");
        //         return;
        //     }
        //     else
        //     {
        //         break;
        //     }
        // }
       
        
        if (dialogResult==true)
        {
            // if (DataCenter.AdministratorMode)
            // {
            //     EngineerScanIDPage manualPage = new EngineerScanIDPage();
            //     bool? manualPageResult = manualPage.ShowDialog();
            //     if (manualPageResult == false)
            //     {
            //         this.Shutdown();
            //         MessageBox.Show("Shuffler APP Close.");
            //     }
            // }
            
            
            
            // 2. Show Main Window
            var mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            try
            {
                if(!string.IsNullOrEmpty(FileProcessManager.Instance.loadErrMsg))
                    CustomMessageBox.ShowDialog($"{FileProcessManager.Instance.loadErrMsg}","Load ini Fail", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                
                mainWindow.Show();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
           
        }
        else
        {
            // 3.close when wrong
            this.Shutdown();
            MessageBox.Show("Shuffler APP Close.");
        }
    }
    // `App.xaml.cs`
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            DataCenter.AppExiting = true;
            ShufflerManager.Instance.Dispose(); // 假設已實作
        }
        catch(Exception ex)
        {
            // Log critical shutdown errors
            Log4netManager.Logger.Fatal($"CRITICAL: Error during application shutdown: {ex}");
            Console.WriteLine($"CRITICAL SHUTDOWN ERROR: {ex.Message}");

            // Still try to log the exception even if logging fails
            try
            {
                System.IO.File.AppendAllText(
                    "shutdown_errors.log",
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {ex}\n\n"
                );
            }
            catch
            {
                // Last resort - at least show in debug output
                System.Diagnostics.Debug.WriteLine($"FATAL SHUTDOWN ERROR: {ex}");
            }
        } finally
        {
            base.OnExit(e);
        }
    }
    
    private bool CheckForUpdate()
    {
        try
        {
            string nasPath = @"C:\ShufflerSystem\ShufflerWPF.exe"; // NAS 上的路徑
            string localPath = Process.GetCurrentProcess().MainModule.FileName;

            if (!File.Exists(nasPath)) return false;

            // 比較版本號
            Version nasVersion = Version.Parse(FileVersionInfo.GetVersionInfo(nasPath).FileVersion);
            Version localVersion = Version.Parse(FileVersionInfo.GetVersionInfo(localPath).FileVersion);

            if (nasVersion > localVersion)
            {
                if (MessageBox.Show($"發現新版本 {nasVersion}，是否更新？", "自動更新", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    RunUpdateScript(Path.GetDirectoryName(nasPath), Path.GetDirectoryName(localPath));
                    Application.Current.Shutdown();
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            // 紀錄 log 或忽略更新繼續執行
            Console.WriteLine("更新檢查失敗: " + ex.Message);
        }
        return false;
    }
    
    private void RunUpdateScript(string nasFolder, string localFolder)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), "update_shuffler.bat");

        // 建立 Batch 內容
        // taskkill 確保程式已關閉，timeout 等待兩秒，xcopy 強制覆蓋
        string scriptContent = $@"
                @echo off
                timeout /t 2 /nobreak > nul

                rem 將 NAS 整個資料夾複製到本機 (含子資料夾、強制覆蓋)
                xcopy ""{nasFolder}\*.*"" ""{localFolder}\"" /Y /E /C /R /I

                rem 啟動程式
                start """" ""{Path.Combine(localFolder, "ShufflerWPF.exe")}""

                rem 刪除自己
                del ""%~f0""
                ";
                        
        // del ""%~f0"" :z刪掉這個批次檔本身
        // /Y：複製時 自動覆蓋 目標檔案，不再詢問 Overwrite (Yes/No)?
        // /E：包含所有子資料夾，就算是空資料夾也一起複製。
        
        File.WriteAllText(scriptPath, scriptContent, System.Text.Encoding.Default);

        // 執行 Batch
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(startInfo);
    }


}