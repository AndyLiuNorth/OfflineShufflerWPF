using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using ShufflerWPF.Manager; // 新增: 引入 TelegramErrorNotifier 所在的命名空間
using System.Windows.Input;
namespace ShufflerWPF.SingleTon;

public enum CustomMessageBoxResult
{
    None = 0,
    Ok = 1,
    Cancel = 2,
    Yes = 3,
    No = 4,
}
    
public enum CustomMessageBoxButtonType
{
    Ok = 0,
    OkCancel = 1,
    YesNo = 2,
    YesNoCancel = 3,
    NoneBtn = 4,
}
    
public enum CustomMessageBoxIcon
{
    None = 0,
    Error = 1,
    Question = 2,
    Warning = 3,
    Information = 4,
    Success = 5,
}
public partial class CustomMessageBox : Window
{
    
    /// <summary>
    /// Static method to show a dialog with the specified message, title, button format, and icon type.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="buttonFormat"></param>
    /// <param name="iconType"></param>
    /// <returns></returns>
    public static CustomMessageBoxResult ShowDialog(string message, string title, 
        CustomMessageBoxButtonType buttonFormat, CustomMessageBoxIcon iconType)
    {
        CustomMessageBox msgBox = new CustomMessageBox(message, title, buttonFormat, iconType);
        
        // 重要!!!
        // 【核心修正】: 在顯示對話框前，明確設定其擁有者。
        // 這可以解決在複雜的執行緒和視窗可見性切換下，WPF 無法正確推斷擁有者而導致的死結問題。
        if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            msgBox.Owner = Application.Current.MainWindow;
        }
        else
        {
            // 作為備用方案，尋找任何一個當前可見的視窗作為擁有者。
            msgBox.Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);
        }
        
        msgBox.Topmost = true;
        // 新增: 若為錯誤對話框，加入 Telegram 佇列 (不阻塞 UI)
        if (iconType == CustomMessageBoxIcon.Error)
        {
           _=TelegramErrorNotifier.EnqueueAsync($"[ErrorDialog]\nTitle: {title}\nMessage: {message}\nTime: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        msgBox.ShowDialog();
            
        return msgBox.Result; 
    }
    
    /// <summary>
    /// thread box
    /// </summary>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="buttonFormat"></param>
    /// <param name="iconType"></param>
    /// <returns></returns>
    public static CustomMessageBox Show(string message, string title,
        CustomMessageBoxButtonType buttonFormat, CustomMessageBoxIcon iconType)
    {
        CustomMessageBox msgBox = new CustomMessageBox(message, title, buttonFormat, iconType);
        msgBox.Topmost = true;
        msgBox.Show();
        return msgBox;
    }
    public string DialogTitle { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty; 
    public CustomMessageBoxResult Result { get; private set; }
    private CustomMessageBox(string message, string title, 
        CustomMessageBoxButtonType buttonFormat, CustomMessageBoxIcon iconType)
    {
        InitializeComponent();
        this.DataContext = this;
        this.Title = title;
        this.Message = message; 
        switch (iconType)
        {
            case CustomMessageBoxIcon.Error:
                IconPack.Kind = PackIconKind.AlphaXCircleOutline;
                IconPack.Foreground = System.Windows.Media.Brushes.Red;
                break;
            case CustomMessageBoxIcon.Question:
                IconPack.Kind = PackIconKind.HelpCircleOutline;
                IconPack.Foreground = System.Windows.Media.Brushes.Blue;
                break;
            case CustomMessageBoxIcon.Warning:
                IconPack.Kind = PackIconKind.Alert;
                IconPack.Foreground = System.Windows.Media.Brushes.Orange;
                break;
            case CustomMessageBoxIcon.Information:
                IconPack.Kind = PackIconKind.Information;
                IconPack.Foreground = System.Windows.Media. Brushes.Orange;
                break;
            case CustomMessageBoxIcon.Success:
                // 如果沒有圖示，隱藏它
                IconPack.Kind = PackIconKind.CheckCircle;
                IconPack.Foreground = System.Windows.Media. Brushes.LightGreen;
                break;
            case CustomMessageBoxIcon.None:
                // 如果沒有圖示，隱藏它
                IconPack.Visibility = Visibility.Collapsed;
                break;
        }
        
        StackPanel buttonPanel = this.FindName("ButtonPanel") as StackPanel;
        if (buttonPanel != null)
        {
            switch (buttonFormat)
            {
                case CustomMessageBoxButtonType.Ok:
                    AddButton("OK", CustomMessageBoxResult.Ok, buttonPanel);
                    break;
                case CustomMessageBoxButtonType.OkCancel:
                    AddButton("OK", CustomMessageBoxResult.Ok, buttonPanel);
                    AddButton("Cancel", CustomMessageBoxResult.Cancel, buttonPanel);
                    break;
                case CustomMessageBoxButtonType.YesNo:
                    AddButton("Yes", CustomMessageBoxResult.Yes, buttonPanel);
                    AddButton("No", CustomMessageBoxResult.No, buttonPanel);
                    break;
                case CustomMessageBoxButtonType.YesNoCancel:
                    AddButton("Yes", CustomMessageBoxResult.Yes, buttonPanel);
                    AddButton("No", CustomMessageBoxResult.No, buttonPanel);
                    AddButton("Cancel", CustomMessageBoxResult.Cancel, buttonPanel);
                    break;
                case CustomMessageBoxButtonType.NoneBtn:
                    break;
            }
        }
       
    }

    private void AddButton(string content, CustomMessageBoxResult result, StackPanel panel)
    {
        Button btn = new Button
        {
            Content = content,
            Width = 110,
            Height = 55,
            FontSize = 18,
            Margin = new Thickness(10),
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.DarkMagenta
            // ... set button Style ...
        };
        btn.Click += (sender, e) => {
            this.Result = result;
            this.Close(); // 按鈕點擊後關閉視窗
        };
        panel.Children.Add(btn);
    }
    
    public bool EnableDrag { get; set; } = true;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!EnableDrag) return;

        // 避免在按鈕上按住拖動造成誤操作
        if (e.OriginalSource is DependencyObject d)
        {
            if (FindParentButton(d) != null) return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Expected when drag conditions not met (e.g., not left mouse down)
            // This is normal, no need to log
        }
        catch (Exception ex)
        {
            // Unexpected drag error - log it
            Log4netManager.Logger.Warn($"Unexpected error during window drag: {ex.Message}");
        }
    }

    private static Button? FindParentButton(DependencyObject d)
    {
        while (d != null)
        {
            if (d is Button b) return b;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}