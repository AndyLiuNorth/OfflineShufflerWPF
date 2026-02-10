using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Model;

public class MemberInfoWebServiceModel:INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    // private string? _username;
    // public string? Username
    // {
    //     get { return _username; }
    //     set
    //     {
    //         if (_username != value)
    //         {
    //             _username = value;
    //             OnPropertyChanged();
    //         }
    //     }
    // }
    
    // User Information
    private int? _id = -1;
    public int? id
    {
        get { return _id; }
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }
    
    private string? _idui = "null";
    public string? idui
    {
        get { return _idui; }
        set { _idui = value;OnPropertyChanged(); }
    }
    public string? siteDomainName { get; set; }
    public string? roleName { get; set; }
    public string? stateName { get; set; }
    public GameTablelist? data { get; set; }
    public int code { get; set; } = 200;
    public string? message { get; set; }
    
}

public class GameTablelist
{
    [JsonPropertyName("tables")]
    public List<string>? tables { get; set; }
}

public class EngineerMemberInfoWebServiceModel
{
    // webservice response model
    
    // {
    //     "id": 2000018, // 有機會變動, 會根據需求調整
    //     "memberName": "zachopop",
    //     "memberID": 439,
    //     "siteDomainName": "Global",
    //     "roleName": "Supervisor",
    //     "stateName": "PasswordDefault"
    // }
    
    // {
    //     "id": 439,
    //     "memberName": "zachopop",
    //     "memberID": 439,
    //     "siteDomainName": "Global",
    //     "roleName": "Supervisor",
    //     "stateName": "PasswordDefault"
    // }
    
    public int? id { get; set; }
    public string? memberName { get; set; }
    public int? code { get; set; }
    public int? memberID { get; set; }
    public string? message { get; set; }
    public string? siteDomainName { get; set; }
    public string? roleName { get; set; }
    public string? stateName { get; set; }
    
}
