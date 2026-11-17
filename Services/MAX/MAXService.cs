using System.Net.Http.Headers;
using System.Text.Json;
using Max.BotClient;
using Max.BotClient.Types;
using Microsoft.Extensions.Caching.Memory;
using Vmx.Common.Extensions;
using Vmx.Common.Services;
using Vmx.Common.Services.Background;
using Vmx.Common.Utils;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.Models;
using Vmx.Messenger.Models.DTO;

namespace Vmx.Messenger.Services.MAX;

public interface IMAXService : IBackgroundWorker
{
    Task<MAXClientDTO> AddClient(MAXClientDTO MAXClient);
    Task<MAXClientDTO> UpdateClient(MAXClientDTO MAXClient);
    Task<MAXClientDTO> DeleteClient(MAXClientDTO MAXClient);
    Task<List<MAXClientDTO>> GetClients();
    Task<List<MAXMessageDTO>> GetMessages(int clientId);
    IQueryable<MAXMessageDTO> GetMessagesQuery(int clientId);
    Task<MAXMessageDTO> AddMessage(MAXMessageDTO MAXMessage);
    Task<MAXMessageDTO> UpdateMessage(MAXMessageDTO MAXMessage);
    Task<MAXMessageDTO> DeleteMessage(MAXMessageDTO MAXMessage);
    Task AddMessageToSend(MAXMessageDTO message);
    Task<string> SendTestMessage(MAXClientDTO client);
}

/// <summary>
/// Результат отправки сообщения.
/// <paramref name="Error"/> == null означает успех.
/// <paramref name="AttachmentFailedSource"/> != null означает, что вложение не удалось
/// загрузить, и сообщение ушло без него.
/// </summary>
public sealed record SendMessageResult(string? Error, string? AttachmentFailedSource);

public class MAXService : VmxBaseService, IMAXService
{
    private static readonly log4net.ILog logger =
        log4net.LogManager.GetLogger(typeof(MAXService));

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private MAXServiceDataModel Model => (MAXServiceDataModel)_model;

    public MAXService(MessengerDbContext dbContext, IMemoryCache memoryCache)
    {
        _model = new MAXServiceDataModel(dbContext, memoryCache);
    }

    // ---------- CRUD ----------

    public async Task<MAXClientDTO> AddClient(MAXClientDTO MAXClient) =>
        await Model.AddClient(MAXClient);

    public async Task<MAXClientDTO> UpdateClient(MAXClientDTO MAXClient) =>
        await Model.UpdateClient(MAXClient);

    public async Task<MAXClientDTO> DeleteClient(MAXClientDTO MAXClient) =>
        await Model.DeleteClient(MAXClient);

    public async Task<List<MAXClientDTO>> GetClients() => await Model.GetClients();

    public IQueryable<MAXMessageDTO> GetMessagesQuery(int clientId) =>
        Model.GetMessagesQuery(clientId);

    public async Task<List<MAXMessageDTO>> GetMessages(int clientId) =>
        await Model.GetMessages(clientId);

    public async Task<MAXMessageDTO> AddMessage(MAXMessageDTO MAXMessage) =>
        await Model.AddMessage(MAXMessage);

    public async Task<MAXMessageDTO> UpdateMessage(MAXMessageDTO MAXMessage) =>
        await Model.UpdateMessage(MAXMessage);

    public async Task<MAXMessageDTO> DeleteMessage(MAXMessageDTO MAXMessage) =>
        await Model.DeleteMessages(MAXMessage);

    public async Task AddMessageToSend(MAXMessageDTO message)
    {
        List<MAXClientDTO> MAXClients = await Model.GetClients(message.ClientId);
        if (MAXClients.CheckEmptyOrFailedVmxCollection(out MAXClientDTO failedElement))
        {
            _ = ServiceLogging<MAXClientDTO>(logger, failedElement);
            return;
        }

        foreach (MAXClientDTO MAXClient in MAXClients)
        {
            message.ClientId = MAXClient.Id;
            message = await Model.AddMessage(message);

            if (message.IsFailedDTO)
            {
                _ = ServiceLogging<MAXClientDTO>(logger, message);
            }
            else
            {
                _ = ServiceLogging<MAXClientDTO>(logger, 200, $"Message '{message.Caption}' was added to MAX client '{MAXClient.ChatName}'");
            }
        }
    }

    // ---------- Отправка сообщений ----------

    /// <summary>
    /// Отправляет сообщение в MAX. Поддерживается одно вложение (локальный путь или http(s)-URL).
    /// Если вложение не удалось загрузить — сообщение уйдёт без него,
    /// а источник будет возвращён в <see cref="SendMessageResult.AttachmentFailedSource"/>.
    /// </summary>
    public async Task<SendMessageResult> SendMessageAsync(MAXClientDTO client, string text, string? attachment = null, CancellationToken ct = default)
    {
        string? attachmentFailedSource = null;
        try
        {
            using var http = MaxHttpClientFactory.Create();
            var bot = new BotClient(client.Token, http);

            (string? Token, UploadType Type)? uploaded = null;
            if (!string.IsNullOrWhiteSpace(attachment))
            {
                uploaded = await UploadAttachmentAsync(bot, http, attachment, ct).ConfigureAwait(false);

                if (uploaded is null)
                    attachmentFailedSource = attachment;
            }

            var messageBuilder = new Message(text).ToChat();
            if (uploaded is { Token: not null } up)
            {
                messageBuilder = up.Type switch
                {
                    UploadType.Video => messageBuilder.WithVideo(up.Token),
                    UploadType.Audio => messageBuilder.WithAudio(up.Token),
                    UploadType.Image => messageBuilder.WithPhotoToken(up.Token),
                    _ => messageBuilder.WithFile(up.Token)
                };
            }

            try
            {
                await bot.SendMessage(client.ChatId, messageBuilder, cancellationToken: ct).ConfigureAwait(false);
            }
            catch (NullReferenceException ex) when (
                ex.StackTrace?.Contains("Max.BotClient.Mapping") == true)
            {
                // Баг Max.BotClient 1.1.3: NRE при разборе markup типа "highlighted".
                // Сообщение доставлено (HTTP 200) — считаем отправку успешной.
                ServiceLogging<MAXClientDTO>(logger, 200, $"SendMessage: NRE in Max.BotClient mapping (message delivered). chatId={client.ChatId}");
            }
            return new SendMessageResult(null, attachmentFailedSource);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ServiceLogging<MAXClientDTO>(logger, 500, ex.ToString());
            return new SendMessageResult(ex.Message, attachmentFailedSource);
            //var err = $"{ex.Message} - {ex.InnerException?.Message}";
            //ServiceLogging<MAXClientDTO>(logger, 500, err);
            //return new SendMessageResult(ex.Message, attachmentFailedSource);
        }
    }

    private async Task<(string Token, UploadType Type)?> UploadAttachmentAsync(BotClient bot, HttpClient http, string source, CancellationToken ct)
    {
        try
        {
            var (uploadType, contentType) = MaxAttachmentTypeResolver.Resolve(source);
            var fileName = MaxAttachmentTypeResolver.ResolveFileName(source);

         //   var bytes = await GetAttachmentBytesAsync(http, source, ct).ConfigureAwait(false);
            var bytes = await FileUtils.GetBytesAsync(source, ct).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
                return null;

            var upload = await bot.GetUploadUrl(uploadType, cancellationToken: ct).ConfigureAwait(false);

            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "image", fileName);

            using var response = await http.PostAsync(upload.Url, content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                ServiceLogging<MAXClientDTO>(logger, (int)response.StatusCode, $"MAX upload failed for '{source}': {body}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var parsed = JsonSerializer.Deserialize<MaxUploadResponse>(json, JsonOpts);
            var token = parsed?.GetFirstToken();

            if (string.IsNullOrEmpty(token))
            {
                ServiceLogging<MAXClientDTO>(logger, 500, $"MAX upload returned no token for '{source}': {json}");
                return null;
            }

            return (token, uploadType);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ServiceLogging<MAXClientDTO>(logger, 500, $"MAX upload exception for '{source}': {ex}");
            return null;
        }
    }

    /// <summary>
    /// Читает байты вложения. Поддерживает:
    /// 1) абсолютный http(s) URL — скачивание как есть;
    /// 2) относительный web-путь ("/x.png", "~/x.png") — резолв в абсолютный URL
    ///    и скачивание;
    /// 3) локальный путь (абсолютный или относительный к рабочей директории).
    /// </summary>
    //private async Task<byte[]?> GetAttachmentBytesAsync(HttpClient http, string source, CancellationToken ct)
    //{
    //    if (string.IsNullOrWhiteSpace(source))
    //        return null;

    //    // 1. Абсолютный http(s) URL
    //    if (Uri.TryCreate(source, UriKind.Absolute, out var absoluteUri) &&
    //        (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
    //    {
    //        return await DownloadAsync(http, absoluteUri, ct).ConfigureAwait(false);
    //    }

    //    // 2. Web-relative путь — резолвим и качаем
    //    if (source.StartsWith('/') || source.StartsWith("~/"))
    //    {
    //        var resolved = FileUtils.RelativePathResolve(source);
    //        if (resolved is null)
    //        {
    //            ServiceLogging<MAXClientDTO>(logger, 500, $"Не удалось разрешить относительный путь '{source}' (проверьте WebApi.ClientUrl)");
    //            return null;
    //        }
    //        return await DownloadAsync(http, resolved, ct).ConfigureAwait(false);
    //    }

    //    // 3. Локальный путь
    //    var local = NormalizePath(source);
    //    if (!File.Exists(local)) return null;
    //    return await File.ReadAllBytesAsync(local, ct).ConfigureAwait(false);
    //}

    //private async Task<byte[]?> DownloadAsync(HttpClient http, Uri uri, CancellationToken ct)
    //{
    //    using var resp = await http.GetAsync(uri, ct).ConfigureAwait(false);
    //    if (!resp.IsSuccessStatusCode)
    //    {
    //        ServiceLogging<MAXClientDTO>(logger, (int)resp.StatusCode, $"MAX download failed: {uri}");
    //        return null;
    //    }

    //    var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

    //    // --- диагностика: сохраняем скачанный файл на диск для сравнения ---
    //    // await DumpDownloadedFileAsync(uri, resp, bytes, ct).ConfigureAwait(false);

    //    return bytes;
    //}

    /// <summary>
    /// Диагностика: сохраняет скачанный по URL файл в C:\tmp с уникальным именем.
    /// Имя: <оригинальное_имя>__<guid8><ext>. Ошибки записи не ломают основной поток.
    /// </summary>
    //private static async Task DumpDownloadedFileAsync(Uri uri, HttpResponseMessage resp, byte[] bytes, CancellationToken ct)
    //{
    //    try
    //    {
    //        const string dumpDir = @"C:\tmp";
    //        Directory.CreateDirectory(dumpDir);

    //        var originalName = Path.GetFileName(uri.LocalPath);
    //        if (string.IsNullOrWhiteSpace(originalName))
    //            originalName = "download";

    //        var ext = Path.GetExtension(originalName);
    //        var baseName = Path.GetFileNameWithoutExtension(originalName);
    //        var unique = Guid.NewGuid().ToString("N").Substring(0, 8);

    //        var dumpPath = Path.Combine(
    //            dumpDir,
    //            $"{baseName}__{unique}{ext}");

    //        await File.WriteAllBytesAsync(dumpPath, bytes, ct).ConfigureAwait(false);

    //        var contentType = resp.Content.Headers.ContentType?.ToString() ?? "<none>";
    //        var contentLength = resp.Content.Headers.ContentLength?.ToString() ?? "<unknown>";

    //        logger.Debug(
    //            $"[MAX] dumped {bytes.Length} bytes (Content-Type: {contentType}, " +
    //            $"Content-Length: {contentLength}) from {uri} → {dumpPath}");
    //    }
    //    catch (Exception ex)
    //    {
    //        // диагностика не должна ломать отправку
    //        logger.Debug($"[MAX] dump failed for {uri}: {ex.Message}");
    //    }
    //}

    //private static string NormalizePath(string source)
    //{
    //    var normalized = source.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);
    //    return Path.GetFullPath(normalized);
    //}

    // ---------- Background Implementation ----------

    public TimeSpan GetInterval() => TimeSpan.FromSeconds(60);

    public string GetName() => nameof(MAXService);

    public async Task RunWorker()
    {
        List<MAXClientDTO> clients = await Model.GetClients(0, true);
        if (clients.CheckEmptyOrFailedVmxCollection(out MAXClientDTO failedElement))
        {
            _ = ServiceLogging<MAXClientDTO>(logger, failedElement);
            return;
        }

        foreach (MAXClientDTO client in clients)
        {
            List<MAXMessageDTO> messages = await Model.GetMessages(client.Id);
            if (messages.CheckEmptyOrFailedVmxCollection(out MAXMessageDTO failedElement1))
            {
                if (failedElement1.Message?.Error?.Code != 414)
                    _ = ServiceLogging<MAXClientDTO>(logger, failedElement1);
                continue;
            }

            foreach (MAXMessageDTO message in messages.OrderBy(x => x.DateCreated))
            {
                await SendStoredMessageAsync(client, message).ConfigureAwait(false);
            }
        }
    }

    private async Task SendStoredMessageAsync(MAXClientDTO client, MAXMessageDTO message)
    {
        var attachment = message.Attachments?.FirstOrDefault();

        var result = await SendMessageAsync(client, message.Caption, attachment, CancellationToken.None).ConfigureAwait(false);

        if (result.Error is null)
        {
            message.Status = MessageStatus.Sent;
            message.DateSent = DateTime.Now;
            if (result.AttachmentFailedSource is not null)
                message.Comment = $"Не удалось приложить: {result.AttachmentFailedSource}";

            var updated = await Model.UpdateMessage(message);
            if (updated.IsFailedDTO)
            {
                _ = ServiceLogging<MAXClientDTO>(logger, updated);
            }
            else
            {
                var log = result.AttachmentFailedSource is not null
                    ? $"Message '{message.Caption}' was sent to MAX client '{client.ChatName}' WITHOUT attachment ({result.AttachmentFailedSource})"
                    : $"Message '{message.Caption}' was sent to MAX client '{client.ChatName}'";
                _ = ServiceLogging<MAXClientDTO>(logger, 200, log);
            }
            return;
        }

        message.Status = MessageStatus.Failed;
        message.Comment = result.AttachmentFailedSource is not null
            ? $"{result.Error} (вложение: {result.AttachmentFailedSource})"
            : result.Error;

        var failed = await Model.UpdateMessage(message);
        if (failed.IsFailedDTO)
            _ = ServiceLogging<MAXClientDTO>(logger, failed);
    }

    // ---------- Тестовое сообщение ----------

    /// <summary>
    /// Диагностическая отправка: тестовый текст + логотип.
    /// Пустая строка при успехе, текст ошибки — при провале (включая случай,
    /// когда не удалось приложить картинку — иначе тест «врёт» об успехе).
    /// </summary>
    public async Task<string> SendTestMessage(MAXClientDTO client)
    {
        var result = await SendMessageAsync(
            client,
            "Test message",
            "/images/vmx.png",
            CancellationToken.None).ConfigureAwait(false);

        if (result.Error is not null)
            return result.Error;

        if (result.AttachmentFailedSource is not null)
            return $"Вложение не загружено: {result.AttachmentFailedSource}";

        return string.Empty;
    }
}