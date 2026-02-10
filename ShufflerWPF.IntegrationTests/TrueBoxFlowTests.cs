using System.Net.Http;

namespace ShufflerWPF.IntegrationTests;

[Collection("TrueBoxHost")]
public class TrueBoxFlowTests
{
    [Fact]
    public async Task Action_demoNormal201_TrueId()
    {
        
        await TrueBoxTestHost.EnsureStartedAsync();
        
        var client = TrueBoxTestHost.Client;
        var form = new FormUrlEncodedContent(
            new[]
            {
                new KeyValuePair<string, string>("encryptrdMessage", "{\"action\":1}")
            });

        var resp = await client.PostAsync("/truebox/action", form);
        string json = await resp.Content.ReadAsStringAsync();
        
        Assert.True(resp.IsSuccessStatusCode);
        Assert.Contains("\"code\":201", json);
        Assert.Contains("\"trueId\"", json);
    }
    
    [Fact]
    public async Task Get_demoNormal201_TrueId()
    {
        
        await TrueBoxTestHost.EnsureStartedAsync();
        
        var client = TrueBoxTestHost.Client;

        var resp = await client.PostAsync("/truebox/get", null);
        string json = await resp.Content.ReadAsStringAsync();
        
        Assert.True(resp.IsSuccessStatusCode);
        Assert.Contains("\"code\":201", json);
        Assert.Contains("\"seed-id\"", json);
    }
}