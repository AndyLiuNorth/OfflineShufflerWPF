using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ShufflerWPF.Model;
using ShufflerWPF.Pages;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Manager
{
    /// <summary>
    /// 封裝單一洗牌任務的狀態和邏輯
    /// </summary>
    public class ShufflingJob
    {
        public string JobId { get; } = Guid.NewGuid().ToString();
        public string ShufflerId { get; }
        public string TrueBoxId { get; }
        public string PrintName { get; }
        public string StatusDescription { get; set; }
        public bool IsFinished { get; set; } = false;

        public ShufflingJob(string shufflerId, string trueBoxId, string printName)
        {
            ShufflerId = shufflerId;
            TrueBoxId = trueBoxId;
            PrintName = printName;
            StatusDescription = $"Job for {ShufflerId} created.";
        }

        /// <summary>
        /// 執行洗牌任務的核心邏輯
        /// </summary>
        public async Task Run()
        {
            try
            {
                // 確保連線
                if (!ShufflerProvider.Instance.Current.ConnectStatus)
                {
                    await ShufflerProvider.Instance.Current.ConnectShuffler().ConfigureAwait(false);
                }

                // 找到對應的 ShufflerStatus 物件以監控狀態
                var shufflerStatus = ShufflerProvider.Instance.Current.GetShufflerStatusList().FirstOrDefault(s => s.ShufflerId == this.ShufflerId);
                if (shufflerStatus == null)
                {
                    throw new InvalidOperationException($"Could not find ShufflerStatus for {this.ShufflerId}");
                }
                
                // wait
                await Task.Delay(500);
                shufflerStatus.LastError = string.Empty;
                
                // 假設是Error狀態，先不Reset，直接報錯誤給現場處理
                if (shufflerStatus.LastStatus == ShufflingStatus.ERROR || shufflerStatus.LastStatus == ShufflingStatus.DISCONNECTED)
                {
                    // await ShufflerManager.Instance.StopShuffle(this.ShufflerId);
                    // await Task.Delay(2000);
                    // if (!ShufflerManager.Instance.ConnectStatus)
                    // {
                    //     await ShufflerManager.Instance.ConnectShuffler();
                    // }
                    
                    Log4netManager.Logger.Error($">>> DoshuffleTask: Task Finish Because Shuffler[{this.ShufflerId}] is ShufflingStatus.ERROR [{shufflerStatus.LastError}].TrueID:{this.TrueBoxId} ");
                    throw new InvalidOperationException($" DoshuffleTask: Task Finish Because Shuffler[{this.ShufflerId}] is ShufflingStatus.ERROR [{shufflerStatus.LastError}].TrueID:{this.TrueBoxId} ");
                    
                }

                // 步驟 1 & 2: 解鎖並開始洗牌
                //StatusDescription = "locking and starting shuffle...";
                await ShufflerProvider.Instance.Current.LockTouch(this.ShufflerId).ConfigureAwait(false);
                await ShufflerProvider.Instance.Current.DoShuffle(this.ShufflerId).ConfigureAwait(false);;
                //Console.WriteLine(">>> DoshuffleTask: Start DoShuffle");
                Log4netManager.Logger.Info($"Start Auto shuffle Task: ShufflerID:{this.ShufflerId} TrueBoxID:{this.TrueBoxId} PrintName:{this.PrintName} ");
 
                // 步驟 3: 等待進入 SHUFFLING 狀態並執行 Action 2
                StatusDescription = "Waiting for SHUFFLING state...";
                int pollingTimeout = 50000;
                int totalWait = 0;
                bool action2Done = false;
                bool timedOut = false;
                string errormes = string.Empty;
                int heartbeatingStop = 0;
                int cardCoundRecord = 0;
                //var splashbox = CustomMessageBox.Show("Waiting for Shuffler Start...", "Please wait...", CustomMessageBoxButtonType.NoneBtn, CustomMessageBoxIcon.Information);
                shufflerStatus.LastStatus = ShufflingStatus.WAITTING;
                while (true)
                {
                    if (totalWait >= pollingTimeout)
                    {
                        timedOut = true;
                        break;
                    }
                    if (DataCenter.AppExiting) throw new OperationCanceledException();
                    
                    if (shufflerStatus.LastStatus == ShufflingStatus.SHUFFLING)
                    {
                        //errormes = await DoTrueBoxActionManager.DoTrueBoxActionSecondAsync(this.TrueBoxId,this.ShufflerId);
                        errormes = await DoTrueBoxActionManager.DoTrueBoxActionFirstToSixAsync(this.TrueBoxId, DoTrueBoxActionManager.TrueBoxAction.AutoShuffling,this.ShufflerId).ConfigureAwait(false);
                        if (string.IsNullOrEmpty(errormes))
                        {
                            action2Done = true;
                            break;
                        }
                        //splashbox.Close();
                        
                        // Action 2 Error Stop Shuffle
                        await ShufflerManager.Instance.StopShuffle(this.ShufflerId).ConfigureAwait(false);
                        
                        throw new Exception($"Action 2 failed: {errormes}");
                    }
                    if (shufflerStatus.LastStatus == ShufflingStatus.ERROR)
                    {
                        throw new Exception($"Shuffler Machine[{this.ShufflerId}] is fail status: [{shufflerStatus.LastStatus }]");
                    }
                    await Task.Delay(500);
                    totalWait += 500;
                }
                
                //splashbox.Close();
                if(timedOut)
                {
                    shufflerStatus.LastStatus = ShufflingStatus.ERROR;
                    throw new Exception("Timeout: Shuffler did not enter SHUFFLING state.");
                }
                // if (!action2Done)
                // {
                //     throw new Exception("Timeout: Shuffler did not enter SHUFFLING state.");
                // }
                
                //步驟 3: Action 2 Success! Hide page and show Start Messagebox
                ShuffleJobManager.Instance.RaiseJobSucceeded(this);

                // 步驟 4: 等待洗牌完成
                Log4netManager.Logger.Debug($">> DoshuffleTask: Status LastError: [{shufflerStatus.LastError}].TrueID:{this.TrueBoxId} ");
                Log4netManager.Logger.Debug($">> DoshuffleTask: Status IsShuffleCompleted: [{shufflerStatus.IsShuffleCompleted}].TrueID:{this.TrueBoxId} ");

                StatusDescription = "Shuffling in progress...";
                while (!shufflerStatus.IsShuffleCompleted)
                {
                    if (DataCenter.AppExiting) throw new OperationCanceledException();
                    
                    Console.WriteLine($">>> DoshuffleTask: Count:{ shufflerStatus.CardCount}");
                    Log4netManager.Logger.Debug($">>> DoshuffleTask: Count:{shufflerStatus.CardCount}>> LastStatus: [{shufflerStatus.LastStatus}] | LastError: [{shufflerStatus.LastError}] | IsShuffleCompleted: [{shufflerStatus.IsShuffleCompleted}]TrueID:{this.TrueBoxId} ");

                    //Reset HEARTBEATING status to avoid infinite loop
                    //shufflerStatus.LastStatus = ShufflingStatus.HEARTBEATING;
                    
                    // Wait if connect lost
                    if (!ShufflerProvider.Instance.Current.ConnectStatus)
                    {
                        int shufflingJobTimeout = 120;
                        int count = 0;
                        
                        // check and wait for reconnect
                        while (!ShufflerProvider.Instance.Current.ConnectStatus && count < shufflingJobTimeout)
                        {
                            Console.WriteLine($">>> DoshuffleTask: lost connect. wait Count:{ count }");
                            Log4netManager.Logger.Error($">>> DoshuffleTask: lost connect. wait Count:{ count }");
                            await Task.Delay(500);
                            count ++;
                        }

                        // Still not connected, throw error
                        if (!ShufflerProvider.Instance.Current.ConnectStatus)
                        {
                            Log4netManager.Logger.Error($">>> DoshuffleTask: Breaking out because lost connect. TrueID:{this.TrueBoxId} ");
                            throw new Exception("Shuffler server disconnected.");//do RaiseJobFailed
                        }
                    }
                    
                    if (shufflerStatus.LastStatus == ShufflingStatus.READ_WRITE)
                    {
                        Log4netManager.Logger.Info($">>> DoshuffleTask: Machine IP:[{this.ShufflerId}].Statue READ_WRITE. Continue.. TrueID:{this.TrueBoxId} ");
                        continue;
                    }
                    if (shufflerStatus.LastStatus != ShufflingStatus.SHUFFLING && shufflerStatus.LastStatus != ShufflingStatus.READY)
                    {
                        throw new Exception($"Machine IP:[{this.ShufflerId}]. Shuffle interrupted with status: {shufflerStatus.LastStatus} \r\n Error: {shufflerStatus.LastError}");
                    }
                    if (shufflerStatus.LastStatus == ShufflingStatus.READY)
                    {
                        //Console.WriteLine(">>> DoshuffleTask: Breaking out of waiting loop as Shuffler is READY.");
                        Log4netManager.Logger.Info($">>> DoshuffleTask: Machine IP:[{this.ShufflerId}]. Breaking out of waiting loop as Shuffler is READY.TrueID:{this.TrueBoxId} ");
                        break;
                    }
                    if(shufflerStatus.LastStatus == ShufflingStatus.ERROR)
                    {
                        Log4netManager.Logger.Error($">>> DoshuffleTask: Machine IP:[{this.ShufflerId}].Breaking out of waiting loop as Shuffler is ERROR[{shufflerStatus.LastError}].TrueID:{this.TrueBoxId}.");
                        throw new Exception($"Shuffle interrupted with status: {shufflerStatus.LastStatus} \r\n Error: {shufflerStatus.LastError}");;
                    }
                    await Task.Delay(500);
                    if(shufflerStatus.CardCount == cardCoundRecord)
                    {
                        heartbeatingStop++;
                    }
                    else
                    {
                        heartbeatingStop = 0;
                    }
                    
                    if(heartbeatingStop>=100)
                    {
                        shufflerStatus.LastStatus = ShufflingStatus.ERROR;
                        Log4netManager.Logger.Error($">>> DoshuffleTask: Machine IP:[{this.ShufflerId}].Breaking out of waiting loop as Shuffler no response 5 minutes.TrueID:{this.TrueBoxId}.");
                        throw new Exception($"Shuffle interrupted : No response from shuffler for 5 minutes.\r\n Last Status: {shufflerStatus.LastStatus} \r\n Last Error: {shufflerStatus.LastError}");
                    }
                    
                    cardCoundRecord = shufflerStatus.CardCount;

                }

                Log4netManager.Logger.Debug($">>>> DoshuffleTask: Machine IP:[{this.ShufflerId}] Final Status LastStatus: [{shufflerStatus.LastStatus}].TrueID:{this.TrueBoxId} ");
                Log4netManager.Logger.Debug($">>>> DoshuffleTask: Machine IP:[{this.ShufflerId}] Final Status LastError: [{shufflerStatus.LastError}].TrueID:{this.TrueBoxId} ");
                Log4netManager.Logger.Debug($">>>> DoshuffleTask: Machine IP:[{this.ShufflerId}] Final Status IsShuffleCompleted: [{shufflerStatus.IsShuffleCompleted}].TrueID:{this.TrueBoxId} ");
                
                // 步驟 5: 完成並解鎖
                await ShufflerProvider.Instance.Current.UnLockTouch(this.ShufflerId).ConfigureAwait(false);
                Log4netManager.Logger.Info($"Start UnLockTouch: Machine IP:[{this.ShufflerId}]. ShufflerID:{this.ShufflerId} TrueBoxID:{this.TrueBoxId} PrintName:{this.PrintName} ");
                
                // 步驟 6: 檢查是否跳出為異常
                if (shufflerStatus.CardCount != 416)
                {
                    Log4netManager.Logger.Error($"Action Shuffling Fail CardCount Count: {shufflerStatus.CardCount}");
                    Log4netManager.Logger.Error($"Action Shuffling Fail CardCount ErrorCode: {shufflerStatus.LastError}");

                    throw new Exception($"Auto shuffling stopped!\r\nMachine IP:[{this.ShufflerId}]\r\nBox ID:[{this.TrueBoxId}]\r\n(Error: Stopped at the card No. {shufflerStatus.CardCount})");

                }
                if (shufflerStatus.LastError != string.Empty)
                {
                    throw new Exception($"Action Shuffling Fail: {shufflerStatus.LastError}");
                }
                if (shufflerStatus.LastStatus == ShufflingStatus.ERROR)
                {
                    throw new Exception($"Action Shuffling Fail: {shufflerStatus.LastError}");
                }
                
                // 步驟 7: 執行 Action 3
                StatusDescription = "Performing final action...";
                //string action3Error = await DoTrueBoxActionManager.DoTrueBoxActionThirdAsync(this.TrueBoxId);
                string action3Error  = await DoTrueBoxActionManager.DoTrueBoxActionFirstToSixAsync(this.TrueBoxId, DoTrueBoxActionManager.TrueBoxAction.AutoFinished).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(action3Error))
                {
                    throw new Exception($"Action 3 failed: {action3Error}");
                }
                StatusDescription = "Shuffle Finished Successfully.";
                
                // Call UI to show success message box
                ShuffleJobManager.Instance.RaiseJobCompleted(new ShuffleJobManager.JobCompletedItem()
                {
                    ShufflerId = this.ShufflerId, 
                    PrintName = this.PrintName
                });
                // Application.Current.Dispatcher.Invoke(() =>
                // {
                //     CustomMessageBox.ShowDialog($"🔹Shuffler IP:{this.ShufflerId}\r\n🔹Box ID:{this.PrintName}\r\nShuffling finished.", "Action 3 Pass",
                //         CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
                // });
                
                Log4netManager.Logger.Info($">>>Machine IP:[{this.ShufflerId}]. DoshuffleTask:Shuffle Finished Successfully. CustomMessageBox End.PrintName:{this.PrintName} ");
            }
            catch (Exception ex)
            {
                StatusDescription = $"Failed: {ex.Message}";
                Log4netManager.Logger.Info($"[ShuffleJob Error] Job for {ShufflerId} failed: {ex.Message}");
                // Application.Current.Dispatcher.Invoke(() =>
                // {
                //     CustomMessageBox.ShowDialog($"{this.ShufflerId}\r\n{this.PrintName}\r\nAn error occurred.\r\nError: {ex.Message}", "Action Error",
                //         CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                // });
                
                // RaiseJobFailed Show Error MessageBox
                ShuffleJobManager.Instance.RaiseJobFailed(this, ex);
                
            }
            finally
            {
                IsFinished = true;
            }
        }
        
        
        // /// <summary>
        // /// 單獨運行洗牌
        // /// </summary>
        // public async Task RunShufflerOnly()
        // {
        //     try
        //     {
        //         // 確保連線
        //         if (!ShufflerManager.Instance.ConnectStatus)
        //         {
        //             await ShufflerManager.Instance.ConnectShuffler();
        //         }
        //         // 找到對應的 ShufflerStatus 物件以監控狀態
        //         var shufflerStatus = ShufflerManager.Instance.GetShufflerStatusList().FirstOrDefault(s => s.ShufflerId == this.ShufflerId);
        //         if (shufflerStatus == null)
        //         {
        //             throw new InvalidOperationException($"Could not find ShufflerStatus for {this.ShufflerId}");
        //         }
        //         await ShufflerManager.Instance.UnLockTouch(this.ShufflerId);
        //         await ShufflerManager.Instance.DoShuffle(this.ShufflerId);
        //         StatusDescription = "Shuffling in progress...";
        //         while (!shufflerStatus.IsShuffleCompleted)
        //         {
        //             if (!ShufflerManager.Instance.ConnectStatus) throw new Exception("Shuffler server disconnected.");
        //             if (shufflerStatus.LastStatus != ShufflingStatus.SHUFFLING && shufflerStatus.LastStatus != ShufflingStatus.READY)
        //             {
        //                 throw new Exception($"Shuffle interrupted with status: {shufflerStatus.LastStatus}");
        //             }
        //             if (shufflerStatus.LastStatus == ShufflingStatus.READY)
        //             {
        //                 //Console.WriteLine(">>> DoshuffleTask: Breaking out of waiting loop as Shuffler is READY.");
        //                 Log4netManager.Logger.Info($">>> DemoFinishedshuffleTask: Breaking out of waiting loop as Shuffler is READY.TrueID");
        //                 break;
        //             }
        //             await Task.Delay(500);
        //         }
        //
        //         // 成功完成
        //         StatusDescription = "Shuffle Finished Successfully.";
        //         Application.Current.Dispatcher.Invoke(() =>
        //         {
        //             CustomMessageBox.ShowDialog($"Shuffle Demo Finished", "Demo",
        //                 CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
        //         });
        //         ShuffleJobManager.Instance.RaiseJobSucceeded(this);
        //         Log4netManager.Logger.Info($">>> DemoFinishedshuffleTask:Shuffle DemoFinished Successfully. ");
        //     }
        //     catch (Exception ex)
        //     {
        //         StatusDescription = $"Failed: {ex.Message}";
        //         Log4netManager.Logger.Error($"[ShuffleJob Error] Job for {ShufflerId} failed: {ex.Message}");
        //         Application.Current.Dispatcher.Invoke(() =>
        //         {
        //             CustomMessageBox.ShowDialog($"{this.ShufflerId}\r\n{this.PrintName}\r\nAn error occurred.\r\nError: {ex.Message}", "Action Error",
        //                 CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
        //         });
        //         ShuffleJobManager.Instance.RaiseJobFailed(this, ex);
        //     }
        //     finally
        //     {
        //         IsFinished = true;
        //     }
        // }
    }

    /// <summary>
    /// 管理所有洗牌任務的 Singleton
    /// </summary>
    public class ShuffleJobManager
    {
        private static readonly Lazy<ShuffleJobManager> _instance = new Lazy<ShuffleJobManager>(() => new ShuffleJobManager());
        
        
        // 表達式主體屬性」(Expression-bodied Property)。它其實是一個唯讀 (get-only) 屬性的簡寫形式
        public static ShuffleJobManager Instance => _instance.Value;
        // get
        // {
        //     return _instance.Value;
        // }
       
        private readonly ConcurrentDictionary<string, ShufflingJob> _activeJobs = new ConcurrentDictionary<string, ShufflingJob>();

        private ShuffleJobManager() { }

        public void StartNewShuffleJob(string shufflerId, string? trueBoxId=null, string? printName=null)
        {
            if (DataCenter.AppExiting) return;
            
            if (trueBoxId == null && printName == null)// Do shuffle Demo
            {
                // var newJob = new ShufflingJob(shufflerId, "demo", "demo");
                // if (_activeJobs.TryAdd(newJob.JobId, newJob))
                // {
                //     // 使用 Task.Run 啟動任務，但不等待它
                //     _ = Task.Run(async () =>
                //     {
                //         await newJob.RunShufflerOnly();
                //         // 任務完成後，可以從列表中移除
                //         _activeJobs.TryRemove(newJob.JobId, out _);
                //     });
                // }
            }
            else// Do shuffle action
            {
                var newJob = new ShufflingJob(shufflerId, trueBoxId, printName);
                if (_activeJobs.TryAdd(newJob.JobId, newJob))
                {
                    // 使用 Task.Run 啟動任務，但不等待它
                    _ = Task.Run(async () =>
                    {
                        await newJob.Run();
                        // 任務完成後，可以從列表中移除
                        _activeJobs.TryRemove(newJob.JobId, out _);
                    });
                }
            }
            
        }

        public event Action<ShufflingJob, Exception>? JobFailed;
        public event Action<ShufflingJob>? JobSucceeded;
        public event Action<JobCompletedItem>? Jobcompleted;
        internal void RaiseJobFailed(ShufflingJob job, Exception ex) => JobFailed?.Invoke(job, ex);
        internal void RaiseJobSucceeded(ShufflingJob job) => JobSucceeded?.Invoke(job);
        
        internal void RaiseJobCompleted(JobCompletedItem e)
        {
            Jobcompleted?.Invoke(new JobCompletedItem
            {
                ShufflerId = e.ShufflerId,
                PrintName = e.PrintName
            });
        }
        
        public class JobCompletedItem
        {
            public string ShufflerId { get; set; }
            public string PrintName { get; set; }
        }

    }
}
