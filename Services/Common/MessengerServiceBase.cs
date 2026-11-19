using Vmx.Common.Extensions;
using Vmx.Common.Logger;
using Vmx.Common.Models.DTO;
using Vmx.Common.Services;
using Vmx.Common.Services.Background;
using Vmx.Messenger.Models;
using Vmx.Messenger.Models.DTO;

namespace Vmx.Messenger.Services.Common;

/// <summary>
/// Необщая часть DataModel: публичные CRUD-операции специфичных для способа доставки полей.
/// Потомки получают весь общий CRUD от MessengerDataModelBase (через inherited members).
/// </summary>
internal abstract class MessengerDataModelBase<TClientDto, TMessageDto>
    where TClientDto : VmxBaseDTO, IMessengerClientDto
    where TMessageDto : VmxBaseDTO, IMessengerMessageDto
{
}

/// <summary>
/// Единый контракт сервиса способа доставки уведомлений: CRUD клиентов/сообщений,
/// постановка сообщения в очередь всем активным клиентам и фоновая отправка.
/// </summary>
public interface IMessengerService<TClientDto, TMessageDto> : IBackgroundWorker
    where TClientDto : IMessengerClientDto
    where TMessageDto : IMessengerMessageDto
{
    Task<TClientDto> AddClientAsync(TClientDto client);
    Task<TClientDto> UpdateClientAsync(TClientDto client);
    Task<TClientDto> DeleteClientAsync(TClientDto client);
    Task<List<TClientDto>> GetClientsAsync();

    IQueryable<TMessageDto> GetMessagesQuery(int clientId);
    Task<List<TMessageDto>> GetMessagesAsync(int clientId);
    Task<TMessageDto> AddMessageAsync(TMessageDto message);
    Task<TMessageDto> UpdateMessageAsync(TMessageDto message);
    Task<TMessageDto> DeleteMessageAsync(TMessageDto message);

    /// <summary>Поставить сообщение в очередь всем подходящим клиентам.</summary>
    Task AddMessageToSendAsync(TMessageDto message);

    /// <summary>Отправить тестовое сообщение. Пустая строка — успех, иначе текст ошибки.</summary>
    Task<string> SendTestMessageAsync(TClientDto client);
}

/// <summary>
/// Базовый сервис способа доставки: общий CRUD (делегирование в базовую DataModel),
/// постановка в очередь и цикл фоновой отправки. Потомки реализуют только саму доставку.
/// </summary>
public abstract class MessengerServiceBase<TDataModel, TClientDto, TMessageDto> : VmxBaseService,
    IMessengerService<TClientDto, TMessageDto>
    where TDataModel : MessengerDataModelBase<TClientDto, TMessageDto>
    where TClientDto : VmxBaseDTO, IMessengerClientDto
    where TMessageDto : VmxBaseDTO, IMessengerMessageDto
{
    protected static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MessengerServiceBase<TDataModel, TClientDto, TMessageDto>));

    /// <summary>Название способа доставки в логах (Email / Telegram / MAX).</summary>
    protected abstract string ChannelName { get; }

    protected TDataModel Model => (TDataModel)_model;

    public async Task<TClientDto> AddClientAsync(TClientDto client) => await Model.AddClientAsync(client);
    public async Task<TClientDto> UpdateClientAsync(TClientDto client) => await Model.UpdateClientAsync(client);
    public async Task<TClientDto> DeleteClientAsync(TClientDto client) => await Model.DeleteClientAsync(client);
    public async Task<List<TClientDto>> GetClientsAsync() => await Model.GetClientsAsync();

    public IQueryable<TMessageDto> GetMessagesQuery(int clientId) => Model.GetMessagesQuery(clientId);
    public async Task<List<TMessageDto>> GetMessagesAsync(int clientId) => await Model.GetPendingMessagesAsync(clientId);
    public async Task<TMessageDto> AddMessageAsync(TMessageDto message) => await Model.AddMessageAsync(message);
    public async Task<TMessageDto> UpdateMessageAsync(TMessageDto message) => await Model.UpdateMessageAsync(message);
    public async Task<TMessageDto> DeleteMessageAsync(TMessageDto message) => await Model.DeleteMessageAsync(message);

    /// <summary>Клиенты, которым следует поставить сообщение в очередь (по умолчанию — все активные).</summary>
    protected virtual Task<List<TClientDto>> GetTargetClientsAsync(TMessageDto message) =>
        Model.GetClientsAsync();

    public async Task AddMessageToSendAsync(TMessageDto message)
    {
        List<TClientDto> clients = await GetTargetClientsAsync(message).ConfigureAwait(false);
        if (clients.CheckEmptyOrFailedVmxCollection(out TClientDto failedClient))
        {
            _ = ServiceLogging<TClientDto>(logger, failedClient);
            return;
        }

        foreach (TClientDto client in clients)
        {
            if (!client.IsActive)
                continue;

            message.ClientId = client.Id;
            TMessageDto added = await Model.AddMessageAsync(message).ConfigureAwait(false);

            if (added.IsFailedDTO)
            {
                _ = ServiceLogging<TClientDto>(logger, added);
                continue;
            }

            _ = ServiceLogging<TClientDto>(logger, 200,
                $"Message '{added.DisplayText}' was added to {ChannelName} client '{client.DisplayName}'");
        }
    }

    #region Background worker (шаблонный цикл отправки)

    public TimeSpan GetInterval() => TimeSpan.FromSeconds(60);

    public string GetName() => GetType().Name;

    /// <summary>Срок хранения старых сообщений в днях; &lt;= 0 — не удалять.</summary>
    protected virtual int MessageRetentionDays => 15;

    /// <summary>Доставка одного сообщения клиенту. null/пусто — успех, иначе текст ошибки.</summary>
    protected abstract Task<string?> SendMessageAsync(TClientDto client, TMessageDto message);

    /// <summary>Вызывается перед отправкой каждого сообщения (например, подготовка вложений у Email).</summary>
    protected virtual Task PrepareMessageAsync(TClientDto client, TMessageDto message) => Task.CompletedTask;

    public async Task RunWorker()
    {
        List<TClientDto> clients = await Model.GetClientsAsync(noCache: true).ConfigureAwait(false);
        if (clients.CheckEmptyOrFailedVmxCollection(out TClientDto failedClient))
        {
            _ = ServiceLogging<TClientDto>(logger, failedClient);
            return;
        }

        foreach (TClientDto client in clients.Where(c => c.IsActive))
        {
            List<TMessageDto> messages = await Model.GetPendingMessagesAsync(client.Id).ConfigureAwait(false);
            if (messages.CheckEmptyOrFailedVmxCollection(out TMessageDto failedMessage))
            {
                // 414 — штатная ситуация "нет сообщений", не логируем как ошибку
                if (failedMessage.Message?.Error?.Code != 414)
                    _ = ServiceLogging<TMessageDto>(logger, failedMessage);
                continue;
            }

            foreach (TMessageDto message in messages.OrderBy(m => m.DateCreated))
                await SendAndStoreAsync(client, message).ConfigureAwait(false);
        }

        if (MessageRetentionDays > 0)
            await Model.DeleteOldMessagesAsync(DateTime.Now.AddDays(-MessageRetentionDays)).ConfigureAwait(false);
    }

    private async Task SendAndStoreAsync(TClientDto client, TMessageDto message)
    {
        try
        {
            await PrepareMessageAsync(client, message).ConfigureAwait(false);
            string? error = await SendMessageAsync(client, message).ConfigureAwait(false);

            if (string.IsNullOrEmpty(error))
            {
                message.Status = MessageStatus.Sent;
                message.DateSent = DateTime.Now;
            }
            else
            {
                message.Status = MessageStatus.Failed;
                message.Comment = error;
            }
        }
        catch (Exception ex)
        {
            _ = ServiceLogging<TClientDto>(logger, 500, ex.Message);
            message.Status = MessageStatus.Failed;
            message.Comment = ex.Message;
        }

        TMessageDto updated = await Model.UpdateMessageAsync(message).ConfigureAwait(false);
        if (updated.IsFailedDTO)
        {
            _ = ServiceLogging<TMessageDto>(logger, updated);
        }
        else if (message.Status == MessageStatus.Sent)
        {
            _ = ServiceLogging<TClientDto>(logger, 200,
                $"Message '{message.DisplayText}' was sent to {ChannelName} client '{client.DisplayName}'");
        }
    }

    #endregion
}

