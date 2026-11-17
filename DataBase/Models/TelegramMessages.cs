using System.ComponentModel.DataAnnotations;

namespace Vmx.Messenger.DataBase.Models;

public class TelegramMessages
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    [MaxLength(350)]
    public string Caption { get; set; }
    public string Body { get; set; }
    public string Attachments { get; set; }
    public int? AttachmentCount { get; set; }
    public int? Status { get; set; }
    public int? AttemptCount { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateSent { get; set; }
    public int? ParseMode { get; set; }
    public string Comment { get; set; }
}
