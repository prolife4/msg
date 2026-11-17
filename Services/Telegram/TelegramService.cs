using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Vmx.Common.Extensions;
using Vmx.Common.Logger;
using Vmx.Common.Services;
using Vmx.Common.Services.Background;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.Models;
using Vmx.Messenger.Models.DTO;

namespace Vmx.Messenger.Services.Telegram;

public interface ITelegramService : IBackgroundWorker
{
    Task<TelegramClientDTO> AddTelegramClient(TelegramClientDTO TelegramClient);
    Task<TelegramClientDTO> UpdateTelegramClient(TelegramClientDTO TelegramClient);
    Task<TelegramClientDTO> DeleteTelegramClient(TelegramClientDTO TelegramClient);
    Task<List<TelegramClientDTO>> GetTelegramClients();
    Task<List<TelegramMessageDTO>> GetTelegramMessages(int clientId);
    IQueryable<TelegramMessageDTO> GetMessagesQuery(int clientId);
    Task<TelegramMessageDTO> AddTelegramMessage(TelegramMessageDTO TelegramMessage);
    Task<TelegramMessageDTO> UpdateTelegramMessage(TelegramMessageDTO TelegramMessage);
    Task<TelegramMessageDTO> DeleteTelegramMessage(TelegramMessageDTO TelegramMessage);
    Task AddTelegramMessageToSend(TelegramMessageDTO message);
    Task<string> SendTestMessage(TelegramClientDTO client);
}

public class TelegramService : VmxBaseService, ITelegramService
{
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(TelegramService));
    private readonly IHttpClientService _httpClientService;

    private TelegramServiceDataModel Model => (TelegramServiceDataModel)_model;
    public TelegramService(MessengerDbContext dbContext, IHttpClientService httpClientService,
        IMemoryCache memoryCache)
    {
        _model = new TelegramServiceDataModel(dbContext, memoryCache);
        _httpClientService = httpClientService;
    }

    public async Task<TelegramClientDTO> AddTelegramClient(TelegramClientDTO TelegramClient) =>
            await Model.AddTelegramClient(TelegramClient);
    public async Task<TelegramClientDTO> UpdateTelegramClient(TelegramClientDTO TelegramClient) =>
        await Model.UpdateTelegramClient(TelegramClient);
    public async Task<TelegramClientDTO> DeleteTelegramClient(TelegramClientDTO TelegramClient) =>
        await Model.DeleteTelegramClient(TelegramClient);
    public async Task<List<TelegramClientDTO>> GetTelegramClients() =>
        await Model.GetTelegramClients();
    public IQueryable<TelegramMessageDTO> GetMessagesQuery(int clientId) =>
         Model.GetMessagesQuery(clientId);
    public async Task<List<TelegramMessageDTO>> GetTelegramMessages(int clientId) =>
        await Model.GetTelegramMessagesMessages(clientId);
    public async Task<TelegramMessageDTO> AddTelegramMessage(TelegramMessageDTO TelegramMessage) =>
        await Model.AddTelegramMessage(TelegramMessage);
    public async Task<TelegramMessageDTO> UpdateTelegramMessage(TelegramMessageDTO TelegramMessage) =>
        await Model.UpdateTelegramMessage(TelegramMessage);
    public async Task<TelegramMessageDTO> DeleteTelegramMessage(TelegramMessageDTO TelegramMessage) =>
        await Model.DeleteTelegramMessages(TelegramMessage);

    public async Task AddTelegramMessageToSend(TelegramMessageDTO message)
    {
        List<TelegramClientDTO> TelegramClients = await Model.GetTelegramClients(message.ClientId);
        if (TelegramClients.CheckEmptyOrFailedVmxCollection(out TelegramClientDTO failedElement))
        {
            _ = ServiceLogging<TelegramClientDTO>(logger, failedElement);
            return;
        }

        foreach (TelegramClientDTO TelegramClient in TelegramClients)
        {
            message.ClientId = TelegramClient.Id;
            message = await Model.AddTelegramMessage(message);

            if (message.IsFailedDTO)
            {
                _ = ServiceLogging<TelegramClientDTO>(logger, message);
            }
            else
            {
                _ = ServiceLogging<TelegramClientDTO>(logger, 200,
                    $"Message '{message.Caption}' was added to Telegram client '{TelegramClient.ChatName}'");
            }
        }
    }

    #region Background Implementation
    public TimeSpan GetInterval() => TimeSpan.FromSeconds(60);

    public string GetName() => nameof(TelegramService);

    private async Task<Stream> GetFileStream(string url)
    {
        url = url.Replace(@"\\", "/").Replace(@"\", "/");
        using HttpClient client = new();
        client.Timeout = TimeSpan.FromSeconds(60);
        try
        {
            var stream = await client.GetStreamAsync(url);
            return stream;
        }
        catch (Exception ex)
        {
            VmxLogger.Instance.Error(logger, ex);
            return null;
        }
    }

    public async Task RunWorker()
    {
        List<TelegramClientDTO> clients = await Model.GetTelegramClients(0, true);
        if (clients.CheckEmptyOrFailedVmxCollection(out TelegramClientDTO failedElement))
        {
            _ = ServiceLogging<TelegramClientDTO>(logger, failedElement);
            return;
        }

        foreach (TelegramClientDTO client in clients)
        {
            List<TelegramMessageDTO> messages = await Model.GetTelegramMessagesMessages(client.Id);
            if (messages.CheckEmptyOrFailedVmxCollection(out TelegramMessageDTO failedElement1))
            {
                if (failedElement1.Message.Error.Code != 414)
                {
                    _ = ServiceLogging<TelegramClientDTO>(logger, failedElement1);
                }
                continue;
            }

            TelegramBotClient clientBot = client.ToClient();
            foreach (TelegramMessageDTO message in messages.OrderBy(x => x.DateCreated))
            {
                if (message.ClientId != client.Id)
                    continue;

                try
                {
                    if (message.Attachments.Count == 0)
                    {
                        await clientBot.SendTextMessageAsync(
                                                chatId: client.ChatId,
                                                text: message.Caption,
                                                parseMode: message.ParseMode
                                            );
                    }
                    else
                    {
                        try
                        {
                            using Stream stream = await GetFileStream(message.Attachments.FirstOrDefault());
                           if(stream!= null)
                            await clientBot.SendPhotoAsync(
                                                    chatId: client.ChatId,
                                                    caption: message.Caption,
                                                    photo: InputFile.FromStream(stream),
                                                    parseMode: message.ParseMode
                                                );
                            else
                            await clientBot.SendTextMessageAsync(
                                               chatId: client.ChatId,
                                               text: message.Caption,
                                               parseMode: message.ParseMode
                                           );
                        }
                        catch (Exception e)
                        {
                            _ = ServiceLogging<TelegramClientDTO>(logger, 500, e.Message);

                            await clientBot.SendTextMessageAsync(
                                                chatId: client.ChatId,
                                                text: message.Caption,
                                                parseMode: message.ParseMode
                                            );
                        }

                    }

                    message.Status = MessageStatus.Sent;
                    message.DateSent = DateTime.Now;

                    TelegramMessageDTO updatedMessage = await Model.UpdateTelegramMessage(message);
                    if (updatedMessage.IsFailedDTO)
                    {
                        _ = ServiceLogging<TelegramClientDTO>(logger, updatedMessage);
                    }
                    else
                    {
                        _ = ServiceLogging<TelegramClientDTO>(logger, 200,
                            $"Message '{message.Caption}' was sent to Telegram client '{client.ChatName}'");
                    }
                }
                catch (Exception ex)
                {
                    _ = ServiceLogging<TelegramClientDTO>(logger, 500, ex.Message);

                    message.Status = MessageStatus.Failed;
                    message.Comment = ex.Message;
                    TelegramMessageDTO updatedMessage = await Model.UpdateTelegramMessage(message);
                    if (updatedMessage.IsFailedDTO)
                    {
                        _ = ServiceLogging<TelegramClientDTO>(logger, updatedMessage);
                    }
                }
            }
        }
    }

    public async Task<string> SendTestMessage(TelegramClientDTO client)
    {
        try
        {
            TelegramBotClient clientBot = client.ToClient();           
            var res = await clientBot.SendTextMessageAsync(chatId: client.ChatId, text: "Test TG message", parseMode: ParseMode.Html);
            return string.Empty;
        }
        catch (Exception ex)
        {
            var err = $"{ex.Message} - ${ex.InnerException}";
            ServiceLogging<TelegramClientDTO>(logger, 500, err);
            return ex.Message;
        }
    }


    #endregion
}
