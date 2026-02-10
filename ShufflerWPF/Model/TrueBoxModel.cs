using System.Text.Json;
namespace ShufflerWPF.Model;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShufflerWPF.SingleTon;

public class TrueBoxModel:INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    // public string UserId { get; set; }
    // public string Domain { get; set; } = string.Empty;
    // public string TrueId { get; set; } = string.Empty;
    // public string TableId { get; set; } = string.Empty;
    // public string ColorType { get; set; } = string.Empty;
    // public string IsMain { get; set; } = string.Empty;
    // public int Count { get; set; }
    // public int Status { get; set; }
    private int? _doMain;
    public int? domain
    {
        get { return _doMain;}
        set
        {
            if (_doMain != value)
            {
                _doMain = value;
                OnPropertyChanged();
                //OnPropertyChanged(nameof(PrintName));
            }
        }
    }
    public string DomainUi
    {
        get
        {
            return _doMain switch
            {
                1 => "Global",
                2 => "MX",
                3 => "CB",
                _ => "Null"
            };
        }
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
    
    private string? _updater;
    public string? updater
    {
        get { return _updater; }
        set
        {
            if (_updater != value)
            {
                _updater = value;
                OnPropertyChanged();
            }
        }
    }
    
    private string? _shufflerIP;
    public string? shufflerIp
    {
        get { return _shufflerIP; }
        set
        {
            if (_shufflerIP != value)
            {
                _shufflerIP = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _trueId;
    public string? trueId
    {
        get{return _trueId;}
        set
        {
            if (_trueId != value)
            {
                _trueId = value;
                DataCenter.CurrentTrueBox.trueId = value;
                OnPropertyChanged();
            }   
        }
    }
    
    private string? _tableId;
    public string? tableId
    {
        get { return _tableId;}
        set
        {
            if (_tableId != value)
            {
                _tableId = value;
                OnPropertyChanged();
            }   
        }
    }
    
    private string? _colorType;
    public string? colorType
    {
        get { return _colorType;}
        set
        {
            if (_colorType != value)
            {
                _colorType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrintName));
            }   
        }
    }

    public string? colortypeUi
    {
        get
        {
            return _colorType switch
            {
                "R" => "Red",
                "B" => "Blue",
                _ => "Null"
            };
        }
    }

    private bool? _isMain;
    public bool? isMain
    {
        get {return _isMain;}
        set
        {
            if (_isMain != value)
            {
                _isMain = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrintName));
            }   
        }
    }

    public string? IsMainUi
    {
        get
        {
            return _isMain switch
            {
                true => "Regular",
                false => "Extra",
                _ => "Null"
            };
        }
    }

    private int? _maxCount=0;

    public int? maxCount
    {
        get { return _maxCount;}
        set
        {
            if (_maxCount != value)
            {
                _maxCount = value;
                OnPropertyChanged();
            }   
        }
    }

    private int? _trueState;

    public int? trueState
    {
        get { return _trueState;}
        set
        {
            if (_trueState != value)
            {
                _trueState = value;
                OnPropertyChanged();
            }   
        }
    }
    
    private string? _statusdescreption;

    public string? Statusdescreption
    {
        get { return _statusdescreption;}
        set
        {
            if (_statusdescreption != value)
            {
                _statusdescreption = value;
                OnPropertyChanged();
            }   
        }
    }

    private int? _count;

    public int? count
    {
        get { return _count;}
        set
        {
            if (_count != value)
            {
                _count = value;
                OnPropertyChanged();
            }   
        }
    }

    public int? _action;
    public int? action
    {
        get { return _action;}
        set
        {
            if (_action != value)
            {
                _action = value;
                OnPropertyChanged();
            }   
        }
    }
    
    private string? _deadline;
    public string? deadline
    {
        get { return _deadline;}
        set
        {
            if (_deadline != value)
            {
                _deadline = value;
                OnPropertyChanged();
                OnPropertyChanged(DateRange);
            }   
        }
    }
    
    private string? _createTime;
    public string? createTime
    {
        get
        {
            if (string.IsNullOrEmpty(_createTime)) return null;
            return _createTime.Length >= 10 ? _createTime.Substring(0, 10) : _createTime;
        }
        set
        {
            if (_createTime != value)
            {
                _createTime = value;
                OnPropertyChanged();
                OnPropertyChanged(DateRange);
            }   
        }
    }
    
    public string DateRange
    {
        get
        {
            return $"{createTime:yyyy-MM-dd} - {deadline:yyyy-MM-dd}";
        }
    }
    
    private string? _detailTime;
    public string? detailTime
    {
        get
        {
            if (string.IsNullOrEmpty(_createTime)) return null;

            // 嘗試各種常見格式 (含 ISO 形式)
            var raw = _createTime.Replace('T', ' '); // 將 ISO 的 'T' 換成空白方便解析
            if (DateTime.TryParse(raw, out var dt))
                return dt.ToString("HH:mm:ss");

            // 解析失敗：嘗試抓出空白後的部分 (日期 與 時間 分隔)
            int space = raw.IndexOf(' ');
            if (space >= 0 && space + 1 < raw.Length)
            {
                var timePart = raw[(space + 1)..].TrimEnd('Z'); // 去掉可能的尾端 Z
                // 若時間部分含毫秒或時區再自行裁剪
                // 簡單裁剪到前 8 碼 (HH:mm:ss)
                if (timePart.Length >= 8)
                    return timePart.Substring(0, 8);
                return timePart;
            }

            return null;
        }
        
    }
    
    private string? _updateTime;
    public string? updateTime
    {
        get { return _updateTime;}
        set
        {
            if (_updateTime != value)
            {
                _updateTime = value;
                OnPropertyChanged();
            }   
        }
    }
    
    private string? _deleteReason;
    public string? deleteReason
    {
        get { return _deleteReason;}
        set
        {
            if (_deleteReason != value)
            {
                _deleteReason = value;
                OnPropertyChanged();
            }   
        }
    }
    
    private long? _shoe;
    public long? shoe
    {
        get { return _shoe;}
        set
        {
            if (_shoe != value)
            {
                _shoe = value;
                OnPropertyChanged();
            }   
        }
    }
    
    //private string? _printName;
    public string? PrintName
    {
        get
        {

            // return if null or empty
            if (string.IsNullOrEmpty(tableId) || string.IsNullOrEmpty(colorType) || isMain == null)
            {
                return null;
            }

            //Color R or B
            string colorCode;
            if (string.Equals(colorType, "R", System.StringComparison.OrdinalIgnoreCase))
            {
                colorCode = "R";
            }
            else if (string.Equals(colorType, "B", System.StringComparison.OrdinalIgnoreCase))
            {
                colorCode = "B";
            }
            else
            {
                colorCode = "?"; // 對未知顏色給予標記
            }
            
            //MainType M or B
            string ismaincode = isMain == true ? "" : "E";
            
            return tableId + colorCode + ismaincode;
        }
        // set
        // {
        //     if (_printName != value)
        //     {
        //         _printName = value;
        //         OnPropertyChanged();
        //     }   
        // }
    }
    
    private string? _tagMessage;
    public string? TagMessage
    {
        get { return _tagMessage; }
        set { _tagMessage = value; }
    }

    private int? _serialNo;
    public int? serialNo
    {
        get { return _serialNo;}
        set
        {
            if (_serialNo != value)
            {
                _serialNo = value;
                OnPropertyChanged();
            }   
        }
    }

    private string? _errMsg;
    public string? errMsg
    {
        get { return _errMsg; }
        set
        {
            if (_errMsg != value)
            {
                _errMsg = value;
                OnPropertyChanged();
            }   
        }
    }

}