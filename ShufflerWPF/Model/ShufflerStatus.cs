using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
namespace ShufflerWPF.Model;

[Flags]
public enum ShufflingStatus {
    /// <summary>
    ///
    /// </summary>
    DISCONNECTED = 0,
    /// <summary>
    ///
    /// </summary>
    READY = 1,
    /// <summary>
    ///
    /// </summary>
    SHUFFLING = 1 << 1,
    /// <summary>
    ///
    /// </summary>
    DEAL = 1 << 2,
    /// <summary>
    ///
    /// </summary>
    REPLACE = 1 << 3,
    /// <summary>
    ///
    /// </summary>
    REPLACEING = 1 << 4,
    /// <summary>
    ///
    /// </summary>
    ERROR = 1 << 5,
    /// <summary>
    ///
    /// </summary>
    READ_WRITE = 1 << 6,
    /// <summary>
    ///
    /// </summary>
    ACTION_CMD = 1 << 7,
    WAITTING = 1 << 8,
    HEARTBEATING = 1 << 9,
}
public class ShufflerStatus: INotifyPropertyChanged 
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    public string? ShufflerId { get; set; } 
    

    public string? FirmVersion { get; set; }        // 洗牌機韌體版本
    public string? FirmId{ get; set; }           // 洗牌機序號
    public string? FirmGen{ get; set; }         // 洗牌機代數

    
    public string _nowShufflingTrueid;//現在正在運行的TrueID
    public string NowShufflingTrueid
    {
        get{return _nowShufflingTrueid;}
        set
        {
            if (_nowShufflingTrueid != value)
            {
                _nowShufflingTrueid = value;
                OnPropertyChanged();
            }
        }
    }
    
    //public bool IsOnline{ get; set; }           // 是否連線
    private bool _isOnline;
    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            if (_isOnline != value)
            {
                _isOnline = value;
                OnPropertyChanged(); // 通知 UI "IsOnline" 屬性已變更
            }
        }
    }
    
    private ShufflingStatus _lastStatus; // 假設 ShufflingStatus 是您的枚舉或類別
    public ShufflingStatus LastStatus
    {
        get => _lastStatus;
        set
        {
            // 只有在值真正改變時才更新並發出通知，這是最佳實踐
            if (!Equals(_lastStatus, value))
            {
                _lastStatus = value;
                OnPropertyChanged(); // 當 LastStatus 被賦予新值時，通知所有綁定到它的 UI 元素
            }
        }
    }
    
    //public string LastError{ get; set; }        // 洗牌機最後錯誤
    public string? _lastError;

    public string? LastError
    {
        get{return _lastError;}
        set
        {
            if (_lastError != value)
            {
                _lastError = value;
                OnPropertyChanged();
            }
        }
    }
    //public string LastWarning{ get; set; }      // 洗牌機最後警告
    private string? _lastWarning;
    public string? LastWarning
    {
        get{return _lastWarning;}
        set
        {
            if (_lastWarning != value)
            {
                _lastWarning = value;
                OnPropertyChanged();
            }
        }
    }
    
    //public bool CardCase{ get; set; }           // 是否有牌匣
    private bool _cardCase;
    public bool CardCase
    {
        get{return _cardCase;}
        set
        {
            if (_cardCase != value)
            {
                _cardCase = value;
                OnPropertyChanged();
            }
        }
    }
   

    //public int NumberOfCompletedShuffling{ get; set; }  // 已完成洗牌次數
    private int _numberOfCompletedShuffling;

    public int NumberOfCompletedShuffling
    {
        get{return _numberOfCompletedShuffling;}
        set
        {
            if (_numberOfCompletedShuffling != value)
            {
                _numberOfCompletedShuffling = value;
                OnPropertyChanged();
            }
        }
    }
    //public bool IsShuffleCompleted{ get; set; }         // 是否完成洗牌
    private bool _isShuffleCompleted;
    public bool IsShuffleCompleted
    {
        get{return _isShuffleCompleted;}
        set
        {
            if (_isShuffleCompleted != value)
            {
                _isShuffleCompleted = value;
                OnPropertyChanged();
            }
        }
    }
    
    private int _cardCount;
    public int CardCount
    {
        get => _cardCount;
        set
        {
            if (_cardCount != value)
            {
                _cardCount = value;
                OnPropertyChanged(); // 通知 UI "IsOnline" 屬性已變更
            }
        }
    }
    
    //public bool IsTouchLocked{ get; set; }      // 是否鎖定觸控
    private bool _isTouchLocked;
    public bool IsTouchLocked
    {
        get => _isTouchLocked;
        set
        {
            if (_isTouchLocked != value)
            {
                _isTouchLocked = value;
                OnPropertyChanged();
            }
        }
    }
    
    //Wow~~~  NumberOfPokerValue [Int32陣列] 每種牌號的數量 索引 0 為辨識失敗，1-13 為黑桃 A-K，14-26 為紅心 A-K，27-39 為方塊 A-K，40-52 為梅花 A-K
    public int[] NumberOfPokerValue{ get; set; }
   

    public override string ToString() {
        return $"{this.ShufflerId}";
    }
}