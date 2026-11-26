using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    private static readonly string[] ScreenMediaKioskExtensions = { ".png", ".jpg", ".jpeg", ".svg" };

    /// <inheritdoc/>
    public Task SendSlideshowImageAsync(string filename, byte[] jpeg, CancellationToken cancellationToken = default)
    {
        var query = ScreenMediaFilenameQuery(filename);
        ScreenMediaRequireBytes(jpeg, nameof(jpeg));
        if (jpeg.Length < 3 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 || jpeg[2] != 0xFF)
        {
            throw new ArgumentException("O slideshow só aceita imagens JPEG/JPG.", nameof(jpeg));
        }

        return _transport.PostBytesAsync("send_slideshow_images.fcgi", jpeg, "application/octet-stream", cancellationToken, query);
    }

    /// <inheritdoc/>
    public async Task<ScreenImageList> GetSlideshowImagesAsync(CancellationToken cancellationToken = default) =>
        ScreenMediaParseList(await GetTextAsync("get_slideshow_images_list.fcgi", null, cancellationToken).ConfigureAwait(false), "images");

    /// <inheritdoc/>
    public Task<byte[]> GetSlideshowImageAsync(string filename, CancellationToken cancellationToken = default) =>
        _transport.GetBytesAsync("get_slideshow_image.fcgi", cancellationToken, ScreenMediaFilenameQuery(filename));

    /// <inheritdoc/>
    public Task DeleteSlideshowImageAsync(string filename, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("delete_slideshow_image.fcgi", null, cancellationToken, ScreenMediaFilenameQuery(filename));

    /// <inheritdoc/>
    public Task RemoveAllSlideshowImagesAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, nameof(RemoveAllSlideshowImagesAsync));
        return _transport.PostRawAsync("remove_slideshow_images.fcgi", null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetKioskIconAsync(string filename, byte[] image, CancellationToken cancellationToken = default)
    {
        var query = ScreenMediaFilenameQuery(filename);
        if (!ScreenMediaKioskExtensions.Any(ext => filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("O ícone do quiosque precisa ser .png, .jpg, .jpeg ou .svg.", nameof(filename));
        }

        ScreenMediaRequireBytes(image, nameof(image));
        return _transport.PostBytesAsync("set_kiosk_icon.fcgi", image, "application/octet-stream", cancellationToken, query);
    }

    /// <inheritdoc/>
    public Task<byte[]> GetKioskIconAsync(string filename, CancellationToken cancellationToken = default) =>
        _transport.GetBytesAsync("get_kiosk_icon.fcgi", cancellationToken, ScreenMediaFilenameQuery(filename));

    /// <inheritdoc/>
    public Task DeleteKioskIconAsync(string filename, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("delete_kiosk_icon.fcgi", null, cancellationToken, ScreenMediaFilenameQuery(filename));

    /// <inheritdoc/>
    public async Task<ScreenImageList> GetKioskIconsAsync(CancellationToken cancellationToken = default) =>
        ScreenMediaParseList(await GetTextAsync("get_kiosk_icons_list.fcgi", null, cancellationToken).ConfigureAwait(false), "icons");

    /// <inheritdoc/>
    public Task SetPhoneIconAsync(byte[] png, CancellationToken cancellationToken = default)
    {
        ScreenMediaRequirePng(png, nameof(png));
        return _transport.PostBytesAsync("set_phone_icon.fcgi", png, "application/octet-stream", cancellationToken);
    }

    /// <inheritdoc/>
    public Task<byte[]> GetPhoneIconAsync(CancellationToken cancellationToken = default) =>
        _transport.GetBytesAsync("get_phone_icon.fcgi", cancellationToken);

    /// <inheritdoc/>
    public Task RemovePhoneIconAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("remove_phone_icon.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task MakeSipConferenceAsync(string? target = null, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "make_sip_conference.fcgi",
            string.IsNullOrWhiteSpace(target) ? null : new Dictionary<string, object> { ["target"] = target! },
            cancellationToken);

    /// <inheritdoc/>
    public Task SetStreamingLogoAsync(byte[] png, CancellationToken cancellationToken = default)
    {
        ScreenMediaRequirePng(png, nameof(png));
        if (png.Length >= MaxImageBytes)
        {
            throw new ArgumentException("O logotipo do streaming precisa ter menos de 1 MB.", nameof(png));
        }

        return _transport.PostBytesAsync("set_streaming_logo.fcgi", png, "application/octet-stream", cancellationToken);
    }

    /// <inheritdoc/>
    public Task<byte[]> GetStreamingLogoAsync(CancellationToken cancellationToken = default) =>
        _transport.GetBytesAsync("get_streaming_logo.fcgi", cancellationToken);

    /// <inheritdoc/>
    public Task RemoveStreamingLogoAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("remove_streaming_logo.fcgi", null, cancellationToken);

    private static string ScreenMediaFilenameQuery(string filename) =>
        "filename=" + Uri.EscapeDataString(Required(filename, nameof(filename)));

    private static void ScreenMediaRequireBytes(byte[] content, string paramName)
    {
        if (content is null || content.Length == 0)
        {
            throw new ArgumentException("Informe o conteúdo do arquivo.", paramName);
        }
    }

    private static void ScreenMediaRequirePng(byte[] png, string paramName)
    {
        ScreenMediaRequireBytes(png, paramName);
        if (png.Length < 4 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47)
        {
            throw new ArgumentException("A imagem precisa estar em PNG.", paramName);
        }
    }

    // A doc descreve os itens como objetos com "filename", mas o exemplo do slideshow traz strings puras: aceita os dois.
    private static ScreenImageList ScreenMediaParseList(string json, string arrayName)
    {
        var names = new List<string>();
        var max = 0;
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty(arrayName, out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    string? name = null;
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        name = item.GetString();
                    }
                    else if (item.ValueKind == JsonValueKind.Object
                        && item.TryGetProperty("filename", out var f)
                        && f.ValueKind == JsonValueKind.String)
                    {
                        name = f.GetString();
                    }

                    if (!string.IsNullOrEmpty(name))
                    {
                        names.Add(name!);
                    }
                }
            }

            if (root.TryGetProperty("max_files", out var maxFiles) && maxFiles.ValueKind == JsonValueKind.Number)
            {
                max = maxFiles.GetInt32();
            }
        }

        return new ScreenImageList(names, max);
    }
}
