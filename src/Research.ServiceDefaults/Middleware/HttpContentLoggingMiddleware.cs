using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Shared.Core.Logging;

namespace Research.ServiceDefaults.Middleware;

/// <summary>
/// Logs request and response bodies of the API under <see cref="ContentLogCategories.Http"/> (product decision: API
/// content is logged). Headers are never logged. The response is not buffered: writes go straight to the client and
/// a copy, capped at <see cref="ContentLoggingOptions.MaxChars"/>, is kept for the log. Event streams and health
/// endpoints are skipped.
/// </summary>
public sealed partial class HttpContentLoggingMiddleware(
    RequestDelegate next,
    ILoggerFactory loggerFactory,
    IOptionsMonitor<ContentLoggingOptions> options)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(ContentLogCategories.Http);

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_logger.IsEnabled(LogLevel.Information)
            || context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var maxChars = options.CurrentValue.MaxChars;
        var requestBody = await ReadRequestBodyAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        LogRequest(_logger, context.Request.Method, context.Request.Path, context.Request.QueryString.Value ?? string.Empty, ContentText.Truncate(requestBody, maxChars), requestBody.Length);

        var originalBody = context.Response.Body;
        await using var tee = new TeeStream(originalBody, maxChars <= 0 ? int.MaxValue : maxChars * 4);
        context.Response.Body = tee;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var contentType = context.Response.ContentType ?? string.Empty;
        if (contentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var responseBody = Encoding.UTF8.GetString(tee.Captured);
        LogResponse(_logger, context.Request.Method, context.Request.Path, context.Response.StatusCode, ContentText.Truncate(responseBody, maxChars), tee.TotalBytes);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is 0 || !(request.ContentLength > 0 || request.Headers.TransferEncoding.Count > 0))
        {
            return string.Empty;
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        request.Body.Position = 0;
        return body;
    }

    [LoggerMessage(EventId = 9210, Level = LogLevel.Information, Message = "HTTP request {Method} {Path}{Query} body {RequestBody} ({RequestLength} chars)")]
    private static partial void LogRequest(ILogger logger, string method, string path, string query, string requestBody, int requestLength);

    [LoggerMessage(EventId = 9211, Level = LogLevel.Information, Message = "HTTP response {Method} {Path} {StatusCode} body {ResponseBody} ({ResponseBytes} bytes)")]
    private static partial void LogResponse(ILogger logger, string method, string path, int statusCode, string responseBody, long responseBytes);

    /// <summary>Forwards every write and keeps a capped copy.</summary>
    private sealed class TeeStream(Stream inner, int maxCaptureBytes) : Stream
    {
        private readonly MemoryStream _capture = new();

        public byte[] Captured => _capture.ToArray();

        public long TotalBytes { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            Capture(buffer.AsSpan(offset, count));
            inner.Write(buffer, offset, count);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Capture(buffer.Span);
            await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _capture.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Capture(ReadOnlySpan<byte> data)
        {
            TotalBytes += data.Length;
            var room = maxCaptureBytes - (int)_capture.Length;
            if (room > 0)
            {
                _capture.Write(data[..Math.Min(room, data.Length)]);
            }
        }
    }
}
