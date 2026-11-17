using System.Collections.Concurrent;
using Telegram.Bot;
using Vmx.Common.Models.DTO;
using Vmx.Messenger.DataBase.Models;

namespace Vmx.Messenger.Models.DTO;

public class TelegramClientDTO : VmxBaseDTO
{
    public int Id { get; set; }
    public string ChatName { get; set; }
    public long ChatId { get; set; }
    public string Token { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }

    public ConcurrentQueue<TelegramMessageDTO> Messages { get; set; } = new();

    public TelegramClientDTO() { }

    public TelegramClientDTO(TelegramClients client)
    { 
        Id = client.Id;
        ChatName = client.ChatName;
        ChatId = client.ChatId.GetValueOrDefault();
        Token = client.Token;
        Description = $"{client.ChatName}";
        IsActive = client.IsActive.GetValueOrDefault();
    }

    public TelegramClients ToDbEntry() =>
        new()
        {
            Id = Id,
            ChatName = ChatName,
            ChatId = ChatId,
            Token = Token,
            IsActive = IsActive
        };

    public TelegramBotClient ToClient() => 
        new(new TelegramBotClientOptions(Token));

}
