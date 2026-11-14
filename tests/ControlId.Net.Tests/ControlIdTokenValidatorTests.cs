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

    private static ControlIdTokenOptions Opt(string? token = "geral", Func<long, string?>? device = null)
        => new() { Token = token, DeviceToken = device };

    [Fact]
    public void Without_metadata_allows()
    {
        var context = Context(null, null);
        Assert.True(ControlIdTokenValidator.Authorize(context, 1));
        Assert.Equal(200, context.Response.StatusCode);
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
    public void Device_token_wins_over_general_and_null_falls_back()
    {
        var options = Opt("geral", id => id == 1 ? "t1" : null);
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
