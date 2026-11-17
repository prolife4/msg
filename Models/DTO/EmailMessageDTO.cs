using Newtonsoft.Json;
using Vmx.Common.Logger;
using Vmx.Common.Models.DTO;
using Vmx.Common.Utils;
using Vmx.Messenger.DataBase.Models;

namespace Vmx.Messenger.Models.DTO
{
    public enum EmailType
    {
        Notification = 0,
        Report = 1
    }
    public class EmailMessageDTO : VmxBaseDTO
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(EmailMessageDTO));

        public int Id { get; set; }
        public int? ClientId { get; set; }
        public string ToEmails { get; set; }
        public List<string> ToEmailsList => GetToEmailList();
        public string Subject { get; set; }
        public string Body { get; set; }

        [JsonIgnore]
        public Dictionary<string, byte[]> Attachments { get; set; } = new();
        public List<string> AttachmentKeys { get; set; } = new();
        public int? AttachmentCount { get; set; }
        public MessageStatus Status { get; set; }
        public int? AttemptCount { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateSent { get; set; }
        public EmailType Type { get; set; }
        public bool UseHtmlBody { get; set; }
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
        public EmailMessageDTO() { }

        public EmailMessageDTO(EmailMessages message)
        {
            Id = message.Id;
            ClientId = message.ClientId;
            ToEmails = message.ToEmails;
            Subject = message.Subject;
            Body = message.Body;
            AttachmentCount = message.AttachmentCount;
            Status = (MessageStatus)message.Status.GetValueOrDefault(0);
            AttemptCount = message.AttemptCount;
            DateCreated = message.DateCreated;
            DateSent = message.DateSent;
            Type = (EmailType)message.Type;
            UseHtmlBody = message.UseHtmlBody.GetValueOrDefault(true);
            Comment = message.Comment;
            // AttachmentKeys = string.IsNullOrWhiteSpace(message.Attachments) ? JsonConvert.DeserializeObject<List<string>>(message.Attachments) ?? new() : new();
        }

        public EmailMessages ToDbEntry() =>
            new()
            {
                Id = Id,
                ClientId = ClientId,
                ToEmails = ToEmails,
                Subject = Subject,
                Body = Body,
                AttachmentCount = AttachmentCount > 0 ? AttachmentCount : (Attachments?.Count ?? 0),
                Status = (int)Status,
                AttemptCount = AttemptCount,
                DateCreated = DateCreated.HasValue ? DateCreated : DateTime.Now,
                DateSent = DateSent,
                Type = (int)Type,
                UseHtmlBody = UseHtmlBody,
                Attachments = Attachments != null ? JsonConvert.SerializeObject(Attachments.Keys.ToList() ?? []) : null,
                Comment = Comment
            };

        private List<string> GetToEmailList()
        {
            if (string.IsNullOrEmpty(ToEmails))
            {
                return new();
            }

            ToEmails = ToEmails.Replace(",", " ").Replace(";", " ").Replace(":", " ");

            string[] mailArr = ToEmails.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            List<string> mailList = new();
            foreach (string mail in mailArr)
            {
                mailList.Add(mail.Trim());
            }
            return mailList;
        }

        public async Task AddFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }
            try
            {
                byte[] fileStream = FileUtils.GetBytesAsync(filePath).Result;
                //Uri uri = null;
                //if (Uri.TryCreate(filePath, UriKind.Absolute, out var absUri) && (absUri.Scheme == Uri.UriSchemeHttp || absUri.Scheme == Uri.UriSchemeHttps))
                //{
                //    uri = absUri;
                //}
                //else if (filePath.StartsWith('/') || filePath.StartsWith("~/"))  // Web-relative путь — резолвим и качаем

                //{
                //    uri = FileUtils.RelativePathResolve(filePath);
                //}

                //if (uri != null)
                //{
                //    using HttpClient client = new();
                //    fileStream = await client.GetByteArrayAsync(uri);
                //}
                //else
                //{
                //    fileStream = await File.ReadAllBytesAsync(filePath);
                //}

                if (!Attachments.TryAdd(filePath, fileStream))
                {
                    Attachments[filePath] = fileStream;
                }
            }
            catch (Exception ex)
            {
                VmxLogger.Instance.Error(logger, ex);
            }
        }
    }
}
