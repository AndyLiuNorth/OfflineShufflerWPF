using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using ShufflerWPF.Manager;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ShufflerWPF.Pages;

public partial class CreateBoxIDPage : Window
{
    public CreateBoxIDPage(List<string> gametablelist)
    {
        InitializeComponent();

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
            
            // ✅ 註冊方法!!在ViewModel觸發DialogResult寫入
            vm.OnCloseDialog = (result) => this.DialogResult = result;
        }
        
    }


    // private async void ButtonEnter_OnClick(object sender, RoutedEventArgs e)
    // {
    //     string contentdata = string.Empty; 
    //     string printdata = string.Empty;
    //     
    //     if (DataContext is CreateIDPageViewModel vm)
    //     {
    //
    //         if (vm.SelectedTableId == null || vm.SelectedColorType == null ||
    //             vm.SelectedIsMain == null)
    //         {
    //             CustomMessageBox.ShowDialog("Ensure to complete all the fields.","Create True Shoe",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Warning);
    //             return;
    //         }
    //         
    //         contentdata =
    //             $"Is the printing info below correct??\r\n\r\n🔹Table No. = {vm.SelectedTableId}\r\n\r\n🔹Card Color={vm.SelectedColorType}\r\n\r\n🔹Purpose={vm.SelectedIsMain}";
    //
    //         //string colorCode = vm.SelectedColorType == "Red" ? "R" : "B";
    //         //string ismaincode = vm.SelectedIsMain == "True" ? "" : "E";
    //         //printdata = $"{vm.SelectedTableId}{colorCode}{ismaincode}";
    //
    //
    //         try
    //         {
    //             CustomMessageBoxResult boxresult = CustomMessageBox.ShowDialog(contentdata, "Upload ID",
    //                 CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Question);
    //             if (boxresult == CustomMessageBoxResult.Yes)
    //             {
    //
    //                 //// Do Get ID
    //                 //string url = "";
    //                 var (newmodel, errormessage)  = await DoTrueBoxActionManager.DoTrueBoxActionZeroAsync(vm);
    //
    //                 
    //
    //                 if (errormessage != string.Empty || newmodel == null)
    //                 {
    //                     CustomMessageBox.ShowDialog($"Create TrueID Fail! action 0 Fail:[{errormessage}]","Create True Shoe",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Error);
    //                     return;
    //                 }
    //                 
    //                 DataCenter.CurrentTrueBox = newmodel;
    //
    //                 CustomMessageBox.ShowDialog("Box ID is created successfully.","Create True Shoe",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Information);
    //
    //                 var splashbox = CustomMessageBox.Show("Printing QRCode...", "Please wait...", CustomMessageBoxButtonType.NoneBtn, CustomMessageBoxIcon.Information);
    //
    //                 try
    //                 {
    //                     Ql810WManager ql810WManager = new Ql810WManager();
    //                     ql810WManager.PrintInfo.Printmode = Ql810WManager.PrintMode.ShoeQRcode;
    //                     bool printresult = await ql810WManager.Ql810DoPrint(newmodel.trueId);
    //                 }
    //                 catch (Exception exception)
    //                 {
    //                     Console.WriteLine(exception);
    //                     splashbox.Close();
    //                     this.DialogResult = false;
    //                     throw;
    //                 }
    //
    //                 splashbox.Close();
    //                 this.DialogResult = true;
    //
    //             }
    //             else
    //             {
    //
    //             }
    //         }
    //         catch (Exception exception)
    //         {
    //             Console.WriteLine(exception);
    //             throw;
    //         }
    //     }
    //
    // }

    private void MaxCount_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9]+");
        e.Handled = regex.IsMatch(e.Text);
    }
    
    /// <summary>
    /// 在文字變更後，移除開頭的零，並確保不為空。
    /// </summary>
    private void MaxCount_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // 移除開頭的零 (如果有多個，例如 005 -> 5)
            if (textBox.Text.Length > 1 && textBox.Text.StartsWith("0"))
            {
                textBox.Text = textBox.Text.TrimStart('0');
            }

            // 如果TextBox變空了，設為0
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "0";
            }
            
            if (int.TryParse(textBox.Text, out int value))
            {
                if (value > 99)
                    textBox.Text = "99";
                else if (value < 0)
                    textBox.Text = "0";
                // 將游標移到最後
                textBox.CaretIndex = textBox.Text.Length;
            }
            else if (!string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text = "0";
                textBox.CaretIndex = 1;
            }

            // 確保游標在文字的最後面
            textBox.CaretIndex = textBox.Text.Length;
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