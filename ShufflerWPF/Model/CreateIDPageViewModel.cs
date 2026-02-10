using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ShufflerWPF.Manager;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Model;

public class CreateIDPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ObservableCollection<string> TableIDList { get; set; }
    public ObservableCollection<string> ColorType { get; set; }
    public ObservableCollection<string> IsMain { get; set; }

    public CreateIDPageViewModel()
    {
        TableIDList = new ObservableCollection<string>();
        ColorType = new ObservableCollection<string>();
        IsMain = new ObservableCollection<string>();
        // ✅ ADD: Initialize command
        CreateBoxCommand = new AsyncRelayCommand(ExecuteCreateBoxAsync, CanCreateBox);
        DeleteBoxCommand = new AsyncRelayCommand(ExecuteDeleteBoxAsync, CanDeleteBox);
    }

    private string? _creator;

    public string? Creator
    {
        get { return _creator; }
        set
        {
            if (_creator != value)
            {
                _creator = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _selectedTableId;

    public string? SelectedTableId
    {
        get { return _selectedTableId; }
        set
        {
            if (_selectedTableId != value) // 檢查值是否真正改變，避免不必要的更新
            {
                _selectedTableId = value;
                OnPropertyChanged(); // <-- **當這個屬性被設定時，通知 UI**
                (CreateBoxCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (DeleteBoxCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private string? _selectedColorType;

    public string? SelectedColorType
    {
        get { return _selectedColorType; }
        set
        {
            if (_selectedColorType != value) // 檢查值是否真正改變，避免不必要的更新
            {
                _selectedColorType = value;
                OnPropertyChanged(); // <-- **當這個屬性被設定時，通知 UI**
                (CreateBoxCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (DeleteBoxCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private string? _selectedIsMain;

    public string? SelectedIsMain
    {
        get { return _selectedIsMain; }
        set
        {
            if (_selectedIsMain != value) // 檢查值是否真正改變，避免不必要的更新
            {
                _selectedIsMain = value;
                OnPropertyChanged(); // <-- **當這個屬性被設定時，通知 UI**
                (CreateBoxCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (DeleteBoxCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private int? _maxCount = 0;

    public int? maxCount
    {
        get { return _maxCount; }
        set
        {
            if (_maxCount != value)
            {
                _maxCount = value;
                OnPropertyChanged();
            }
        }
    }

    // ✅ Click Button command
    public ICommand CreateBoxCommand { get; }

    // ✅ Callback for successful creation
    public Action<TrueBoxModel>? OnBoxCreatedSuccessfully { get; set; }

    // ✅ Callback for closing dialog
    public Action<bool>? OnCloseDialog { get; set; }
    
 


    private bool CanCreateBox()
    {
        return !string.IsNullOrEmpty(SelectedTableId)
               && !string.IsNullOrEmpty(SelectedColorType)
               && !string.IsNullOrEmpty(SelectedIsMain);
    }
    // ✅ Click Button command
    public ICommand DeleteBoxCommand { get; }

    private async Task ExecuteCreateBoxAsync()
    {
        string contentdata = string.Empty;
        string printdata = string.Empty;

        if (this.SelectedTableId == null || this.SelectedColorType == null ||
            this.SelectedIsMain == null)
        {
            CustomMessageBox.ShowDialog("Ensure to complete all the fields.", "Create True Shoe",
                CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Warning);
            return;
        }

        contentdata =
            $"Is the printing info below correct??\r\n\r\n🔹Table No. = {this.SelectedTableId}\r\n\r\n🔹Card Color={this.SelectedColorType}\r\n\r\n🔹Purpose={this.SelectedIsMain}";

        //string colorCode = vm.SelectedColorType == "Red" ? "R" : "B";
        //string ismaincode = vm.SelectedIsMain == "True" ? "" : "E";
        //printdata = $"{vm.SelectedTableId}{colorCode}{ismaincode}";


        try
        {
            CustomMessageBoxResult boxresult = CustomMessageBox.ShowDialog(contentdata, "Upload ID",
                CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Question);
            if (boxresult == CustomMessageBoxResult.Yes)
            {

                //// Do Get ID
                //string url = "";
                var (newmodel, errormessage) = await DoTrueBoxActionManager.DoTrueBoxActionZeroAsync(this);



                if (errormessage != string.Empty || newmodel == null)
                {
                    CustomMessageBox.ShowDialog($"Create TrueID Fail! action 0 Fail:[{errormessage}]",
                        "Create True Shoe", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
                    return;
                }

                DataCenter.CurrentTrueBox = newmodel;

                CustomMessageBox.ShowDialog("Box ID is created successfully.", "Create True Shoe",
                    CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Information);

                var splashbox = CustomMessageBox.Show("Printing QRCode...", "Please wait...",
                    CustomMessageBoxButtonType.NoneBtn, CustomMessageBoxIcon.Information);

                try
                {
                    Ql810WManager ql810WManager = new Ql810WManager();
                    ql810WManager.PrintInfo.Printmode = Ql810WManager.PrintMode.ShoeQRcode;
                    bool printresult = await ql810WManager.Ql810DoPrint(newmodel.trueId);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    splashbox.Close();
                    OnCloseDialog?.Invoke(false);
                    throw;
                }

                splashbox.Close();
                OnCloseDialog?.Invoke(true);

            }
            else
            {

            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }

    }

    
    public Action<(bool, string)>? OnCloseDeletePageDialog { get; set; }
    private async Task ExecuteDeleteBoxAsync()
    {
        

        if (this.SelectedTableId == null || this.SelectedColorType == null ||
            this.SelectedIsMain == null)
        {
            CustomMessageBox.ShowDialog("Ensure to complete all the fields.","Create True Shoe",CustomMessageBoxButtonType.Ok,CustomMessageBoxIcon.Warning);
            this.OnCloseDeletePageDialog?.Invoke((false,"Fields incomplete"));
            return;
        }
       
        try
        {
            var contentdata =
                $"Is the printing info below correct??\r\n\r\n🔹Table No. = {this.SelectedTableId}\r\n\r\n🔹Card Color={this.SelectedColorType}\r\n\r\n🔹Purpose={this.SelectedIsMain}";
            CustomMessageBoxResult boxresult = CustomMessageBox.ShowDialog(contentdata, "Delete ID",
                CustomMessageBoxButtonType.YesNo, CustomMessageBoxIcon.Warning);

            if (boxresult == CustomMessageBoxResult.Yes)
            {
                string err = await DoTrueBoxActionManager.DoTrueBoxActionDeleteByTableColorMainAsync(this);
                this.OnCloseDeletePageDialog?.Invoke((true,err));
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            this.OnCloseDeletePageDialog?.Invoke((false,""));
            throw;
        }
        
    }
    
    private bool CanDeleteBox()
    {
        return !string.IsNullOrEmpty(SelectedTableId)
               && !string.IsNullOrEmpty(SelectedColorType)
               && !string.IsNullOrEmpty(SelectedIsMain);
    }

}