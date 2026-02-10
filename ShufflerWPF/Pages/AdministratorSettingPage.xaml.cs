using System.IO;
using System.Windows;
using System.Windows.Input;
using ShufflerWPF.Manager;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Pages;

public partial class AdministratorSettingPage : Window
{
    public AdministratorSettingPage()
    {
        InitializeComponent();
        
        DataContext  = _reloadParamFile();;
       
        
    }

    private const string SettingsFileName = "shufflersettings.ini";
    private SoftWareSettingsModel? _reloadParamFile()
    {
        try
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

            if (File.Exists(filePath))
            {
                FileProcessManager.Instance.LoadSettingsFromFile(filePath);

                DataCenter.MyShufflerSettings = FileProcessManager.Instance.Settings;

                Log4netManager.Logger.Info($"Successfully loaded settings from {SettingsFileName}.");
            }
            else
            {
                Log4netManager.Logger.Warn($"{SettingsFileName} not found. Using default settings.");
            }
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"Failed to load settings from {SettingsFileName}.", ex);
            // 即使載入失敗，程式也會繼續使用預設值運行
        }
        
        return FileProcessManager.Instance.Settings;
    }

    private async void SaveSettingBtnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SoftWareSettingsModel nowVm)
        {
            try
            {
                await FileProcessManager.Instance.SaveSettingsAsync(nowVm);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }

            CustomMessageBox.ShowDialog($"Save Setting Success!", "Save Setting", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Information);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = true;
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