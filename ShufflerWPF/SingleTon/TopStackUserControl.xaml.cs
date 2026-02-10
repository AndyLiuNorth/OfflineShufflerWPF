using System.DirectoryServices.ActiveDirectory;
using System.Windows;
using System.Windows.Controls;
using ShufflerWPF.Manager;

namespace ShufflerWPF.SingleTon;

public partial class TopStackUserControl : UserControl
{
    private Window? _parentWindow;
    public TopStackUserControl()
    {
        InitializeComponent();
        Loaded += TopStackUserControl_Loaded;
        
        try
        {
            
            // Loaded += (s, e) =>
            // {
            //     var parentWindow = Window.GetWindow(this);
            //     if (parentWindow != null)
            //     {
            //         parentWindow.IsVisibleChanged += ParentWindow_IsVisibleChanged;
            //     }
            // };
            // 原本: this.DataContext = new { userid = DataCenter.CurrentMember.id.ToString() ?? "null" };
            var cm = DataCenter.CurrentMember;
            string? userIdStr = cm == null ? "null" : (cm.id != null ? cm.idui : "null");
            this.DataContext = new { userid = userIdStr };
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error("Failed to set DataContext in TopStackUserControl.", ex);
            this.DataContext = new { Username = "Error" };
        }
    }
    
    private void TopStackUserControl_Loaded(object? sender, RoutedEventArgs e)
    {
        AttachParentWindowEvents();
    }
    private void AttachParentWindowEvents()
    {
        _parentWindow = Window.GetWindow(this);
        if (_parentWindow != null)
        {
            _parentWindow.IsVisibleChanged += ParentWindow_IsVisibleChanged;
        }
    }
    private void ParentWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _parentWindow = Window.GetWindow(this);
        if (_parentWindow?.IsVisible == true)
        {
            try
            {
                var cm = DataCenter.CurrentMember;
                //string userIdStr = (cm != null && cm.id != null) ? cm.idui : "null";
                string userIdStr = (cm != null && cm.id != null) ? (cm.idui ?? "null") : "null";
                this.DataContext = new { userid = userIdStr };
            }
            catch (Exception ex)
            {
                Log4netManager.Logger.Error("Failed to set DataContext in TopStackUserControl.", ex);
                this.DataContext = new { Username = "Error" };
            }
        }
    }
    // private void ParentWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    // {
    //     // Reload id to top
    //     try
    //     {
    //         this.DataContext = new { userid = DataCenter.CurrentMember.id.ToString() ?? "null" };
    //     }
    //     catch (Exception ex)
    //     {
    //         Log4netManager.Logger.Error("Failed to set DataContext in TopStackUserControl.", ex);
    //         this.DataContext = new { Username = "Error" };
    //     }
    // }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        
        var parentWindow = Window.GetWindow(this);
        Log4netManager.Logger.Info($"CancelButton_Click from [{parentWindow?.Name ?? "null"}]");

        if (parentWindow == null)
        {
            Log4netManager.Logger.Warn("Parent window is null in CancelButton_Click.");
        }
        else
        {
            // 僅在模態視窗時可設定 DialogResult
            if (System.Windows.Interop.ComponentDispatcher.IsThreadModal)
            {
                // 確保視窗允許設定 DialogResult（只有 ShowDialog 開啟的視窗）
                parentWindow.DialogResult = false;
            }
            else
            {
                parentWindow.Hide();
            }
        }

        try
        {
            if (Application.Current?.MainWindow != null && !Application.Current.MainWindow.IsVisible)
            {
                Application.Current.MainWindow.Show();
            }
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error("Failed to show MainWindow.", ex);
        }
        
    }
    


}