using System.Net;
using System.Text.RegularExpressions;
using ControlId.Access;

namespace ControlId.Tests;

public class ImageBatchingTests
{
    private static ControlIdDeviceOptions Options(Action<ControlIdDeviceOptions>? configure = null)
    {
        var options = new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.5") };
        configure?.Invoke(options);
        return options;
    }

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    private static UserImage Photo(long userId, int bytes = 300 * 1024) => new(userId, TestImages.Jpeg(640, 480, bytes));

    private static string Results(params long[] userIds) =>
        "{\"results\":[" + string.Join(",", userIds.Select(id => $"{{\"user_id\":{id},\"success\":true}}")) + "]}";

    private static int UserIdsIn(string body) => Regex.Matches(body, "\"user_id\"").Count;

    [Fact]
    public async Task Batch_over_1MB_becomes_several_requests_and_results_keep_the_input_order()
    {
        // Cada foto tem ~400 KB em Base64: duas cabem em 1 MiB, três não.
        var device = LoggedDevice()
            .OnJson("user_set_image_list.fcgi", Results(1, 2))
            .OnJson("user_set_image_list.fcgi", Results(3, 4))
            .OnJson("user_set_image_list.fcgi", Results(5));
        using var client = new AccessClient(Options(), device.CreateClient());

        var results = await client.SetUserImagesAsync(Enumerable.Range(1, 5).Select(i => Photo(i)));

        var requests = device.RequestsTo("user_set_image_list.fcgi").ToList();
        Assert.Equal(3, requests.Count);
        Assert.Equal(new[] { 2, 2, 1 }, requests.Select(r => UserIdsIn(r.Body)));
        Assert.Equal(new long?[] { 1, 2, 3, 4, 5 }, results.Select(r => r.UserId));
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task Small_batch_stays_in_a_single_request()
    {
        var device = LoggedDevice().OnJson("user_set_image_list.fcgi", Results(1, 2, 3));
        using var client = new AccessClient(Options(), device.CreateClient());

        var results = await client.SetUserImagesAsync(Enumerable.Range(1, 3).Select(i => Photo(i, 100)));

        Assert.Single(device.RequestsTo("user_set_image_list.fcgi"));
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task Batch_is_split_by_the_photo_count_limit()
    {
        var device = LoggedDevice()
            .OnJson("user_set_image_list.fcgi", Results(1, 2))
            .OnJson("user_set_image_list.fcgi", Results(3, 4))
            .OnJson("user_set_image_list.fcgi", Results(5));
        using var client = new AccessClient(Options(o => o.MaxImagesPerBatch = 2), device.CreateClient());

        var results = await client.SetUserImagesAsync(Enumerable.Range(1, 5).Select(i => Photo(i, 100)));

        Assert.Equal(new[] { 2, 2, 1 }, device.RequestsTo("user_set_image_list.fcgi").Select(r => UserIdsIn(r.Body)));
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task Photo_larger_than_the_batch_limit_goes_alone()
    {
        var device = LoggedDevice().OnJson("user_set_image_list.fcgi", Results(1));
        using var client = new AccessClient(Options(o => o.MaxImageBatchBytes = 1000), device.CreateClient());

        await client.SetUserImagesAsync(new[] { Photo(1, 2000), Photo(2, 2000) });

        Assert.Equal(2, device.RequestsTo("user_set_image_list.fcgi").Count());
    }

    [Fact]
    public async Task Batch_refused_by_the_device_falls_back_to_one_photo_at_a_time()
    {
        var device = LoggedDevice()
            .On("user_set_image_list.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid image list","code":3}""")
            .OnJson("user_set_image.fcgi", """{"success":true}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var results = await client.SetUserImagesAsync(new[] { Photo(1, 100), Photo(2, 100), Photo(3, 100) });

        Assert.Single(device.RequestsTo("user_set_image_list.fcgi"));
        var singles = device.RequestsTo("user_set_image.fcgi").ToList();
        Assert.Equal(3, singles.Count);
        Assert.Contains("user_id=1&", singles[0].Uri.Query);
        Assert.Contains("user_id=3&", singles[2].Uri.Query);
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task Fallback_only_affects_the_refused_batch()
    {
        // Lote 1 (fotos 1 e 2) recusado; lote 2 (fotos 3 e 4) aceito.
        var device = LoggedDevice()
            .On("user_set_image_list.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid image list","code":3}""")
            .OnJson("user_set_image_list.fcgi", Results(3, 4))
            .OnJson("user_set_image.fcgi", """{"success":true}""");
        using var client = new AccessClient(Options(o => o.MaxImagesPerBatch = 2), device.CreateClient());

        var results = await client.SetUserImagesAsync(Enumerable.Range(1, 4).Select(i => Photo(i, 100)));

        Assert.Equal(2, device.RequestsTo("user_set_image_list.fcgi").Count());
        Assert.Equal(2, device.RequestsTo("user_set_image.fcgi").Count());
        Assert.Equal(4, results.Count);
        Assert.Equal(new long?[] { null, null, 3, 4 }, results.Select(r => r.UserId));
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task Fallback_reports_a_photo_the_device_also_refuses_as_a_failed_result()
    {
        var device = LoggedDevice()
            .On("user_set_image_list.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid image list","code":3}""")
            .OnJson("user_set_image.fcgi", """{"success":true}""")
            .On("user_set_image.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid image","code":4}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var results = await client.SetUserImagesAsync(new[] { Photo(1, 100), Photo(2, 100) });

        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Equal(2, results[1].UserId);
        Assert.Equal("Invalid image", Assert.Single(results[1].Errors).Message);
    }

    [Fact]
    public async Task Command_not_supported_does_not_fall_back()
    {
        var device = LoggedDevice()
            .On("user_set_image_list.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid command","code":1}""")
            .OnJson("user_set_image.fcgi", """{"success":true}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => client.SetUserImagesAsync(new[] { Photo(1, 100) }));

        Assert.Empty(device.RequestsTo("user_set_image.fcgi"));
    }
}
