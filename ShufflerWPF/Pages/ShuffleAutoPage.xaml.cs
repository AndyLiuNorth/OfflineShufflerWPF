using System.Windows;
using ShufflerWPF.Manager;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ShufflerWPF.Pages;

public partial class ShuffleAutoPage : Window
{
    //private bool _thisroundresult;
    private readonly TopStackUserControl _topStack;
    private ShuffleJobManager _jobManager;
    public ShuffleAutoPage(ShufflerAutoPageViewModel vm, TopStackUserControl topStack)
    {
        _topStack = topStack;
        InitializeComponent();
        
        _jobManager = ShuffleJobManager.Instance;
        
        try
        {
            this.Closing += Window_Closing;
            this.IsVisibleChanged += Window_IsVisibleChanged;
            
            
            //ShufflerManager.Instance.ShufflerStatusChangeAction += OnShufflerStatusListChanged;
            
            // 改註冊於Base底下的Action事件，由Base在獲得IP List的時候去invoke更新UI
            ShufflerProvider.Instance.Current.ShufflerStatusChangeAction += OnShufflerStatusListChanged;
            
            // 由外部Manager去管理Job的成功與失敗事件去執行UI動作
            _jobManager.JobFailed += OnJobFailed;
            _jobManager.JobSucceeded += OnJobSucceeded;
            _jobManager.Jobcompleted += OnJobCompleted;



        }
        catch (Exception e)
        {
            this.DialogResult = false;
            Console.WriteLine(e);
            throw;
        }
        DataContext = vm;
        
        _topStack.DataContext = new { userid = DataCenter.CurrentMember?.idui ?? "null" };
        TopStackHost.Content = _topStack;
    }
    
    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        bool isVisible = (bool)e.NewValue;
        this.Dispatcher.Invoke(() =>
        {
            if (isVisible)
            {
                if (Application.Current.MainWindow != null)
                {
                    DoShuffleButton.IsEnabled = false;
                    Application.Current.MainWindow.Hide();
                    _topStack.DataContext = new { userid = DataCenter.CurrentMember?.idui ?? "null" };
                }
            }
            else
            {
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Show();
                }
            }
        });
    }
    
    // -Update New Scan Truebox
    public void updateTruebox(ShufflerAutoPageViewModel vm)
    {
        DataContext = vm;
    }
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 若應用正在關閉 (Dispatcher/Shutdown) 或 MainWindow 為 null/正在卸載，不呼叫 Show()
        try
        {
            if (Application.Current?.Dispatcher?.HasShutdownStarted == false)
            {
                var main = Application.Current.MainWindow;
                if (main != null && main != this && main.IsLoaded && main.Visibility != Visibility.Visible)
                {
                    try { main.Show(); } catch { /* 忽略關閉期間顯示例外 */ }
                }
            }
        }
        catch { }
        finally
        {
            ShufflerManager.Instance.ShufflerStatusChangeAction -= OnShufflerStatusListChanged;
            ShuffleJobManager.Instance.JobFailed -= OnJobFailed;
            ShuffleJobManager.Instance.JobSucceeded -= OnJobSucceeded;
        }
    }
    
    // -Notice! Get shuffler List from this
    private void OnShufflerStatusListChanged(List<ShufflerStatus> latestList)
    {
        // 使用 BeginInvoke 非同步排入佇列，避免在登入對話框顯示時同步阻塞造成卡死
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsLoaded || this.Visibility != Visibility.Visible) {
                // 視窗已關閉或隱藏時不更新
                return;
            }
            if (DataContext is ShufflerAutoPageViewModel vm)
            {
                // 刪除不存在的
                var itemsToRemove = vm.ShufflerStatusList
                    .Where(uiItem => !latestList.Any(latestItem => latestItem.ShufflerId == uiItem.ShufflerId))
                    .ToList();
                foreach (var item in itemsToRemove)
                    vm.ShufflerStatusList.Remove(item);

                // 新增新的
                var itemsToAdd = latestList
                    .Where(latestItem => !vm.ShufflerStatusList.Any(uiItem => uiItem.ShufflerId == latestItem.ShufflerId))
                    .ToList();
                foreach (var item in itemsToAdd)
                    vm.ShufflerStatusList.Add(item);
            }
        }));
    }

    private async void DoShuffleButton_Click(object sender, RoutedEventArgs e)
    {
        DoShuffleButton.IsEnabled = false;
        ShufflerListBox.IsEnabled = false;
        //_thisroundresult = true;
        if (DataCenter.CurrentTrueBox.trueId == null)
        {
            CustomMessageBox.ShowDialog("CurrentTrueBox is null!", "Shuffling", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            DoShuffleButton.IsEnabled = true;
            ShufflerListBox.IsEnabled = true;
            return;
        }
        if (DataCenter.CurrentTrueBox.action==2)
        {
            CustomMessageBox.ShowDialog($"Please wait. \r\r This Box ID: [{DataCenter.CurrentTrueBox.trueId}] is being shuffled now.", "Shuffling", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            DoShuffleButton.IsEnabled = true;
            ShufflerListBox.IsEnabled = true;
            return;
        }
        // if (!ShufflerManager.Instance.ConnectStatus)
        // {
        //     await ShufflerManager.Instance.ConnectShuffler();
        // }

        if (!ShufflerProvider.Instance.Current.ConnectStatus)
        {
            await ShufflerProvider.Instance.Current.ConnectShuffler();
        }
        
        if (!(DataContext is ShufflerAutoPageViewModel vm)) return;
        if (vm.SelectedShufflerStatus == null)
        {
            CustomMessageBox.ShowDialog("Please Select a Shuffler!", "Shuffling", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            DoShuffleButton.IsEnabled = true;
            ShufflerListBox.IsEnabled = true;
            return;
        }

        if (vm.SelectedShufflerStatus.LastStatus == ShufflingStatus.SHUFFLING)
        {
            CustomMessageBox.ShowDialog("This Shuffler is shuffling!", "Shuffling",
                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            ShufflerListBox.IsEnabled = true;
            return;
        }

        try
        {
            string shufflerId = vm.SelectedShufflerStatus.ShufflerId;
            string trueBoxId = DataCenter.CurrentTrueBox.trueId;
            string printName = vm.OneBoxModel.PrintName;
            // 不再立即 Hide()
            ShuffleJobManager.Instance.StartNewShuffleJob(shufflerId, trueBoxId, printName );
            
          
        }
        finally
        {
            // 保持禁用直到事件回呼關閉頁面
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
    private void StopShuffleButton_Click(object sender, RoutedEventArgs e)
    {
        // 這裡一直都是主執行緒 沒有await非同��，所以還在UI thread上，
        // CustomMessageBoxResult Result  = CustomMessageBox.ShowDialog("Shuffling Stop!\r\n Please Confirm whether to terminate","Shuffling",CustomMessageBoxButtonType.YesNo,CustomMessageBoxIcon.Question);
        //
        // if (Result == CustomMessageBoxResult.Yes)
        // {
        //     try
        //     {
        //         _thisroundresult = false;
        //         if (DataContext is ShufflerAutoPageViewModel vm)
        //         {
        //             // 遇到await 關鍵字會將 UI 主執行緒釋���出來這時才是非同步
        //             await ShufflerManager.Instance.StopShuffle(vm.SelectedShufflerStatus);
        //         }
        //     }
        //     catch (Exception exception)
        //     {
        //         this.DialogResult = false;
        //         Console.WriteLine(exception);
        //         throw;
        //     }
        //
        //     //this.DialogResult = true;
        // }
    }

    private void ShufflerListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ShufflerAutoPageViewModel vm)
        {
            if (vm.SelectedShufflerStatus != null)
            {
                if (vm.SelectedShufflerStatus.LastStatus==ShufflingStatus.SHUFFLING)
                {
                    DoShuffleButton.IsEnabled = false;
                }
                else if(vm.SelectedShufflerStatus.LastStatus==ShufflingStatus.READY)
                {
                    DoShuffleButton.IsEnabled = true;
                }
                else
                {
                    DoShuffleButton.IsEnabled = false;
                }
            }
            else
            {
                DoShuffleButton.IsEnabled = false;
            }
        }
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

    
    /// <summary>
    /// Show MainWindow and Hide Self when Job Failed 目前和Succeeded一樣
    /// </summary>
    /// <param name="job"></param>
    /// <param name="ex"></param>
    private void OnJobFailed(ShufflingJob job, Exception ex)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsLoaded) return;
            DoShuffleButton.IsEnabled = true;
            ShufflerListBox.IsEnabled = true;
            CustomMessageBox.ShowDialog($"🔹Shuffler ID:{job.ShufflerId}\r\nShuffling Fail:{ex.Message}", "Shuffling", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            Application.Current.MainWindow?.Show();
            this.Hide();
        }));
    }
    
    /// <summary>
    /// Show MainWindow and Hide Self when Job Succeeded
    /// </summary>
    /// <param name="job"></param>
    private void OnJobSucceeded(ShufflingJob job)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsLoaded) return;
            DoShuffleButton.IsEnabled = true;
            ShufflerListBox.IsEnabled = true;
            ////!!!!!!!!
            CustomMessageBox.ShowDialog($"🔹Shuffler ID:{job.ShufflerId}\r\nShuffling Start", "Shuffling", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
            Application.Current.MainWindow?.Show();
            this.Hide();
        }));
    }

    private void OnJobCompleted(ShuffleJobManager.JobCompletedItem e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsLoaded) return;
            DoShuffleButton.IsEnabled = true;
            ShufflerListBox.IsEnabled = true;
            ////!!!!!!!!
            CustomMessageBox.ShowDialog($"🔹Shuffler IP:{e.ShufflerId}\r\n🔹Box ID:{e.PrintName}\r\nShuffling finished.", "Action 3 Pass",
                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
            this.Hide();
        }));
    }
}