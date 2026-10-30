using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Internal;

/// <summary>
/// Corpo de uma resposta lido sob demanda. Fechar o stream libera a resposta e a vaga do limite de requisições
/// simultâneas do equipamento, que fica ocupada enquanto o corpo é lido.
/// </summary>
internal sealed class ResponseStream : Stream
{
    private readonly Stream _inner;
    private readonly HttpResponseMessage _response;
    private SemaphoreSlim? _throttle;

    public ResponseStream(Stream inner, HttpResponseMessage response, SemaphoreSlim throttle)
    {
        _inner = inner;
        _response = response;
        _throttle = throttle;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _inner.ReadAsync(buffer, offset, count, cancellationToken);

#if NET5_0_OR_GREATER
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(buffer, cancellationToken);
#endif

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _response.Dispose();
            Interlocked.Exchange(ref _throttle, null)?.Release();
        }

        base.Dispose(disposing);
    }
}
