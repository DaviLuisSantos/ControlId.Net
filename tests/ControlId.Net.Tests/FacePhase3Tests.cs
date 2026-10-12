using System.Text;
using ControlId.Access;

namespace ControlId.Tests;

public class FacePhase3Tests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task SetUserImage_reads_documented_rejection()
    {
        // Resposta de exemplo da documentação: cadastro recusado por baixa nitidez.
        var device = LoggedDevice().OnJson("user_set_image.fcgi", """
            {"user_id":123,"scores":{"bounds_width":397,"horizontal_center_offset":87,"vertical_center_offset":-75,"center_pose_quality":698,"sharpness_quality":105},
             "success":false,"errors":[{"code":8,"message":"Low sharpness"}]}
            """);
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.SetUserImageAsync(123, new byte[] { 1 }, rejectExistingFace: true);

        Assert.False(result.Success);
        Assert.Equal(105, result.Scores!.SharpnessQuality);
        Assert.Equal(FaceImageErrorCode.LowSharpness, Assert.Single(result.Errors).CodeType);
        Assert.EndsWith("&match=1", device.RequestsTo("user_set_image.fcgi").Single().Uri.Query);
        Assert.Contains("Low sharpness", Assert.Throws<ControlIdException>(() => result.EnsureSuccess()).Message);
    }

    [Fact]
    public async Task SetUserImages_returns_one_result_per_photo()
    {
        var device = LoggedDevice().OnJson("user_set_image_list.fcgi", """
            {"results":[{"user_id":1,"success":false,"errors":[{"code":2,"message":"Face not detected"}]},
                        {"user_id":3,"scores":{"bounds_width":104,"horizontal_center_offset":16,"vertical_center_offset":-150,"center_pose_quality":768,"sharpness_quality":1000},"success":true}]}
            """);
        using var client = new AccessClient(Options, device.CreateClient());

        var results = await client.SetUserImagesAsync(new[] { new UserImage(1, new byte[] { 1 }), new UserImage(3, new byte[] { 2 }) }, rejectExistingFaces: true);

        Assert.Equal(FaceImageErrorCode.FaceNotDetected, results[0].Errors[0].CodeType);
        Assert.True(results[1].Success);
        Assert.StartsWith("""{"match":true,""", device.RequestsTo("user_set_image_list.fcgi").Single().Body);
    }

    [Fact]
    public async Task TestUserImage_posts_binary()
    {
        var device = LoggedDevice().OnJson("user_test_image.fcgi", """{"scores":{"bounds_width":120},"success":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True((await client.TestUserImageAsync(Encoding.ASCII.GetBytes("JPG"))).Success);
        var request = device.RequestsTo("user_test_image.fcgi").Single();
        Assert.Equal("application/octet-stream", request.ContentType);
        Assert.Equal("JPG", request.Body);
    }

    [Fact]
    public async Task Image_with_timestamp_and_image_info_use_get()
    {
        var device = LoggedDevice()
            .OnBytes("user_get_image.fcgi", Encoding.UTF8.GetBytes("""{"timestamp":1624997578,"image":"AQID"}"""), "application/json")
            .OnBytes("user_list_images.fcgi", Encoding.UTF8.GetBytes("""{"image_info":[{"user_id":1,"timestamp":1628203752},{"user_id":2,"timestamp":1628203753}]}"""), "application/json");
        using var client = new AccessClient(Options, device.CreateClient());

        var image = await client.GetUserImageWithTimestampAsync(9);
        var info = await client.GetUserImageInfoAsync();

        Assert.Equal(1624997578, image.Timestamp);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.Image);
        Assert.Equal(new long[] { 1, 2 }, info.Select(i => i.UserId));
        Assert.Equal("?session=s&user_id=9&get_timestamp=1", device.RequestsTo("user_get_image.fcgi").Single().Uri.Query);
        var list = device.RequestsTo("user_list_images.fcgi").Single();
        Assert.Equal(HttpMethod.Get, list.Method);
        Assert.Equal("?session=s&get_timestamp=1", list.Uri.Query);
    }

    [Fact]
    public async Task GetUserImages_returns_image_or_error_and_limits_to_100()
    {
        var device = LoggedDevice().OnJson("user_get_image_list.fcgi", """
            {"user_images":[{"id":1,"timestamp":1626890032,"image":"AQID"},{"id":2,"error":{"code":1,"message":"User does not exist"}}]}
            """);
        using var client = new AccessClient(Options, device.CreateClient());

        var images = await client.GetUserImagesAsync(new long[] { 1, 2 });

        Assert.Equal(new byte[] { 1, 2, 3 }, images[0].Image);
        Assert.Equal("User does not exist", images[1].Error!.Message);
        Assert.Equal("""{"user_ids":[1,2]}""", device.RequestsTo("user_get_image_list.fcgi").Single().Body);
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetUserImagesAsync(Enumerable.Range(1, 101).Select(i => (long)i)));
    }

    [Fact]
    public async Task Delete_images_by_list_and_dangling()
    {
        var device = LoggedDevice().OnJson("user_destroy_image.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.DeleteUserImagesAsync(new long[] { 1, 2 });
        await client.DeleteDanglingUserImagesAsync();

        Assert.Equal(new[] { """{"user_ids":[1,2]}""", """{"dangling":true}""" }, device.RequestsTo("user_destroy_image.fcgi").Select(r => r.Body));
    }

    [Fact]
    public async Task Face_templates_extract_enroll_and_destroy()
    {
        var device = LoggedDevice()
            .OnJson("face_template_extract.fcgi", """{"scores":{"bounds_width":113},"face_template":"vRYFPUe+","success":true}""")
            .OnJson("face_template_extract_list.fcgi", """{"results":[{"id":1,"face_template":"abc","success":true},{"id":2,"success":false,"errors":[{"code":1,"message":"Failed"}]}]}""")
            .OnJson("face_template_enroll.fcgi", """{"results":[{"user_id":1,"success":true},{"user_id":2,"errors":[{"code":3,"message":"Face exists","info":{"match_user_id":1,"match_score":969,"match_similarity":4000}}],"success":false}]}""")
            .OnJson("face_template_destroy.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal("vRYFPUe+", (await client.ExtractFaceTemplateAsync(new byte[] { 1 })).FaceTemplate);
        var extracted = await client.ExtractFaceTemplatesAsync(new[] { new FaceTemplateImage(1, new byte[] { 1, 2, 3 }) });
        var enrolled = await client.EnrollFaceTemplatesAsync(
            new[] { new FaceTemplateEnrollment(1, "q/HSO4", DateTimeOffset.FromUnixTimeSeconds(1628727478)) }, rejectExistingFaces: true);
        await client.DeleteFaceTemplatesAsync(new long[] { 1, 2 });
        await client.DeleteAllFaceTemplatesAsync();

        Assert.Equal(2, extracted[1].Id);
        Assert.Equal(1, enrolled[1].Errors[0].Info!.MatchUserId);
        Assert.Equal(4000, enrolled[1].Errors[0].Info!.MatchSimilarity);
        Assert.Equal("""{"face_images":[{"id":1,"image":"AQID"}]}""", device.RequestsTo("face_template_extract_list.fcgi").Single().Body);
        Assert.Equal(
            """{"match":true,"face_templates":[{"user_id":1,"timestamp":1628727478,"face_template":"q/HSO4"}]}""",
            device.RequestsTo("face_template_enroll.fcgi").Single().Body);
        Assert.Equal(new[] { """{"user_ids":[1,2]}""", """{"all":true}""" }, device.RequestsTo("face_template_destroy.fcgi").Select(r => r.Body));
    }

    [Fact]
    public async Task Camera_capture_sync_and_export()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var device = LoggedDevice()
            .OnBytes("save_screenshot.fcgi", png, "image/png")
            .OnJson("template_sync_init.fcgi", "")
            .OnJson("template_sync_end.fcgi", "")
            .OnBytes("export_objects.fcgi", new byte[] { 0x50, 0x4B }, "application/zip");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(png, await client.CaptureCameraAsync(CameraType.Infrared));
        await client.StartTemplateSyncAsync();
        await client.EndTemplateSyncAsync();
        var export = await client.ExportObjectsAsync(new[] { new ExportObjectRequest("users"), new ExportObjectRequest("cards", "value", "user_id") });

        Assert.Equal(new byte[] { 0x50, 0x4B }, export);
        Assert.Equal("""{"frame_type":"camera","camera":"ir"}""", device.RequestsTo("save_screenshot.fcgi").Single().Body);
        Assert.Equal("{}", device.RequestsTo("template_sync_init.fcgi").Single().Body);
        Assert.Single(device.RequestsTo("template_sync_end.fcgi"));
        Assert.Equal(
            """{"objects":[{"object":"users"},{"object":"cards","columns":["value","user_id"]}]}""",
            device.RequestsTo("export_objects.fcgi").Single().Body);
    }
}
