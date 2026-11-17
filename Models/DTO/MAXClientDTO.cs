using System.Collections.Concurrent;
using Vmx.Common.Models.DTO;
using Vmx.Messenger.DataBase.Models;

namespace Vmx.Messenger.Models.DTO;

public class MAXClientDTO : VmxBaseDTO, IMessengerClientDto
{
    public int Id { get; set; }
    public string ChatName { get; set; }
    public long ChatId { get; set; }
    public string Token { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Имя клиента для логов (см. <see cref="IMessengerClientDto.DisplayName"/>).</summary>
    public string DisplayName => ChatName;

    public ConcurrentQueue<MAXClientDTO> Messages { get; set; } = new();

    public MAXClientDTO() { }

    public MAXClientDTO(MAXClients client)
    { 
        Id = client.Id;
        ChatName = client.ChatName;
        ChatId = client.ChatId.GetValueOrDefault();
        Token = client.Token;
        Description = $"{client.ChatName}";
        IsActive = client.IsActive.GetValueOrDefault();
    }

    public MAXClients ToDbEntry() =>
        new()
        {
            Id = Id,
            ChatName = ChatName,
            ChatId = ChatId,
            Token = Token,
            IsActive = IsActive
        };

}
