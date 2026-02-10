using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Manager
{
    /// <summary>
    /// 專門用來背景排程傳送錯誤訊息到 Telegram，避免在 UI 執行緒阻塞或多執行緒競爭。
    /// 使用單一 Channel + 單一消費工作者確保順序且可節流。
    /// </summary>
    public static class TelegramErrorNotifier
    {
        // 無界 Channel，寫入不會阻塞；如需限制可改成 CreateBounded。
        private static readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // 去重記錄（短時間相同訊息不重複送出）
        private static readonly ConcurrentDictionary<string, DateTime> _recent = new();
        private static readonly TimeSpan _dedupWindow = TimeSpan.FromSeconds(5);
        private static readonly SemaphoreSlim _startOnce = new(1,1);
        private static volatile bool _started = false;

        // 節流：兩則訊息間最小間隔
        private static readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(250);
        private static DateTime _lastSent = DateTime.MinValue;

        /// <summary>
        /// 佇列訊息。若在短時間內重複同樣的 Key（訊息全文）則忽略。
        /// </summary>
        public static async Task EnqueueAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            
            try
            {
                await TryStartWorkerAsync();
            }
            catch (Exception ex)
            {
                Log4netManager.Logger.Error("Worker start failed", ex);
            }

            // var now = DateTime.UtcNow;
            // // 去重：同一內容 5 秒內只發一次
            // var last = _recent.GetOrAdd(message, _ => now);
            // if (now - last < _dedupWindow)
            // {
            //     return; // 忽略重複
            // }
            // _recent[message] = now;

            // 寫入 channel（失敗代表完成/取消，不再處理）
            _channel.Writer.TryWrite(message);
        }

        private static async Task TryStartWorkerAsync()
        {
            if (_started) return;
            
            // 如果有第二個線程琬一點點進來，會被卡在這，直到這個被釋放
            // 但是二個線程被釋放進來後會被_started返回
            // 所以這邊是保證只會啟動一次WorkerLoop
            await _startOnce.WaitAsync();
            try
            {
                if (_started) return;
                _started = true;
                _ = Task.Run(WorkerLoop); // 背景啟動
                // _ = Task.Run(async()=>
                // {
                //     await WorkerLoop();
                // });
            }
            finally
            {
                _startOnce.Release();
            }
        }

        private static async Task WorkerLoop()
        {
            Log4netManager.Logger.Info("TelegramErrorNotifier worker started.");
            try
            {
                if (DataCenter.AppExiting) return;
                
                await foreach (var msg in _channel.Reader.ReadAllAsync())
                {
                    try
                    {
                        // 250ms wait interval
                        var now = DateTime.UtcNow;
                        var diff = now - _lastSent;
                        if (diff < _minInterval)
                        {
                            await Task.Delay(_minInterval - diff);
                        }

                        if (DataCenter.MyShufflerSettings.LiveMode == "false")
                        {
                            await TelegramBotTypeManager.Pd2GroupGb.SendMessageGbAsync(msg);
                        }
                        else if (DataCenter.MyShufflerSettings.DoMainNumber == 2)
                        {
                            await TelegramBotTypeManager.MexicoSupervisorGroup.SendMessageGbAsync(msg);
                        }
                        else if(DataCenter.MyShufflerSettings.DoMainNumber == 3)
                        {
                            await TelegramBotTypeManager.CebuSupervisorGroup.SendMessageGbAsync(msg);
                        }
                        
                        _lastSent = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        Log4netManager.Logger.Error($"TelegramErrorNotifier send failure: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log4netManager.Logger.Error($"TelegramErrorNotifier worker crashed: {ex.Message}");
            }
            finally
            {
                _started = false; // 允許重啟（理論上不會）
                Log4netManager.Logger.Info("TelegramErrorNotifier worker stopped.");
            }
        }
    }
}
