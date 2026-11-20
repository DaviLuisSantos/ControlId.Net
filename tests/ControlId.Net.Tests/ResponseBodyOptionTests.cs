using System.Net;
using System.Text.Json;
using ControlId.Internal;

namespace ControlId.Tests;

/// <summary><see cref="ControlIdDeviceOptions.ExceptionResponseBody"/> decide o que vai para <see cref="ControlIdException.ResponseBody"/>.</summary>
public class ResponseBodyOptionTests
{
    private const string ErrorJson = """{"error":"Invalid door","code":7}""";
    private const string Html = "<html><body>Bad gateway</body></html>";

    private static ControlIdDeviceOptions Options(ExceptionResponseBody mode) => new()
    {
        BaseAddress = new Uri("http://10.0.0.9"),
        ExceptionResponseBody = mode,
    };

    private static FakeDeviceHandler Device() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public void The_default_keeps_only_the_device_error_json()
    {
        Assert.Equal(ExceptionResponseBody.ErrorOnly, new ControlIdDeviceOptions().ExceptionResponseBody);
    }

    [Theory]
    [InlineData(ExceptionResponseBody.ErrorOnly, true)]
    [InlineData(ExceptionResponseBody.Full, true)]
    [InlineData(ExceptionResponseBody.None, false)]
    public async Task Device_error_json_body(ExceptionResponseBody mode, bool kept)
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.BadRequest, ErrorJson);
        using var transport = new ControlIdTransport(Options(mode), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdDeviceErrorException>(() => transport.PostRawAsync("load_objects.fcgi", null, CancellationToken.None));

        Assert.Equal(kept ? ErrorJson : null, error.ResponseBody);
        Assert.Equal("Invalid door", error.DeviceMessage); // o campo extraído não depende da opção
    }

    [Theory]
    [InlineData(ExceptionResponseBody.ErrorOnly, false)]
    [InlineData(ExceptionResponseBody.Full, true)]
    [InlineData(ExceptionResponseBody.None, false)]
    public async Task Non_json_error_body(ExceptionResponseBody mode, bool kept)
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.BadGateway, Html);
        using var transport = new ControlIdTransport(Options(mode), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdDeviceErrorException>(() => transport.PostRawAsync("load_objects.fcgi", null, CancellationToken.None));

        Assert.Equal(kept ? Html : null, error.ResponseBody);
        Assert.Equal(HttpStatusCode.BadGateway, error.StatusCode);
    }

    [Theory]
    [InlineData(ExceptionResponseBody.ErrorOnly, false)]
    [InlineData(ExceptionResponseBody.Full, true)]
    [InlineData(ExceptionResponseBody.None, false)]
    public async Task Json_that_is_not_the_device_error_shape(ExceptionResponseBody mode, bool kept)
    {
        const string Other = """{"users":[{"id":1,"password":"segredo"}]}""";
        var device = Device().On("load_objects.fcgi", HttpStatusCode.InternalServerError, Other);
        using var transport = new ControlIdTransport(Options(mode), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdDeviceErrorException>(() => transport.PostRawAsync("load_objects.fcgi", null, CancellationToken.None));

        Assert.Equal(kept ? Other : null, error.ResponseBody);
    }

    [Theory]
    [InlineData(ExceptionResponseBody.ErrorOnly, false)]
    [InlineData(ExceptionResponseBody.Full, true)]
    [InlineData(ExceptionResponseBody.None, false)]
    public async Task Invalid_success_body(ExceptionResponseBody mode, bool kept)
    {
        var device = Device().OnJson("load_objects.fcgi", Html);
        using var transport = new ControlIdTransport(Options(mode), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdException>(() => transport.PostAsync<JsonElement>("load_objects.fcgi", null, CancellationToken.None));

        Assert.Equal(kept ? Html : null, error.ResponseBody);
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
    }

    [Theory]
    [InlineData(ExceptionResponseBody.ErrorOnly, false)]
    [InlineData(ExceptionResponseBody.Full, true)]
    [InlineData(ExceptionResponseBody.None, false)]
    public async Task Refused_login_body(ExceptionResponseBody mode, bool kept)
    {
        var device = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.Unauthorized, Html);
        using var transport = new ControlIdTransport(Options(mode), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdAuthenticationException>(() => transport.LoginAsync(CancellationToken.None));

        Assert.Equal(kept ? Html : null, error.ResponseBody);
    }

    [Theory]
    [InlineData(ExceptionResponseBody.ErrorOnly, true)]
    [InlineData(ExceptionResponseBody.Full, true)]
    [InlineData(ExceptionResponseBody.None, false)]
    public async Task Refused_login_with_a_device_error_json(ExceptionResponseBody mode, bool kept)
    {
        const string Body = """{"error":"Invalid login","code":3}""";
        var device = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.Unauthorized, Body);
        using var transport = new ControlIdTransport(Options(mode), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdAuthenticationException>(() => transport.LoginAsync(CancellationToken.None));

        Assert.Equal(kept ? Body : null, error.ResponseBody);
    }

    [Theory]
    [InlineData(ExceptionResponseBody.ErrorOnly, true)]
    [InlineData(ExceptionResponseBody.Full, true)]
    [InlineData(ExceptionResponseBody.None, false)]
    public async Task Not_supported_follows_the_same_rule(ExceptionResponseBody mode, bool kept)
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid command","code":1}""");
        using var transport = new ControlIdTransport(Options(mode), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => transport.PostRawAsync("load_objects.fcgi", null, CancellationToken.None));

        Assert.Equal(kept, error.ResponseBody is not null);
    }

    [Fact]
    public void An_undefined_value_is_rejected_by_validation()
    {
        var options = Options((ExceptionResponseBody)99);

        Assert.Throws<ArgumentOutOfRangeException>(() => new ControlIdTransport(options, new FakeDeviceHandler().CreateClient()));
    }
}
