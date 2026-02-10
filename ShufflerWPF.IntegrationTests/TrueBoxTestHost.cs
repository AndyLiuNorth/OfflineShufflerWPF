using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using ShufflerWPF.SingleTon;
using ShufflerWPF.Model;

namespace ShufflerWPF.IntegrationTests;

public class TrueBoxTestHost
{
    private static IHost? _host;
    private static readonly object _lock = new();

    public static async Task EnsureStartedAsync()
    {
        lock (_lock)
        {
            if (_host != null) return;
            
            _host = Host.CreateDefaultBuilder().ConfigureWebHostDefaults(
                webBuilder =>
                {
                    webBuilder.UseTestServer();
                    webBuilder.Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                                {
                                    
                                    // Simulate /truebox/action Post method endpoint
                                    endpoints.MapPost("/truebox/action", async context =>
                                    {
                                        var form = await context.Request.ReadFormAsync();
                                        var enc = form["encryptedMessage"].ToString();
                                        context.Response.ContentType = "application/json";

                                        if (string.IsNullOrEmpty((enc)))
                                        {
                                            await context.Response.WriteAsync("{\"code\":400,\"errMsg\":\"missing encryptedMessage\"}");
                                            return;
                                        }
                                        
                                        int code = (enc.Contains("\"action\":2") || enc.Contains("\"action\":-1")) ? 204 : 201;
                                        string trueId = "it-" + Guid.NewGuid().ToString("N")[..8];
                                        await context.Response.WriteAsync($"{{\"code\":{code},\"errMsg\":\"\",\"data\":{{\"trueId\":\"{trueId}\"}}}}");
                                        
                                    });
                                    
                                    // Simulate /truebox/{trueId} Get method endpoint
                                    endpoints.MapPost("/truebox/get", async context =>
                                    {
                                        context.Response.ContentType = "application/json";
                                        await context.Response.WriteAsync("{\"code\":201,\"errMsg\":\"\",\"data\":{\"trueId\":\"seed-id\",\"tableId\":\"T01\",\"colorType\":\"R\",\"isMain\":true,\"count\":5,\"updater\":123,\"state\":0}}");
                                    });
                                }
                            );
                        }
                    );
                }).Build();
            
            _host.Start();
        }
        // 取得內嵌伺服器位址並覆寫設定
        var baseAddr = _host.GetTestServer().BaseAddress.ToString().TrimEnd('/');
        //DataCenter.MyShufflerSettings.ActionTrueIdApi = baseAddr + "/truebox/action";
        //ataCenter.MyShufflerSettings.GetTrueIdApi = baseAddr + "/truebox/get";
        DataCenter.CurrentMember = new MemberInfoWebServiceModel { id = 123, roleName = "Tester" };
        await Task.CompletedTask;
    }

    public static HttpClient Client => _host?.GetTestServer().CreateClient() ?? throw new InvalidOperationException("Host not started. Call EnsureStartedAsync first.");

}