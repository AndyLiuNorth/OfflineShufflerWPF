using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShufflerWPF.Manager;
namespace ShufflerWPF.Model;

public class ShufflerAutoPageViewModel:INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private ObservableCollection<ShufflerStatus>? _shufflerStatusList;

    public ObservableCollection<ShufflerStatus>? ShufflerStatusList
    {
        get { return _shufflerStatusList;}
        set
        {
            // 檢查新的值是否與舊的值不同
            if (_shufflerStatusList != value)
            {
                _shufflerStatusList = value;
                OnPropertyChanged();
            }
        }
        
    }
    
    
    // 注意這邊是重點，UI有做綁定<Grid DataContext="{Binding SelectedShufflerStatus}">
    // 所以List選取時會自動獲取選取的ShufflerStatus參數
    // 另一重點在於ShufflerManager內部的List<ShufflerStatus>是直接綁定到這個SelectedShufflerStatus
    // 而原廠是Server不斷送Status過來(如下範例)，然後會對應IP去更新List<ShufflerStatus>，然後UI一起更新
    // 所以有個重點就是，洗牌過程中去點選IP切換，是不發送或更改任何指令過去的
    // {
    //     "CommandName" : "ShufflingCardCount",
    //     "ShufflerId" : "10.93.93.93",
    //     "CardCount" : 50,
    //     "Thickness" : -1
    // }
    private ShufflerStatus? _selectedShufflerStatus;
    public ShufflerStatus? SelectedShufflerStatus
    {
        get => _selectedShufflerStatus;
        set
        {
            if (_selectedShufflerStatus != value)
            {
                _selectedShufflerStatus = value;
                OnPropertyChanged();
            }
        }
    }
    
    public ShufflerAutoPageViewModel()
    {
        ShufflerStatusList = new ObservableCollection<ShufflerStatus>();
        OneBoxModel = new TrueBoxModel();
    }
    
    private TrueBoxModel? _oneBoxModel;
    public TrueBoxModel? OneBoxModel
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
   
}