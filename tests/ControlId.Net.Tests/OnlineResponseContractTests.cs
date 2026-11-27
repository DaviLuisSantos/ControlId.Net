using System.Text.Json.Nodes;
using ControlId.Access;
using ControlId.Access.Online;
#if NET8_0_OR_GREATER
using System.Net;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
#endif

namespace ControlId.Tests;

/// <summary>
/// Contrato JSON das respostas do modo online (#64): os campos que o equipamento lê em <c>result</c> e o formato
/// exato de <c>actions</c> para porta, SecBox e catraca. A comparação é semântica (<see cref="JsonNode.DeepEquals"/>):
/// a ordem das propriedades não importa, mas nomes, tipos, valores e a ordem das ações importam.
/// </summary>
public class OnlineResponseContractTests
{
    [Fact]
    public void Grant_response_matches_the_equipment_contract()
    {
        var grant = OnlineAuthorization.Grant(
            6,
            "Neal Caffrey",
            DeviceAction.ReleaseTurnstile(TurnstileDirection.Clockwise),
            DeviceAction.OpenSecBox(65793, reason: 3),
            DeviceAction.OpenDoor(1));
        grant.PortalId = 1;
        grant.Message = "Acesso liberado";
        grant.UserImage = true;
        grant.UserImageHash = "3f786850e387550fdab836ed7e6dc881de23001b";

        OnlineJson.AssertMatches(
            """
            {
              "result": {
                "event": 7,
                "user_id": 6,
                "portal_id": 1,
                "message": "Acesso liberado",
                "user_name": "Neal Caffrey",
                "user_image": true,
                "user_image_hash": "3f786850e387550fdab836ed7e6dc881de23001b",
                "actions": [
                  { "action": "catra", "parameters": "allow=clockwise" },
                  { "action": "sec_box", "parameters": "id=65793, reason=3" },
                  { "action": "door", "parameters": "door=1" }
                ]
              }
            }
            """,
            grant.ToResponseJson());
    }

    [Fact]
    public void Deny_response_matches_the_equipment_contract()
    {
        var deny = OnlineAuthorization.Deny(6, "Neal Caffrey", "Fora do horário");
        deny.PortalId = 1;
        deny.UserImage = false;
        deny.UserImageHash = "3f786850e387550fdab836ed7e6dc881de23001b";

        OnlineJson.AssertMatches(
            """
            {
              "result": {
                "event": 6,
                "user_id": 6,
                "portal_id": 1,
                "message": "Fora do horário",
                "user_name": "Neal Caffrey",
                "user_image": false,
                "user_image_hash": "3f786850e387550fdab836ed7e6dc881de23001b"
              }
            }
            """,
            deny.ToResponseJson());
    }

    [Fact]
    public void Not_identified_response_matches_the_equipment_contract()
    {
        var notIdentified = OnlineAuthorization.NotIdentified("Credencial não reconhecida");
        notIdentified.PortalId = 1;

        OnlineJson.AssertMatches(
            """
            {
              "result": {
                "event": 3,
                "portal_id": 1,
                "message": "Credencial não reconhecida"
              }
            }
            """,
            notIdentified.ToResponseJson());
    }
}

#if NET8_0_OR_GREATER
/// <summary>
/// Fallback configurado (#64): quando o handler falha, o equipamento recebe o <see cref="ControlIdOnlineOptions.FallbackAuthorization"/>
/// como resposta 200, no mesmo formato de uma decisão normal.
/// </summary>
public sealed class OnlineFallbackResponseContractTests : IAsyncDisposable
{
    private WebApplication? _app;

    private async Task<HttpClient> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControlIdOnline((_, _) => throw new InvalidOperationException("banco fora do ar"));
        builder.Services.Configure<ControlIdOnlineOptions>(o =>
            o.FallbackAuthorization = OnlineAuthorization.NotIdentified("mensagem"));

        _app = builder.Build();
        _app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
        await _app.StartAsync();
        return _app.GetTestClient();
    }

    [Fact]
    public async Task Configured_not_identified_fallback_is_the_response_when_the_handler_throws()
    {
        var http = await StartAsync();

        var response = await http.PostAsync("/api/new_card.fcgi", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("device_id", "1"),
            new KeyValuePair<string, string>("card_value", "123"),
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        OnlineJson.AssertMatches(
            """{"result":{"event":3,"message":"mensagem"}}""",
            await response.Content.ReadAsStringAsync());
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

internal static class OnlineJson
{
    public static void AssertMatches(string expected, string actual)
    {
        var expectedNode = JsonNode.Parse(expected);
        var actualNode = JsonNode.Parse(actual);
        Assert.True(
            JsonNode.DeepEquals(expectedNode, actualNode),
            $"JSON fora do contrato do modo online.{Environment.NewLine}" +
            $"Esperado: {expectedNode?.ToJsonString()}{Environment.NewLine}" +
            $"Obtido:   {actualNode?.ToJsonString()}");
    }
}
