using System.Windows;
using System.Windows.Input;
using ShufflerWPF.Manager;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Pages;

public partial class ShuffleManualPage : Window
{
    public ShuffleManualPage(TrueBoxViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.OnShufflingCompleted += HandleManualComplete;
        
    }

    private void HandleManualComplete(bool result)
    {
        this.DialogResult = result;
    }
    
    // private async void ManualShufflingButton_Click(object sender, RoutedEventArgs e)
    // {
    //     CustomMessageBox.ShowDialog("Please perform manual shuffling.","Shuffling",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Information);
    //
    //     if(DataCenter.CurrentTrueBox.trueId== null)
    //     {
    //        CustomMessageBox.ShowDialog("Please make sure you have Enter a True Box ID before proceeding.", "Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
    //        return; 
    //     }
    //     
    //     //string errormes = await DoTrueBoxActionManager.DoTrueBoxActionFirstAsync(DataCenter.CurrentTrueBox.trueId);
    //     string errormes = await DoTrueBoxActionManager.DoTrueBoxActionFirstToSixAsync(DataCenter.CurrentTrueBox.trueId, DoTrueBoxActionManager.TrueBoxAction.ManualFinished);
    //     
    //     if (errormes==string.Empty)
    //     {
    //         this.DialogResult = true;
    //     }
    //     else
    //     { 
    //         Log4netManager.Logger.Error($"Manual shuffling is not allowed.\r\n Message:[{errormes}]");
    //         CustomMessageBox.ShowDialog($"Manual shuffling is not allowed.\r\n Message:[{errormes}]", "Action 1 Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
    //         this.DialogResult = false;
    //     }
    //     
    //    
    // }
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