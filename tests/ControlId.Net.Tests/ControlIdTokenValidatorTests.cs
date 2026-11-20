#if NET8_0_OR_GREATER
using ControlId.AspNetCore;
using ControlId.AspNetCore.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace ControlId.Tests;

public sealed class ControlIdTokenValidatorTests
{
    private static DefaultHttpContext Context(string? authorization, ControlIdTokenOptions? options)
    {
        var context = new DefaultHttpContext();
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        if (options is not null)
        {
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(options), "teste"));
        }

        return context;
    }

    private static ControlIdTokenOptions Opt(string? token = "geral", Func<long, string?>? device = null, bool fallback = false)
        => new() { Token = token, DeviceToken = device, AllowTokenFallback = fallback };

    private static DefaultHttpContext WithMetadata(string? authorization, params object[] metadata)
    {
        var context = Context(authorization, null);
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(metadata), "teste"));
        return context;
    }

    private static object Anonymous()
    {
        var builder = new FakeBuilder();
        builder.AllowAnonymousControlIdCallbacks();
        return builder.Build().Metadata.GetMetadata<AllowAnonymousControlIdCallbacksMetadata>()!;
    }

    [Fact]
    public void Without_metadata_is_closed()
    {
        var context = Context("Bearer qualquer", null);
        Assert.Equal(CallbackSecurity.Closed, ControlIdTokenValidator.GetSecurity(context, out _));
        Assert.False(ControlIdTokenValidator.Authorize(context, 1));
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public void Anonymous_opt_in_allows_without_header()
    {
        var context = WithMetadata(null, Anonymous());
        Assert.Equal(CallbackSecurity.Anonymous, ControlIdTokenValidator.GetSecurity(context, out _));
        Assert.True(ControlIdTokenValidator.Authorize(context, 1));
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public void Token_wins_over_anonymous_opt_in_in_any_order()
    {
        Assert.False(ControlIdTokenValidator.Authorize(WithMetadata(null, Anonymous(), Opt()), 1));
        Assert.False(ControlIdTokenValidator.Authorize(WithMetadata(null, Opt(), Anonymous()), 1));
        Assert.True(ControlIdTokenValidator.Authorize(WithMetadata("Bearer geral", Opt(), Anonymous()), 1));
        Assert.Equal(CallbackSecurity.SharedToken, ControlIdTokenValidator.GetSecurity(WithMetadata(null, Anonymous(), Opt()), out _));
        Assert.Equal(CallbackSecurity.DeviceToken, ControlIdTokenValidator.GetSecurity(WithMetadata(null, Opt(device: _ => "x")), out _));
    }

    [Fact]
    public void Previous_token_is_accepted_with_the_current_one()
    {
        var options = new ControlIdTokenOptions { Token = "novo", PreviousToken = "antigo" };
        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer novo", options), 1));
        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer antigo", options), 1));
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer outro", options), 1));
        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer antigo", new ControlIdTokenOptions { PreviousToken = "antigo" }), 1));
    }

    [Fact]
    public void Device_token_is_not_called_without_a_bearer_header()
    {
        var calls = 0;
        var options = Opt(null, id =>
        {
            calls++;
            return "t1";
        });

        Assert.False(ControlIdTokenValidator.Authorize(Context(null, options), 1));
        Assert.False(ControlIdTokenValidator.Authorize(Context("Basic dDE=", options), 1));
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer ", options), 1));
        Assert.Equal(0, calls);

        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer t1", options), 1));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Device_token_without_fallback_refuses_the_shared_token()
    {
        var options = Opt("geral", id => id == 1 ? "t1" : null);
        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer t1", options), 1));
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer geral", options), 1));
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer geral", options), 2));
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer geral", options), null));
    }

    [Fact]
    public void Missing_header_returns_401_with_challenge()
    {
        var context = Context(null, Opt());
        Assert.False(ControlIdTokenValidator.Authorize(context, 1));
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public void Basic_scheme_is_rejected() =>
        Assert.False(ControlIdTokenValidator.Authorize(Context("Basic geral", Opt()), 1));

    [Fact]
    public void Wrong_token_is_rejected() =>
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer outro", Opt()), 1));

    [Fact]
    public void Right_token_is_accepted() =>
        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer geral", Opt()), 1));

    [Fact]
    public void Token_of_another_device_is_rejected()
    {
        var options = Opt(null, id => id == 1 ? "t1" : id == 2 ? "t2" : null);
        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer t1", options), 1));
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer t2", options), 1));
    }

    [Fact]
    public void Device_token_wins_over_general_and_null_falls_back_when_allowed()
    {
        var options = Opt("geral", id => id == 1 ? "t1" : null, fallback: true);
        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer t1", options), 1));
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer geral", options), 1));
        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer geral", options), 2));
        Assert.True(ControlIdTokenValidator.Authorize(Context("Bearer geral", options), null));
    }

    [Fact]
    public void Nothing_configured_fails()
    {
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer x", Opt(null)), 1));
        Assert.False(ControlIdTokenValidator.Authorize(Context("Bearer ", Opt("")), 1));
    }

    [Fact]
    public void Extension_adds_readable_metadata()
    {
        var builder = new FakeBuilder();
        builder.RequireControlIdToken("abc");
        builder.RequireControlIdToken(o => o.DeviceToken = _ => "z");

        var endpoint = builder.Build();
        var all = endpoint.Metadata.GetOrderedMetadata<ControlIdTokenOptions>();
        Assert.Equal(2, all.Count);
        Assert.Equal("abc", all[0].Token);
        Assert.NotNull(all[1].DeviceToken);
        Assert.Throws<ArgumentException>(() => builder.RequireControlIdToken(""));
    }

    [Fact]
    public void Anonymous_extension_adds_readable_metadata()
    {
        var builder = new FakeBuilder();
        Assert.Same(builder, builder.AllowAnonymousControlIdCallbacks());

        var endpoint = builder.Build();
        Assert.NotNull(endpoint.Metadata.GetMetadata<AllowAnonymousControlIdCallbacksMetadata>());
        Assert.Throws<ArgumentNullException>(() => ((FakeBuilder)null!).AllowAnonymousControlIdCallbacks());
    }

    private sealed class FakeBuilder : IEndpointConventionBuilder
    {
        private readonly List<Action<EndpointBuilder>> _conventions = new();

        public void Add(Action<EndpointBuilder> convention) => _conventions.Add(convention);

        public void Finally(Action<EndpointBuilder> finallyConvention) { }

        public Endpoint Build()
        {
            var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0);
            foreach (var convention in _conventions)
            {
                convention(builder);
            }

            return builder.Build();
        }
    }
}
#endif
