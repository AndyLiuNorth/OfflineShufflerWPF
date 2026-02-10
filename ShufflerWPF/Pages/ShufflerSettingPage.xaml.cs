using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using ShufflerWPF.Manager;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;
using System.Windows.Threading;
namespace ShufflerWPF.Pages;

public partial class ShufflerSettingPage : Window
{
    private readonly BlockingCollection<Func<Task>> qcollection = new BlockingCollection<Func<Task>>();
    private CancellationTokenSource? _cts = new CancellationTokenSource();
    public ShufflerSettingPage(ShufflerAutoPageViewModel vm)
    {
        InitializeComponent();
        try
        {
            this.Closing += Window_Closing;
            // Start Connect Shuffler and Run Task Receive
            // Machine has finished Connect in MainWindow 
            //ShufflerManager.Instance.ConnectShuffler();
            // Update Shuffler List
            ShufflerManager.Instance.ShufflerStatusChangeAction += OnShufflerStatusListChanged;
            //test get
            //ShufflerStatusList = ShufflerManager.Instance.GetShufflerStatusList();
            IsVisibleChanged += OnIsVisibleChanged;
            //this.Loaded += ShufflerSettingPage_Show;
        }
        catch (Exception e)
        {
            this.DialogResult = false;
            Console.WriteLine(e);
            throw;
        }

        DataContext = vm;
        
    }
    
    private void OnIsVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        // Use BlockingCollection<Func<Task>> because we want to demo the BlockingCollection with Func<>
        if (IsVisible)// show
        {
            
            _cts = new CancellationTokenSource();
            
            Func<Task> func = PollingShufflerStatus;
            qcollection.Add(func);
            _= Task.Run(async ()=>
            {
                foreach (var taskFunc in qcollection.GetConsumingEnumerable(_cts.Token))
                {
                    await taskFunc();
                }
            },_cts.Token);
        }
        else// hide
        {
            
            // break GetConsumingEnumerable CompleteAdding will distroy the qcollection
            // qcollection.CompleteAdding();
        
            // Cancel the polling task
            _cts?.Cancel();
            
            _cts?.Dispose();
            _cts = null;
        }
    }
    
    
    private async Task PollingShufflerStatus()
    {
        while (true)
        {
            try
            {
                if (DataCenter.AppExiting) break;
                
                if (_cts?.IsCancellationRequested == true)
                {
                    Log4netManager.Logger.Debug("PollingShufflerStatus _cts Cancel");
                    break;
                }
                
                if (DataCenter.ShufflerLoseConnect)
                {
                    Log4netManager.Logger.Debug("ShufflerLoseConnect");
                    // await Dispatcher.InvokeAsync(() =>
                    // {
                    //     CustomMessageBox.Show(
                    //         "Shuffler Disconnected! Please Check Connection!",
                    //         "Shuffler Connection",
                    //         CustomMessageBoxButtonType.Ok,
                    //         CustomMessageBoxIcon.Error);
                    // }); 
                    await Dispatcher.InvokeAsync(() =>
                        {
                            this.Hide();
                        }); 
                    
                    break;
                }
                await Task.Delay(2000, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 被取消時離開
            }
        }
    }
    
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // break GetConsumingEnumerable
        qcollection.CompleteAdding();
        
        // Cancel the polling task
        if(_cts!=null)
            _cts.Cancel();
        
        // ShufflerManager.Instance.CloseShuffler();
        // Window is cosing????
        
        if (Application.Current.MainWindow != null && !DataCenter.AppExiting)
        {
            Application.Current.MainWindow.Show();
        }
        // -unregist.But actually we don't need this. Because this page only new once.
        ShufflerManager.Instance.ShufflerStatusChangeAction -= OnShufflerStatusListChanged;
    }
    

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        
        this.Hide();
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Show();
        }
        
    }

    private async void DoShuffleButton_Click(object sender, RoutedEventArgs e)
    {
        
        if (DataCenter.ShufflerLoseConnect)
        {
                        
        }
        
        
        // - 直接在 UI thread 設定狀態，因為這個事件是 UI thread 觸發的
        //DoShuffleButton.IsEnabled = false;
        ShufflerListBox.IsEnabled = false;
        //_thisroundresult = true;

        // 0. 確保連線
        if (!ShufflerManager.Instance.ConnectStatus)
        {
            await ShufflerManager.Instance.ConnectShuffler();
        }

        if (DataContext is ShufflerAutoPageViewModel vm)
        {
            // 1. 檢查是否選擇了 Shuffler
            if (vm.SelectedShufflerStatus == null)
            {
                CustomMessageBox.ShowDialog("Please Select a Shuffler!", "Shuffling",
                    CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                return;
            }

            try
            {
                // 2. 禁用UI，防止重複點擊
                //DoShuffleButton.IsEnabled = false;
                ShufflerListBox.IsEnabled = false;

                // 3. 從 ViewModel 取得必要的資料
                string shufflerId = vm.SelectedShufflerStatus.ShufflerId;

                // 4. 呼叫 Job Manager 啟動一個新的背景任務
                ShuffleJobManager.Instance.StartNewShuffleJob(shufflerId);

            }
            finally
            {
                // 5. 重新啟用UI控制項，以便下次顯示時可用
                //DoShuffleButton.IsEnabled = true;
                ShufflerListBox.IsEnabled = true;
            }
        }
    }
    
    private void CloseAndSetResult(bool? result)
    {
        // 檢查視窗是否仍然有效
        var win = Window.GetWindow(this);
        if (win != null && win.IsLoaded)
        {
            win.DialogResult = result;
            //win.Close();
        }
    }
    private async void StopShuffleButton_Click(object sender, RoutedEventArgs e)
    {
        // 這裡一直都是主執行緒 沒有await非同步，所以還在UI thread上，
        CustomMessageBoxResult Result  = CustomMessageBox.ShowDialog("Shuffling Stop!\r\n Please Confirm whether to terminate","Shuffling",CustomMessageBoxButtonType.YesNo,CustomMessageBoxIcon.Question);

        if (Result == CustomMessageBoxResult.Yes)
        {
            try
            {
                //_thisroundresult = false;
                if (DataContext is ShufflerAutoPageViewModel vm)
                {
                    // 遇到await 關鍵字會將 UI 主執行緒釋放出來這時才是非同步
                    await ShufflerManager.Instance.StopShuffle(vm.SelectedShufflerStatus.ShufflerId);
                }
            }
            catch (Exception exception)
            {
                this.DialogResult = false;
                Console.WriteLine(exception);
                throw;
            }
        
            //this.DialogResult = true;
        }
    }

    private void RefreshShuffleButton_Click(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private async void LockUnlockButton_Click(object sender, RoutedEventArgs e)
    {
        //DoShuffleButton.IsEnabled = false;
        ShufflerListBox.IsEnabled = false;
        if (DataContext is ShufflerAutoPageViewModel vm)
        {
            if (vm.SelectedShufflerStatus != null)
            {
                if (vm.SelectedShufflerStatus.IsTouchLocked)
                {
                    await ShufflerManager.Instance.UnLockTouch(vm.SelectedShufflerStatus.ShufflerId);
                    LockUnclokScreenButton.Content = "Lock Screen";
                }
                else
                {
                    await ShufflerManager.Instance.LockTouch(vm.SelectedShufflerStatus.ShufflerId);
                    LockUnclokScreenButton.Content = "UnLock Screen";
                }
            }
        }

        //DoShuffleButton.IsEnabled = true;
        ShufflerListBox.IsEnabled = true;
    }
    
    private void OnShufflerStatusListChanged(List<ShufflerStatus> latestList)
    {
        
        this.Dispatcher.Invoke(() =>
        {
            if (DataContext is ShufflerAutoPageViewModel vm)
            {
                //若有變化時更新全部Shuffler List
                var itemsToRemove = vm.ShufflerStatusList
                    .Where(uiItem => !latestList.Any(latestItem => latestItem.ShufflerId == uiItem.ShufflerId))
                    .ToList(); // ToList() 避免在遍歷時修改集合

                foreach (var item in itemsToRemove)
                {
                    vm.ShufflerStatusList.Remove(item);
                }

                var itemsToAdd = latestList
                    .Where(latestItem => !vm.ShufflerStatusList.Any(uiItem => uiItem.ShufflerId == latestItem.ShufflerId))
                    .ToList();

                foreach (var item in itemsToAdd)
                {
                    //update UI
                    vm.ShufflerStatusList.Add(item);
                }
            }
        });
    }

    private void DragMe(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch (Exception)
        {

            //throw;
        }
    }
}