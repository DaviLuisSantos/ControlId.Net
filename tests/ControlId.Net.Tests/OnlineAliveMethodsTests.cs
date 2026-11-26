#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ControlId.Access.Online;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Tests;

/// <summary><c>device_is_alive.fcgi</c> e <c>session_is_valid.fcgi</c> respondem em GET e em POST, com a mesma regra de token (issue #51).</summary>
public sealed class OnlineAliveMethodsTests : IAsyncDisposable
{
    private const string Token = "s3cret";

    private WebApplication? _app;

    private async Task<HttpClient> StartAsync(Action<RouteGroupBuilder> configureGroup)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControlIdOnline((_, _) => Task.FromResult(OnlineAuthorization.Deny()));

        _app = builder.Build();
        configureGroup(_app.MapControlIdOnline("/api"));
        await _app.StartAsync();
        return _app.GetTestClient();
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string? bearer = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return request;
    }

    [Theory]
    [InlineData("GET", "device_is_alive")]
    [InlineData("POST", "device_is_alive")]
    [InlineData("GET", "session_is_valid")]
    [InlineData("POST", "session_is_valid")]
    public async Task Alive_routes_answer_200_in_get_and_post(string method, string route)
    {
        var http = await StartAsync(group => group.AllowAnonymousControlIdCallbacks());

        var response = await http.SendAsync(Request(new HttpMethod(method), $"/api/{route}.fcgi?device_id=7"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "device_is_alive")]
    [InlineData("POST", "device_is_alive")]
    [InlineData("GET", "session_is_valid")]
    [InlineData("POST", "session_is_valid")]
    public async Task Alive_routes_apply_the_same_token_rule_in_get_and_post(string method, string route)
    {
        var http = await StartAsync(group => group.RequireControlIdToken(Token));
        var url = $"/api/{route}.fcgi?device_id=7";

        var without = await http.SendAsync(Request(new HttpMethod(method), url));
        var wrong = await http.SendAsync(Request(new HttpMethod(method), url, "errado"));
        var right = await http.SendAsync(Request(new HttpMethod(method), url, Token));

        Assert.Equal(HttpStatusCode.Unauthorized, without.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task Alive_routes_are_closed_without_token_or_anonymous_opt_in(string method)
    {
        var http = await StartAsync(_ => { });

        var response = await http.SendAsync(Request(new HttpMethod(method), "/api/device_is_alive.fcgi?device_id=7"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
#endif
