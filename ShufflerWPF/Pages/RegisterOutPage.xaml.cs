using System.Windows;
using System.Windows.Input;
using ShufflerWPF.Manager;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Pages;

public partial class RegisterOutPage : Window
{
    private double? _nowaction;
    public RegisterOutPage(TrueBoxViewModel vm, double? currentaction)
    {
        InitializeComponent();
        DataContext = vm;
        _nowaction = currentaction;

        if (_nowaction == 4)
        {
            StatusLabel.Content = "Current Status: Action 4 - Ready to Take Out (Action 5)";
        }
        else if (_nowaction == 5)
        {
            StatusLabel.Content = "Current Status: Action 5 - Ready to Confirm receipt (Action 6)";
        }
    }

    private async void RegisterButtonButton_Click(object sender, RoutedEventArgs e)
    {
        
        RegisterButtonButton.IsEnabled = false;
        bool success = false;

        try
        {
            var box = DataCenter.CurrentTrueBox;
            if (box == null)
            {
                CustomMessageBox.ShowDialog("Box 資料不存在。", "Register", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                return;
            }

            double? currentAction = box.action;
            string? printName = box.PrintName;
            string? scanId = box.trueId;

            if (string.IsNullOrWhiteSpace(scanId))
            {
                CustomMessageBox.ShowDialog("Scan ID 為空，無法執行。", "Register", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                return;
            }

            string domain = DataCenter.MyShufflerSettings?.Domain ?? string.Empty;
            CustomMessageBoxResult ask;
            if (domain == "MX" || (domain == "CB" && currentAction == 4))
                ask = CustomMessageBox.ShowDialog($"Are you sure to Take out this shoe?\r\n🔹Box ID:{printName}", "Register",
                                                  CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Question);
            else if (domain == "CB" && currentAction == 5)
                ask = CustomMessageBox.ShowDialog($"Are you sure to Sign for shoe?\r\n🔹Box ID:{printName}", "Sign for shoe",
                                                  CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Question);
            else
                ask = CustomMessageBoxResult.No;

            if (ask != CustomMessageBoxResult.Yes)
                return;

            string action5Error = string.Empty;
            string action6Error = string.Empty;

            if (domain == "MX")
            {
                //action5Error = await DoTrueBoxActionManager.DoTrueBoxActionFifthAsync(scanId);
                action5Error  = await DoTrueBoxActionManager.DoTrueBoxActionFirstToSixAsync(scanId, DoTrueBoxActionManager.TrueBoxAction.TakeOut);
                if (!string.IsNullOrEmpty(action5Error))
                {
                    Log4netManager.Logger.Info($"Take Out Fail\r\n{action5Error}\r\n🔹Box ID:{scanId}");
                    CustomMessageBox.ShowDialog($"Take out this shoe Fail.\r\n{action5Error}", "Sign for shoe",
                                                 CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                    return;
                }

                //action6Error = await DoTrueBoxActionManager.DoTrueBoxActionSix(scanId);
                action6Error  = await DoTrueBoxActionManager.DoTrueBoxActionFirstToSixAsync(scanId, DoTrueBoxActionManager.TrueBoxAction.Confirmrecept);
                if (!string.IsNullOrEmpty(action6Error))
                {
                    Log4netManager.Logger.Info($"Sign for shoe Fail\r\n{action6Error}\r\n🔹Box ID:{scanId}");
                    CustomMessageBox.ShowDialog($"Sign for shoe  Fail.\r\n{action6Error}", "Sign for shoe ",
                                                 CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                    return;
                }

                CustomMessageBox.ShowDialog($"🔹Box ID:{printName}\r\n\r\nSign for shoe  successfully.", "Action 5 OK",
                                            CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
                success = true;
            }
            else if (domain == "CB")
            {
                if (_nowaction == 4)
                {
                    //action5Error = await DoTrueBoxActionManager.DoTrueBoxActionFifthAsync(scanId);
                    action5Error  = await DoTrueBoxActionManager.DoTrueBoxActionFirstToSixAsync(scanId, DoTrueBoxActionManager.TrueBoxAction.TakeOut);
                    if (!string.IsNullOrEmpty(action5Error))
                    {
                        Log4netManager.Logger.Error($"Take Out Fail\r\n{action5Error}\r\n🔹Box ID:{scanId}");
                        CustomMessageBox.ShowDialog($"Take Out this shoe Fail.\r\n{action5Error}", "Sign for shoe",
                                                     CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                        return;
                    }
                    CustomMessageBox.ShowDialog($"🔹Box ID:{printName}\r\n\r\nTake Out this shoe successfully.", "Action 5 OK",
                                                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
                    success = true;
                }
                else if (_nowaction == 5)
                {
                    //action6Error = await DoTrueBoxActionManager.DoTrueBoxActionSix(scanId);
                    action6Error  = await DoTrueBoxActionManager.DoTrueBoxActionFirstToSixAsync(scanId, DoTrueBoxActionManager.TrueBoxAction.Confirmrecept);
                    if (!string.IsNullOrEmpty(action6Error))
                    {
                        Log4netManager.Logger.Info($"Sign for shoe Fail\r\n{action6Error}\r\n🔹Box ID:{scanId}");
                        CustomMessageBox.ShowDialog($"Sign for shoe  Fail.\r\n{action6Error}", "Sign for shoe ",
                                                     CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                        return;
                    }
                    CustomMessageBox.ShowDialog($"🔹Box ID:{printName}\r\n\r\nSign for shoe  successfully.", "Action 5 OK",
                                                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Success);
                    success = true;
                }
            }
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Fatal($"RegisterButton exception: {ex.Message}");
            CustomMessageBox.ShowDialog($"Register Fail.\r\n{ex.Message}", "Register shoe",
                                        CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
        }
        finally
        {
            try
            {
                Application.Current.MainWindow?.Show();

                // 安全設定結果：只有在以 ShowDialog 開啟時才可設 DialogResult
                if (this.Owner != null) // 典型對話窗在 ShowDialog 前會指定 Owner
                    this.DialogResult = success;
                else
                    if (success) this.Close();
            }
            catch
            {
                // 避免再次拋出導致閃退
            }
            RegisterButtonButton.IsEnabled = true;
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
}