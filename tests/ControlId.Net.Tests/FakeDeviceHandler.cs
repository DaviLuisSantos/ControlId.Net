using System.Net;
using System.Text;

namespace ControlId.Tests;

/// <summary>
/// Simula um equipamento: responde por endpoint (*.fcgi) e guarda as requisições recebidas.
/// </summary>
internal sealed class FakeDeviceHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<Func<HttpRequestMessage, HttpResponseMessage>>> _routes = new();

    public List<(HttpMethod Method, Uri Uri, string? ContentType, string Body)> Requests { get; } = new();

    public FakeDeviceHandler On(string endpoint, HttpStatusCode status, string body = "")
    {
        if (!_routes.TryGetValue(endpoint, out var queue))
        {
            _routes[endpoint] = queue = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        }

        queue.Enqueue(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        return this;
    }

    public FakeDeviceHandler OnBytes(string endpoint, byte[] body, string contentType)
    {
        if (!_routes.TryGetValue(endpoint, out var queue))
        {
            _routes[endpoint] = queue = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        }

        queue.Enqueue(_ =>
        {
            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        return this;
    }

    public FakeDeviceHandler OnJson(string endpoint, string json) => On(endpoint, HttpStatusCode.OK, json);

    public IEnumerable<(HttpMethod Method, Uri Uri, string? ContentType, string Body)> RequestsTo(string endpoint) =>
        Requests.Where(r => r.Uri.AbsolutePath.EndsWith("/" + endpoint, StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!, request.Content?.Headers.ContentType?.MediaType, body));

        var endpoint = request.RequestUri!.AbsolutePath.TrimStart('/');
        if (!_routes.TryGetValue(endpoint, out var queue) || queue.Count == 0)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        // Mantém a última resposta para chamadas repetidas.
        var responder = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        return responder(request);
    }

    public HttpClient CreateClient() => new(this);
}
