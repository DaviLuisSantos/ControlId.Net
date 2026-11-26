using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    private const int FaceTemplateBytes = 1024;

    /// <inheritdoc/>
    public Task<FaceTemplateMatchResult> MatchFaceTemplateAsync(string faceTemplate, long? userId = null, CancellationToken cancellationToken = default)
    {
        Required(faceTemplate, nameof(faceTemplate));
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(faceTemplate);
        }
        catch (FormatException)
        {
            throw new ArgumentException("O template facial precisa estar em Base64.", nameof(faceTemplate));
        }

        if (decoded.Length != FaceTemplateBytes)
        {
            throw new ArgumentException($"O template facial decodificado precisa ter exatamente {FaceTemplateBytes} bytes.", nameof(faceTemplate));
        }

        var body = new Dictionary<string, object> { ["face_template"] = faceTemplate };
        if (userId is { } id)
        {
            body["user_id"] = id;
        }

        return _transport.PostAsync<FaceTemplateMatchResult>("face_template_match.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountFaceTemplatesAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<FaceTemplateCountResponse>("face_template_count_distinct.fcgi", cancellationToken).ConfigureAwait(false)).Count;

    /// <inheritdoc/>
    public async Task<string> MatchFingerprintTemplatesAsync(IReadOnlyList<byte[]> templates, CancellationToken cancellationToken = default)
    {
        if (templates is null || templates.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um template.", nameof(templates));
        }

        var query = new StringBuilder("temp_num=").Append(templates.Count.ToString(CultureInfo.InvariantCulture));
        long total = 0;
        for (var i = 0; i < templates.Count; i++)
        {
            var template = templates[i];
            if (template is null || template.Length == 0)
            {
                throw new ArgumentException($"O template {i} está vazio.", nameof(templates));
            }

            query.Append("&size").Append(i.ToString(CultureInfo.InvariantCulture)).Append('=').Append(template.Length.ToString(CultureInfo.InvariantCulture));
            total += template.Length;
        }

        var body = new byte[total];
        var offset = 0;
        foreach (var template in templates)
        {
            Buffer.BlockCopy(template, 0, body, offset, template.Length);
            offset += template.Length;
        }

        if (offset != total)
        {
            throw new InvalidOperationException("A soma dos tamanhos não confere com o corpo montado.");
        }

        var text = await _transport.PostBytesAsync("template_match.fcgi", body, "application/octet-stream", cancellationToken, query.ToString()).ConfigureAwait(false);
        return ControlIdJson.Deserialize<FingerprintTemplateResponse>(text).Template ?? string.Empty;
    }

    /// <inheritdoc/>
    public Task<IoState> GetIoStateAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<IoState>("io_state.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> IsIdentifyingAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<IsIdentifyingResponse>("is_identifying.fcgi", cancellationToken).ConfigureAwait(false)).IsIdentifying;

    /// <inheritdoc/>
    public Task<TurnstileCounters> GetTurnstileCountersAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<TurnstileCounters>("get_catra_info.fcgi", cancellationToken);

    /// <inheritdoc/>
    public async Task<byte[]> GetLastSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetJsonAsync<LastSnapshotResponse>("get_last_snapshot.fcgi", cancellationToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(response.ImageBase64) ? Array.Empty<byte>() : Convert.FromBase64String(response.ImageBase64);
    }

    /// <inheritdoc/>
    public async Task<string> GetRfidModuleAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<RfidModuleResponse>("device_rfid_module.fcgi", cancellationToken).ConfigureAwait(false)).Module ?? string.Empty;

    /// <inheritdoc/>
    public Task<HidLastCard> GetHidLastCardAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<HidLastCard>("get_hid_last_card.fcgi", cancellationToken);

    /// <inheritdoc/>
    public Task<WiegandCardRead> GetLastWiegandReadAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<WiegandCardRead>("get_wiegand_card_read.fcgi", cancellationToken);

    private sealed class FaceTemplateCountResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    private sealed class FingerprintTemplateResponse
    {
        [JsonPropertyName("template")]
        public string? Template { get; set; }
    }

    private sealed class IsIdentifyingResponse
    {
        [JsonPropertyName("is_identifying")]
        public bool IsIdentifying { get; set; }
    }

    private sealed class LastSnapshotResponse
    {
        [JsonPropertyName("image_base64")]
        public string? ImageBase64 { get; set; }
    }

    private sealed class RfidModuleResponse
    {
        [JsonPropertyName("module")]
        public string? Module { get; set; }
    }
}
