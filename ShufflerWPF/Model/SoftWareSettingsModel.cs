using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace ShufflerWPF.Model;

public class SoftWareSettingsModel: INotifyPropertyChanged 
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    [JsonIgnore] public ObservableCollection<string> DomainCombobox{ get; set; }
    [JsonIgnore] public ObservableCollection<string> StudioCombobox{ get; set; }
    [JsonIgnore] public ObservableCollection<string> LiveModeCombobox{ get; set; }

    // Domain -> Studios 對應表
    private readonly Dictionary<string, string[]> _studioOptionsByDomain = new()
    {
        { "MX", new [] { "Nightclub", "Witcher", "Opera", "Kingsman" } },
        { "CB", new [] { "A", "B" } },
        // 可視需要保留 Global 行為，這裡給與 MX 同組或可改為 Array.Empty<string>()
        { "Global", new [] { "Nightclub", "Witcher", "Opera", "Kingsman" } }
    };

    public SoftWareSettingsModel()
    {
        DomainCombobox = new ObservableCollection<string>{ "MX", "CB" };
        LiveModeCombobox = new ObservableCollection<string>{ "True", "False" };
        StudioCombobox = new ObservableCollection<string>();
        // 初始化依預設 Domain 載入對應 Studios
        UpdateStudiosForDomain();
    }
    public int DoMainNumber
    {
        get
        {
            return _doMain switch
            {
                "Global" => 1,
                "MX" => 2,
                "CB" => 3,
                _ => 0
            };
        }
    }

    // for zack service MX or CB
    private string _doMain = "Global"; // 預設值
    public string Domain
    {
        get => _doMain;
        set
        {
            if (_doMain != value)
            {
                _doMain = value;
                OnPropertyChanged();
                UpdateStudiosForDomain();
            }
        }
    }
    
    private string _studio = "Nightclub"; // 預設值

    public string Studio
    {
        get => _studio;
        set
        {
            if (_studio != value)
            {
                _studio = value;
                OnPropertyChanged();
            }
        }
    }
    
    private int _liveMode = 0; // 預設值
    public string LiveMode
    {
        get
        {
            return _liveMode switch
            {
                0 => "True",
                1 => "False",
                _ => "False"
            };
        }
        set
        {
            // 先將傳入的 string (例如 "MX") 轉換成對應的 int
            int newliveValue = value switch
            {
                "True" => 0,
                "False" => 1,
                _ => 1
            };

            // 只有在內部的 int 值真正改變時，才更新並通知 UI
            if (_liveMode != newliveValue)
            {
                _liveMode = newliveValue;
                OnPropertyChanged();
            }
        }
    }
    
    private string _authApi = "https://bi-dev.theiehicppgcs.com/api/v1/on-site/shuffle-room/users/";
    public string AuthApi
    {
        get => _authApi;
        set
        {
            if (_authApi != value)
            {
                _authApi = value;
                OnPropertyChanged();
            }
        }
    }
    
    private string _tableListApi = "https://bi-dev.theiehicppgcs.com/api/v1/on-site/shuffle-room/game-tables";
    public string TableListApi
    {
        get => _tableListApi;
        set
        {
            if (_tableListApi != value)
            {
                _tableListApi = value;
                OnPropertyChanged();
            }
        }
    }
    
    private string _engineerLogInApi = "https://north-op-dev.theiehicppgcs.com/api/v1/members/sign-in";
    public string EngineerLogInApi
    {
        get => _engineerLogInApi;
        set
        {
            if (_engineerLogInApi != value)
            {
                _engineerLogInApi = value;
                OnPropertyChanged();
            }
        }
    }

    private string _shufflerServerUrl = "http://3.114.71.1:8075/event/";
    public string ShufflerServerUrl
    {
        get => _shufflerServerUrl;
        set
        {
            if (_shufflerServerUrl != value)
            {
                _shufflerServerUrl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActionTrueIdApi));
                OnPropertyChanged(nameof(GetTrueIdApi));
                OnPropertyChanged(nameof(ActionDeleteby));
            }
        }
    }
    
    private string _shufflerServerUrl2 = "";
    public string ShufflerServerUrl2
    {
        get => _shufflerServerUrl2;
        set
        {
            if (_shufflerServerUrl2 != value)
            {
                _shufflerServerUrl2 = value;
                OnPropertyChanged();
            }
        }
    }
    
    private string _shufflerServerUrl3 = "";
    public string ShufflerServerUrl3
    {
        get => _shufflerServerUrl3;
        set
        {
            if (_shufflerServerUrl3 != value)
            {
                _shufflerServerUrl3 = value;
                OnPropertyChanged();
            }
        }
    }
    
    private string _actionTrueIdApi = "actionTrueId";
    public string ActionTrueIdApi
    {
        get
        {
            var apiname = _actionTrueIdApi;
            var sb = new StringBuilder();

            void createAllUrl(string url)
            {
                if(!string.IsNullOrEmpty(url))
                    sb.Append(url).Append(apiname);
            }

            createAllUrl(_shufflerServerUrl);

            return sb.ToString();
        }

    }
    private string _getTrueIdApi = "getTrueInfo";
    public string GetTrueIdApi
    {
        get
        {
            var apiname = _getTrueIdApi;
            var sb = new StringBuilder();

            void createAllUrl(string url)
            {
                if(!string.IsNullOrEmpty(url))
                    sb.Append(url).Append(apiname);
            }

            createAllUrl(_shufflerServerUrl);

            return sb.ToString();
        }
    }
    
    private string _getInfobyTable = "getInfoForTableColorMain";
    public string GetInfobyTable
    {
        get
        {
            var apiname = _getInfobyTable;
            var sb = new StringBuilder();

            void createAllUrl(string url)
            {
                if(!string.IsNullOrEmpty(url))
                    sb.Append(url).Append(apiname);
            }
            createAllUrl(_shufflerServerUrl);
            return sb.ToString();
        }
    }
    
    private string _actionDeleteby = "deactivateForTableColorMain";
    public string ActionDeleteby
    {
        get
        {
            var apiname = _actionDeleteby;
            var sb = new StringBuilder();

            void createAllUrl(string url)
            {
                if(!string.IsNullOrEmpty(url))
                    sb.Append(url).Append(apiname);
            }

            createAllUrl(_shufflerServerUrl);

            return sb.ToString();
        }

    }
    
    private string _shufflerMachineServerIp = "127.0.0.1";
    public string ShufflerMachineServerIp
    {
        get => _shufflerMachineServerIp;
        set
        {
            if (_shufflerMachineServerIp != value)
            {
                _shufflerMachineServerIp = value;
                OnPropertyChanged();
            }
        }
    }
    
    private string _apiProxyUrl = "http://43.212.218.113:8077/api/apiProxy";
    public string ApiProxyUrl
    {
        get => _apiProxyUrl;
        set
        {
            if (_apiProxyUrl != value)
            {
                _apiProxyUrl = value;
                OnPropertyChanged();
            }
        }
    }
    
    private string _apiProxyToken = string.Empty;
    public string ApiProxyToken
    {
        get => _apiProxyToken;
        set
        {
            if (_apiProxyToken != value)
                _apiProxyToken = value;
        }
    }
    private string _biOpToken = string.Empty;
    public string BiOpToken
    {
        get => _biOpToken;
        set
        {
            if (_biOpToken != value)
                _biOpToken = value;
        }
    }
    
    

    private void UpdateStudiosForDomain()
    {
        if (_studioOptionsByDomain.TryGetValue(_doMain, out var studios))
        {
            StudioCombobox.Clear();
            foreach (var s in studios) StudioCombobox.Add(s);
            if (!StudioCombobox.Contains(_studio))
            {
                Studio = StudioCombobox.FirstOrDefault() ?? string.Empty;
            }
        }
        else
        {
            StudioCombobox.Clear();
            Studio = string.Empty;
        }
        // 若改成重新指派集合才需要 OnPropertyChanged("StudioCombobox");
    }
}