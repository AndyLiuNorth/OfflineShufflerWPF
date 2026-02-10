using System.Text.Json;

namespace ShufflerWPF.Model;

public class APIProxyJsonModel
{
    public int proxyStatusCode { get; set; }
    public int endpointStatusCode { get; set; }
    public int durationMs { get; set; }
    public string upstream { get; set; } = string.Empty;
    public Payload? payload { get; set; }
    public string? errorCode { get; set; }
    public string? message { get; set; }
}


public class Payload
{
    public string? externalRaw { get; set; }
    public string? normalized { get; set; }
}

/// <summary>
/// below is different model for shuffler api response
/// </summary>
public class ShufflerJsonModel
{
    public int code { get; set; }
    public TrueBoxModel? data { get; set; }
    public string? errMsg { get; set; }
}