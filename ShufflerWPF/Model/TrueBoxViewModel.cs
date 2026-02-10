using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShufflerWPF.Manager;
using ShufflerWPF.SingleTon;
using System.Text.Json;
using System.Windows.Input;

namespace ShufflerWPF.Model;

public class TrueBoxViewModel:INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public TrueBoxViewModel()
    {
        //_webServiceManager = WebserviceSingleTonManager.Instance;
        _oneBoxModel = new  TrueBoxModel();
        //_oneBoxModel.PropertyChanged += _trueBoxChange;
        ManualShufflingCommand = new AsyncRelayCommand(ExecuteManualShufflingAsync, CanManualShuffling);
    }

    // this method update all property of this truebox when trueID change
    //private WebserviceSingleTonManager _webServiceManager;
    // private async void _trueBoxChange(object? sender, PropertyChangedEventArgs e)
    // {
    //     if (e.PropertyName == nameof(_oneBoxModel.TrueId))
    //     {
    //         
    //         // //debug demo this is for update truebox when trueID change.
    //         // var updatedData = new {
    //         //     TrueId = _oneBoxModel.TrueId,
    //         //     Creator = _oneBoxModel.Creator,
    //         //     // ... 其他屬性 ...
    //         // };
    //         // string jsonPayload = JsonSerializer.Serialize(updatedData);
    //         // string jsonPayloadPBE = DataCenter.EncryptForWebService("jsonPayload","thisispassword");
    //         // try
    //         // {
    //         //     string result = await _webServiceManager.SendPostAsync("thisisURL", jsonPayloadPBE);
    //         //     TrueBoxModel newmodel = JsonSerializer.Deserialize<TrueBoxModel>(result);
    //         //     Console.WriteLine($"Web API 更新成功，回應: {result}");
    //         // }
    //         // catch (Exception ex)
    //         // {
    //         //     Console.WriteLine($"Web API 更新失敗: {ex.Message}");
    //         // }
    //
    //         TrueBoxModel newmodel = new TrueBoxModel()
    //         {
    //             TrueId = _oneBoxModel.TrueId,
    //             DoMain = "DMDM",
    //             TableId = "ccc22",
    //             ColorType = "Blue",
    //             IsMain = "True",
    //             Count = 1,
    //             Status = 2
    //         };
    //     }
    // }
    
    
    private TrueBoxModel _oneBoxModel;

    public TrueBoxModel OneBoxModel
    {
        get { return _oneBoxModel; }
        set
        {
            if (OneBoxModel != value)
            {
                _oneBoxModel = value;
                OnPropertyChanged();
            }
           
        }
    }
    
    // Manual Shuffling Command
    public ICommand ManualShufflingCommand { get;}
    
    public Action<bool>? OnShufflingCompleted { get; set; }

    private bool CanManualShuffling()
    {
        return !string.IsNullOrEmpty(DataCenter.CurrentTrueBox?.trueId);
    }

    private async Task ExecuteManualShufflingAsync()
    {
        CustomMessageBox.ShowDialog("Please perform manual shuffling.", "Shuffling", CustomMessageBoxButtonType.Ok,
            CustomMessageBoxIcon.Information);

        if (DataCenter.CurrentTrueBox.trueId == null)
        {
            CustomMessageBox.ShowDialog("Please make sure you have Enter a True Box ID before proceeding", "Error",
                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);

            return;
        }
        
        //string errormes = await DoTrueBoxActionManager.DoTrueBoxActionFirstAsync(DataCenter.CurrentTrueBox.trueId);
        string errormes = await DoTrueBoxActionManager.DoTrueBoxActionFirstToSixAsync(DataCenter.CurrentTrueBox.trueId, DoTrueBoxActionManager.TrueBoxAction.ManualFinished);
        
        
        if (errormes==string.Empty)
        {
            // Run UI
            OnShufflingCompleted?.Invoke(true);
        }
        else
        { 
            Log4netManager.Logger.Error($"Manual shuffling is not allowed.\r\n Message:[{errormes}]");
            CustomMessageBox.ShowDialog($"Manual shuffling is not allowed.\r\n Message:[{errormes}]", "Action 1 Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            
            // Run uI
            OnShufflingCompleted?.Invoke(false);
        }
    }
    
    
}