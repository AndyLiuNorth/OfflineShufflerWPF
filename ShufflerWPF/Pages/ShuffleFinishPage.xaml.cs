using System.Windows;
using System.Windows.Input;
using ShufflerWPF.SingleTon;
using ShufflerWPF.Model;
using ShufflerWPF.Manager;
namespace ShufflerWPF.Pages;

public partial class ShuffleFinishPage : Window
{
    public string errormessage { get; set; } = string.Empty;
    public ShuffleFinishPage(TrueBoxViewModel vm)
    {
        InitializeComponent();
        //notice this register over twice
        //this.Activated += ShuffleManualPage_Activated;
        //demo debug
        DataContext = vm;
    }
    private async void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        PrintButton.IsEnabled=false;
        errormessage = string.Empty;
        if (DataContext is TrueBoxViewModel vm)
        {
            try
            {
                if (vm.OneBoxModel.tableId == null)
                {
                    throw new Exception($"Box ID {vm.OneBoxModel.trueId}\r\n tableId is Null");
                }
                string tableId = vm.OneBoxModel.tableId;
                
                if (string.IsNullOrWhiteSpace(vm.OneBoxModel.colorType))
                {
                    throw new Exception($"Box ID {vm.OneBoxModel.trueId}\r\ncolorType is Null or Empty");
                }
                string colorCode = vm.OneBoxModel.colorType!.ToUpperInvariant();
                string ismaincode = vm.OneBoxModel.isMain == true ? "M" : "B";
            
                string printdata=$"{tableId}{colorCode}{ismaincode}";

                if (vm.OneBoxModel.shufflerIp == null)
                {
                    throw new Exception($"Box ID {vm.OneBoxModel.trueId}\r\nshufflerIP is Null");
                }
                string printip = vm.OneBoxModel.shufflerIp;
                
                //Debug demp add IP
                var splashbox = CustomMessageBox.Show("Print Shoe Information...", "Please wait...", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Information);

                Ql810WManager printer = new Ql810WManager();
                //printer.PrintInfo.count = DataCenter.CurrentTrueBox.count.ToString();
                //printer.PrintInfo.tableIDName = DataCenter.CurrentTrueBox.PrintName;
                printer.PrintInfo.Printmode = Ql810WManager.PrintMode.ShoeInfo;
                bool result = await printer.Ql810DoPrint();
                
                splashbox.Close();
                
                this.DialogResult = result;
            }
            catch (Exception ex)
            {
                this.DialogResult = false;
                errormessage = ex.Message;
                Console.WriteLine(ex);
            }
            finally
            {
                
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
    
}