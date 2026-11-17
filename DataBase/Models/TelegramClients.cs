using System.ComponentModel.DataAnnotations;

namespace Vmx.Messenger.DataBase.Models;

public class TelegramClients
{
    public int Id { get; set; }
    [MaxLength(150)]
    public string ChatName { get; set; }
    public long? ChatId { get; set; }
    [MaxLength(1000)]
    public string Token { get; set;}
    public bool? IsActive { get; set; }

}
