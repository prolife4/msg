using Newtonsoft.Json;
using Vmx.Common.Models.DTO;
using Vmx.Messenger.DataBase.Models;


namespace Vmx.Messenger.Models.DTO;

public class MAXMessageDTO : VmxBaseDTO
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Caption { get; set; }
    public string Body { get; set; }
    public List<string> Attachments { get; set; } = [];
    public int AttachmentCount { get; set; }
    public MessageStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateSent { get; set; }
    public string TextFormat { get; set; }
    public string Comment { get; set; }
    public string FormattedDateCreated => DateCreated.HasValue ? DateCreated.Value.ToString("yyyy-MM-dd HH:mm:ss") : "--";
    public string FormattedDateSent => DateSent.HasValue ? DateSent.Value.ToString("yyyy-MM-dd HH:mm:ss") : "--";

    public string FormattedStatus
    {
        get
        {
            string text = "Неизвестно";
            string color = "muted";
            switch (Status)
            {
                case MessageStatus.Sent:
                    text = "Отправлен";
                    color = "success";
                    break;
                case MessageStatus.Failed:
                    text = $"Ошибка: {Comment}";
                    color = "danger";
                    break;
                case MessageStatus.InQueue:
                    text = "В очереди";
                    color = "info";
                    break;
                case MessageStatus.Canceled:
                    text = "Отменен";
                    break;
            }

            return $"<span class='text-{color}'>{text}</span>";
        }
    }

    public MAXMessageDTO() { }

    public MAXMessageDTO(MAXMessages message)
    { 
        Id = message.Id;
        ClientId = message.ClientId.GetValueOrDefault();
        Caption = message.Caption;
        Body = message.Body;
        AttachmentCount = message.AttachmentCount.GetValueOrDefault();
        Status = (MessageStatus)message.Status.GetValueOrDefault();
        AttemptCount = message.AttemptCount.GetValueOrDefault();
        DateCreated = message.DateCreated.GetValueOrDefault();
        DateSent = message.DateSent.GetValueOrDefault();
        TextFormat = message.TextFormat;
       // ParseMode = (ParseMode)message.ParseMode.GetValueOrDefault();
        Comment = message.Comment;
        try
        { 
            Attachments = JsonConvert.DeserializeObject<List<string>>(message.Attachments);
        }
        finally
        {

        }
    }

    public MAXMessages ToDbEntry()
    {
        MAXMessages message = new()
        {
            Id = Id,
            ClientId = ClientId,
            Caption = Caption,
            Body = Body,
            AttachmentCount = AttachmentCount,
            Status = (int)Status,
            AttemptCount = AttemptCount,
            DateCreated = DateCreated,
            DateSent = DateSent,
            TextFormat = TextFormat,
          //  ParseMode = (int)ParseMode,
            Comment = Comment,
        };

        try
        {
            message.Attachments = JsonConvert.SerializeObject(Attachments);
        }
        finally
        {

        }

        return message;
    }
        

  
}
