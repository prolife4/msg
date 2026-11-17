using System.Text;

namespace Vmx.Messenger.Services.MAX;

/// <summary>
/// DelegatingHandler для диагностики обмена с MAX.
/// Логирует метод, URL, тело запроса и (для текстовых ответов) тело ответа
/// на уровне Debug. Бинарные ответы (image/*, video/*, audio/*,
/// application/octet-stream) НЕ читаются и НЕ переупаковываются —
/// иначе ReadAsStringAsync разрушает байты и добавляет charset=utf-8.
/// </summary>
internal sealed class MaxLoggingHandler : DelegatingHandler
{
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MaxLoggingHandler));

    public MaxLoggingHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var requestBody = await ReadRequestBodyAsync(request, ct).ConfigureAwait(false);

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Debug($"[MAX] {request.Method} {request.RequestUri} → EXCEPTION: {ex}");
            throw;
        }

        var responseBody = await ReadResponseBodyAsync(response, ct).ConfigureAwait(false);

        logger.Debug(
            $"[MAX] {request.Method} {request.RequestUri}{Environment.NewLine}" +
            $"  request: {requestBody}{Environment.NewLine}" +
            $"  status: {(int)response.StatusCode} {response.StatusCode}{Environment.NewLine}" +
            $"  response: {responseBody}");

        return response;
    }

    private static async Task<string> ReadRequestBodyAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Content is null)
            return "<empty>";

        // multipart (загрузка файла) — не читаем, чтобы не тянуть мегабайты в лог
        if (request.Content is MultipartFormDataContent)
            return $"<multipart: {request.Content.Headers.ContentType}>";

        // бинарный request body тоже не логируем
        if (!IsTextualContent(request.Content.Headers.ContentType))
        {
            var len = request.Content.Headers.ContentLength?.ToString() ?? "<unknown>";
            return $"<binary: {request.Content.Headers.ContentType}, {len} bytes>";
        }

        try
        {
            return await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return "<unreadable>";
        }
    }

    private static async Task<string> ReadResponseBodyAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content is null)
            return "<empty>";

        // ВАЖНО: бинарные ответы (image/jpeg и т.п.) НЕ трогаем.
        // ReadAsStringAsync для них разрушает байты (UTF-8 replacement)
        // и добавляет charset=utf-8 при переупаковке в StringContent.
        if (!IsTextualContent(response.Content.Headers.ContentType))
        {
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "<none>";
            var contentLength = response.Content.Headers.ContentLength?.ToString() ?? "<unknown>";
            return $"<binary: {contentType}, {contentLength} bytes>";
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // ВАЖНО: ReadAsStringAsync "съедает" поток — возвращаем контент обратно,
            // иначе библиотека ниже по цепочке не сможет прочитать ответ.
            // Для текстовых ответов это безопасно: байты и кодировка сохраняются.
            response.Content = new StringContent(
                body,
                Encoding.UTF8,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");

            return body;
        }
        catch (Exception ex)
        {
            return $"<unreadable: {ex.Message}>";
        }
    }

    /// <summary>
    /// Текстовый ли Content-Type (JSON/XML/текст/форма)?
    /// Для бинарных (image/*, video/*, audio/*, application/octet-stream и т.п.) — false.
    /// </summary>
    private static bool IsTextualContent(System.Net.Http.Headers.MediaTypeHeaderValue? contentType)
    {
        if (contentType?.MediaType is null)
            return false;

        var mediaType = contentType.MediaType;

        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
            return true;

        if (mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            return true;

        if (mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    protected override void Dispose(bool disposing)
    {
        // НЕ вызываем base.Dispose(disposing) — InnerHandler общий,
        // его жизненным циклом управляет MaxHttpClientFactory.
    }
}