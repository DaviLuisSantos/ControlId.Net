using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Online;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Tests;

public sealed class AspNetCoreOnlineTests : IAsyncDisposable
{
    private readonly RecordingHandler _handler = new();
    private WebApplication? _app;

    private async Task<HttpClient> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_handler);
        builder.Services.AddControlIdOnline<ScopedHandler>();

        _app = builder.Build();
        _app.MapControlIdOnline("/api");
        await _app.StartAsync();
        return _app.GetTestClient();
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    [Fact]
    public async Task Pro_identification_is_authorized_by_the_handler()
    {
        var http = await StartAsync();

        var response = await http.PostAsync("/api/new_user_identified.fcgi", Form(("device_id", "1"), ("user_id", "6"), ("event", "7"), ("portal_id", "1")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = json.RootElement.GetProperty("result");
        Assert.Equal(7, result.GetProperty("event").GetInt32());
        Assert.Equal("door=1", result.GetProperty("actions")[0].GetProperty("parameters").GetString());
        Assert.Equal(6, Assert.IsType<UserIdentifiedEvent>(Assert.Single(_handler.Identifications)).UserId);
    }

    [Fact]
    public async Task Enterprise_card_is_denied_when_unknown()
    {
        var http = await StartAsync();

        var response = await http.PostAsync("/api/new_card.fcgi", Form(("card_value", "999"), ("device_id", "1")));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, json.RootElement.GetProperty("result").GetProperty("event").GetInt32());
        Assert.Equal(999, Assert.IsType<CardEvent>(Assert.Single(_handler.Identifications)).CardValue);
    }

    [Fact]
    public async Task Biometric_image_reads_query_and_binary_body()
    {
        var http = await StartAsync();
        var content = new ByteArrayContent(new byte[] { 10, 20, 30, 40 });
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var response = await http.PostAsync("/api/new_biometric_image.fcgi?device_id=1&width=2&height=2&session=x", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var biometric = Assert.IsType<BiometricEvent>(Assert.Single(_handler.Identifications));
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, biometric.Data);
        Assert.Equal(2, biometric.Height);
    }

    [Fact]
    public async Task Exit_button_and_alive_answer_empty_ok()
    {
        var http = await StartAsync();

        var rex = await http.PostAsync(
            "/api/new_rex_log.fcgi",
            new StringContent("""{"device_id":1,"rex_log":{"time":1700000000,"event":11,"user_id":0,"portal_id":1}}""", Encoding.UTF8, "application/json"));
        var alive = await http.PostAsync("/api/device_is_alive.fcgi", new StringContent("""{"access_logs":5}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, rex.StatusCode);
        Assert.Equal(string.Empty, await rex.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        Assert.Equal(11, Assert.Single(_handler.ExitButtons).Log!.Event);
    }

    [Fact]
    public async Task Online_enroll_results_reach_the_handler()
    {
        var http = await StartAsync();

        var card = await http.PostAsync(
            "/api/card_create.fcgi",
            new StringContent("""{"user_id": 1, "card_value": 132456789, "device_id": 935107}""", Encoding.UTF8, "application/json"));
        var face = await http.PostAsync(
            "/api/face_create.fcgi",
            new StringContent("""{"user_id": 1, "device_id": 935107, "face": {"image": "AQID", "width": 1, "height": 3}}""", Encoding.UTF8, "application/json"));
        var invalid = await http.PostAsync("/api/pin_create.fcgi", new StringContent("{oops", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        Assert.Equal(string.Empty, await card.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, face.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        Assert.Collection(
            _handler.Enrollments,
            e => Assert.Equal(132456789, e.Result.CardValue),
            e => Assert.Equal(new byte[] { 1, 2, 3 }, e.Result.Face!.Image));
    }

    [Fact]
    public async Task User_image_is_served_by_the_handler()
    {
        var http = await StartAsync();

        var found = await http.GetAsync("/api/user_get_image.fcgi?user_id=6");
        var missing = await http.GetAsync("/api/user_get_image.fcgi?user_id=7");
        var invalid = await http.GetAsync("/api/user_get_image.fcgi");

        Assert.Equal(new byte[] { 0xFF, 0xD8 }, await found.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>Guarda o que chegou; singleton para o teste inspecionar.</summary>
    private sealed class RecordingHandler
    {
        public List<OnlineIdentificationEvent> Identifications { get; } = new();

        public List<ExitButtonEvent> ExitButtons { get; } = new();

        public List<OnlineEnrollEvent> Enrollments { get; } = new();
    }

    /// <summary>Regras de exemplo: usuário 6 e cartões conhecidos passam.</summary>
    private sealed class ScopedHandler : IOnlineAccessHandler
    {
        private readonly RecordingHandler _recorder;

        public ScopedHandler(RecordingHandler recorder) => _recorder = recorder;

        public Task<OnlineAuthorization> AuthorizeAsync(OnlineIdentificationEvent identification, CancellationToken cancellationToken)
        {
            _recorder.Identifications.Add(identification);
            return Task.FromResult(identification switch
            {
                UserIdentifiedEvent { UserId: 6 } user => OnlineAuthorization.Grant(user.UserId, "Neal Caffrey", DeviceAction.OpenDoor(1)),
                CardEvent { CardValue: 123 } => OnlineAuthorization.Grant(6, "Neal Caffrey", DeviceAction.OpenDoor(1)),
                CardEvent => OnlineAuthorization.NotIdentified(),
                BiometricEvent => OnlineAuthorization.NotIdentified(),
                _ => OnlineAuthorization.Deny(),
            });
        }

        public Task OnExitButtonAsync(ExitButtonEvent exitButton, CancellationToken cancellationToken)
        {
            _recorder.ExitButtons.Add(exitButton);
            return Task.CompletedTask;
        }

        public Task OnRemoteEnrollAsync(OnlineEnrollEvent enrollment, CancellationToken cancellationToken)
        {
            _recorder.Enrollments.Add(enrollment);
            return Task.CompletedTask;
        }

        public Task<byte[]?> GetUserImageAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(userId == 6 ? new byte[] { 0xFF, 0xD8 } : null);
    }
}
