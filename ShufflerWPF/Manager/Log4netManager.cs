using System;
using System.Collections.Concurrent;
using System.IO;
using log4net;
using log4net.Config;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

// 您可以將這個類別放在您專案的 Manager 或 SingleTon 資料夾中
namespace ShufflerWPF.Manager 
{
    // public enum LogLevel
    // {
    //     INFO,
    //     WARN,
    //     ERROR,
    //     FATAL
    // }

    /// <summary>
    /// 一個高效能、執行緒安全的非同步日誌管理器 Singleton。
    /// </summary>
    public sealed class Log4netManager
    {
        
        // 1. 提供一個全域的 ILog 實例，讓程式其他地方使用
        public static readonly ILog Logger = LogManager.GetLogger(typeof(Log4netManager));

        // 2. 提供一個方法，在應用程式啟動時呼叫一次，以載入設定
        public static void Configure()
        {
            // 尋找並載入 log4net.config 檔案
            var runId = DateTime.Now.ToString("yyyyMMdd-HHmmss"); log4net.GlobalContext.Properties["RunId"] = runId; Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetLog"));
            var log4netConfig = new FileInfo("log4net.config");
            XmlConfigurator.Configure(log4netConfig);

            
            // 讀取程式名稱與版本
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            
            // 程式名稱
            string appName =
                asm.GetCustomAttribute<AssemblyTitleAttribute>()?.Title
                ?? asm.GetName().Name
                ?? "UnknownApp";

            // 版本號 (檔案版本)
            string appVersion =
                asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                ?? asm.GetName().Version?.ToString()
                ?? "0.0.0.0";
            
            Logger.Info("==================================================");
            Logger.Info("Log4net configured successfully. Application starting...");
            Logger.Info($"Application: {appName}  Version: {appVersion}  RunId: {runId}");
            Logger.Info("==================================================");
        }
        
        // // Lazy
        // private static readonly Lazy<LogManager> _instance = new Lazy<LogManager>(() => new LogManager());
        // public static LogManager Instance => _instance.Value;
        //
        // // BlockingCollection
        // private readonly BlockingCollection<string> _logQueue;
        // private readonly Task _consumerTask;
        // private readonly StreamWriter _streamWriter;
        // private readonly string _logDirectory;
        //
        // /// <summary>
        // /// create Log file
        // /// </summary>
        // private LogManager()
        // {
        //     _logQueue = new BlockingCollection<string>();
        //
        //     try
        //     {
        //         // 設定日誌檔案路徑
        //         string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        //         _logDirectory = Path.Combine(baseDirectory, "Logs");
        //         Directory.CreateDirectory(_logDirectory); // 確保資料夾存在
        //
        //         string filePath = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");
        //
        //         // 使用 StreamWriter 來寫入檔案，設定為 Append 模式並啟用 AutoFlush。
        //         _streamWriter = new StreamWriter(filePath, append: true, Encoding.UTF8)
        //         {
        //             AutoFlush = true
        //         };
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"[FATAL] LogManager failed to initialize file writer: {ex.Message}");
        //         // 如果檔案寫入器初始化失敗，可以選擇拋出例外或降級為僅 Console 輸出。
        //         _streamWriter = null;
        //     }
        //
        //
        //     // 3. 啟動一個長駐的背景任務（消費者），專門負責從佇列中取出日誌並寫入目標。
        //     _consumerTask = Task.Run(() => ProcessLogQueue());
        // }
        //
        // /// <summary>
        // /// polling BlockingCollection
        // /// </summary>
        // private void ProcessLogQueue()
        // {
        //     // BlockingCollection wait until。
        //     foreach (string message in _logQueue.GetConsumingEnumerable())
        //     {
        //         try
        //         {
        //             //show console log
        //             Console.WriteLine(message);
        //
        //             // Write to file
        //             _streamWriter?.WriteLine(message);
        //         }
        //         catch (Exception ex)
        //         {
        //             Console.WriteLine($"[ERROR] Failed to write log: {ex.Message}");
        //         }
        //     }
        // }
        //
        // /// <summary>
        // /// all API for log
        // /// </summary>
        // public void Info(string message) => Log(LogLevel.INFO, message);
        // public void Warn(string message) => Log(LogLevel.WARN, message);
        // public void Error(string message, Exception? ex = null) => Log(LogLevel.ERROR, message, ex);
        // public void Fatal(string message, Exception? ex = null) => Log(LogLevel.FATAL, message, ex);
        //
        // /// <summary>
        // /// add to queue
        // /// </summary>
        // private void Log(LogLevel level, string message, Exception? ex = null)
        // {
        //     // Add queue last round ok
        //     if (_logQueue.IsAddingCompleted) return;
        //
        //     var stringBuilder = new StringBuilder();
        //     stringBuilder.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]");
        //     stringBuilder.Append($"[{level,-5}]"); // -5 表示左對齊，總寬度 5
        //     stringBuilder.Append($" - {message}");
        //     
        //     // add exception info
        //     if (ex != null)
        //     {
        //         stringBuilder.AppendLine();
        //         stringBuilder.Append($"   Exception: {ex.Message}");
        //         stringBuilder.AppendLine();
        //         stringBuilder.Append($"   StackTrace: {ex.StackTrace}");
        //     }
        //
        //     _logQueue.Add(stringBuilder.ToString());
        // }
        //
        // /// <summary>
        // /// 實作 IDisposable，確保在應用程式關閉時能優雅地處理剩餘的日誌。
        // /// </summary>
        // public void Dispose()
        // {
        //     // no more log to add
        //     _logQueue.CompleteAdding();
        //
        //     try
        //     {
        //         // wait all add log task finish
        //         _consumerTask.Wait(TimeSpan.FromSeconds(5));
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"[ERROR] Exception during LogManager shutdown: {ex.Message}");
        //     }
        //     
        //     _streamWriter?.Close();
        //     _streamWriter?.Dispose();
        //     _logQueue.Dispose();
        // }
    }
}