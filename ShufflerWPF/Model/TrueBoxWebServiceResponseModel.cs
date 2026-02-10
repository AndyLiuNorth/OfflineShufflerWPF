using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Model;

public class TrueBoxWebServiceResponseModel
{
    // webservice response model
    public TrueBoxModel data { get; set; } = new  TrueBoxModel();
    public int? code { get; set; }
    public string? errMsg { get; set; }
    
}
// public class TrueInformationList
// {
//     public string serialNo { get; set; }
//     public string trueId{ get; set; }
//     public string domain{ get; set; }
//     public string tableId{ get; set; }
//     public string colorType{ get; set; }
//     public bool isMain{ get; set; }
//     public int count{ get; set; }
//     public int maxCount{ get; set; }
//     public int action{ get; set; }
//     public string deadline{ get; set; }
//     public string updateTime{ get; set; }
//     public string updater{ get; set; }
//     public string deleteReason{ get; set; }
//     public int state{ get; set; }
// }
