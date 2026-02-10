using System.Net;
using System.Net.Http;
namespace ShufflerWPF.Manager;

public class WebserviceModel
{
    private HttpClient _httpClient= new HttpClient(); 

    private HttpClient HttpClient
    {
        get{ return _httpClient; }
        set{ _httpClient = value; }
    }
    
    private string _url= string.Empty;

    public string Url
    {
        get{ return _url; }
        set{ _url = value; }
    }

    private string _jsonReuestString= string.Empty;

    public string JsonReuestString
    {
        get{ return _jsonReuestString; }   
        set{ _jsonReuestString = value;}
    }
    
    private string _jsonResponseString;

    public string JsonResponseString
    {
        get{ return _jsonResponseString; }   
        set{ _jsonResponseString = value;}
    }
}