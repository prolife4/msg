using Microsoft.AspNetCore.StaticFiles;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using Vmx.Messenger.DataBase.Models;
using Vmx.Common.Logger;
using Vmx.Common.Models.DTO;
using Microsoft.Exchange.WebServices.Data;
using Vmx.Common.Utils;

namespace Vmx.Messenger.Models.DTO
{
    public class EmailClientDTO : VmxBaseDTO
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(EmailClientDTO));

        public int Id { get; set; }
        public string Address { get; set; }
        public string SubAddress { get; set; }
        public int? Port { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public bool? UseSsl { get; set; }
        public int? MaxAttemptCount { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDefault { get; set; }
        public bool? UseEWS { get; set; }
        public string Description { get; set; }
        public string TestAddress { get; set; }

        public ConcurrentQueue<EmailMessageDTO> Messages { get; set; } = new();

        public EmailClientDTO() { }

        public EmailClientDTO(EmailClients emailClient)
        {
            Id = emailClient.Id;
            Address = emailClient.Address;
            SubAddress = emailClient.SubAddress;
            Port = emailClient.Port;
            Login = emailClient.Login;
            Password = emailClient.Password;
            UseSsl = emailClient.UseSsl;
            MaxAttemptCount = emailClient.MaxAttemptCount;
            IsActive = emailClient.IsActive;
            IsDefault = emailClient.IsDefault;
            UseEWS = emailClient.UseEWS;
            Description = $"{emailClient.Address}_{emailClient.SubAddress}";
        }

        public EmailClients ToDbEntry() =>
            new()
            {
                Id = Id,
                Address = Address,
                SubAddress = SubAddress,
                Port = Port,
                Login = Login,
                Password = Password,
                UseSsl = UseSsl,
                MaxAttemptCount = MaxAttemptCount,
                IsActive = IsActive,
                IsDefault = IsDefault,
                UseEWS = UseEWS
            };

        public List<MailMessage> TryGetMailMessages(out EmailMessageDTO message)
        {
            message = new() { Message = new() { Error = new(404, "No one message to send were found") } };
            if (string.IsNullOrEmpty(Address) || string.IsNullOrEmpty(SubAddress) ||
               Port == null)
            {
                return new();
            }

            Messages.TryDequeue(out message);
            if (message == null)
            {
                return new();
            }

            List<MailMessage> emailMessages = new();
            FileExtensionContentTypeProvider fileProvider = new();

            foreach (string email in message.ToEmailsList)
            {
                MailMessage mailMsg = new()
                {
                    From = new MailAddress(Address),
                    Subject = message.Subject,
                    Body = message.Body,
                    IsBodyHtml = message.UseHtmlBody,
                };

                mailMsg.To.Add(new MailAddress(email));

                foreach (KeyValuePair<string, byte[]> attachment in message.Attachments)
                {
                    string fileName = Path.GetFileName(attachment.Key);
                    fileProvider.TryGetContentType(fileName, out string fileType);
                    MemoryStream ms = new();
                    ms.Write(attachment.Value, 0, attachment.Value.Length);
                    mailMsg.Attachments.Add(new System.Net.Mail.Attachment(ms, fileName, fileType));
                }

                emailMessages.Add(mailMsg);
            }

            return emailMessages;
        }

        public async Task<bool> PrepareEmailAttacments(EmailMessageDTO emailMessage)
        {
            if (emailMessage?.Attachments == null || emailMessage?.Attachments?.Count <= 0)
            {
                VmxLogger.Instance.Info(logger, $"email message has no attachments");
                return true;
            }

            VmxLogger.Instance.Info(logger, $"prepare attachments cnt {emailMessage?.Attachments?.Count}");
            foreach (var attachment in emailMessage.Attachments)
            {
                // if (attachment.Value.Length > 0) continue;
                VmxLogger.Instance.Info(logger, $"prepare attachments add file {attachment.Key}");
                await emailMessage.AddFile(attachment.Key);
            }
            return true;
        }

        public List<MailMessage> PrepareMailMessages(EmailMessageDTO message)
        {
            List<MailMessage> emailMessages = new();

            if (string.IsNullOrEmpty(Address) || string.IsNullOrEmpty(SubAddress) ||
               Port == null)
            {
                return emailMessages;
            }

            FileExtensionContentTypeProvider fileProvider = new();

            foreach (string email in message.ToEmailsList)
            {
                MailMessage mailMsg = new()
                {
                    From = new MailAddress(Address),
                    Subject = message.Subject,
                    Body = message.Body,
                    IsBodyHtml = message.UseHtmlBody,
                };

                mailMsg.To.Add(new MailAddress(email));

                foreach (KeyValuePair<string, byte[]> attachment in message?.Attachments ?? [])
                {
                    var fileBytes = attachment.Value;
                    fileBytes ??= FileUtils.GetBytesAsync(attachment.Key).Result;
                    string fileName = Path.GetFileName(attachment.Key);
                    fileProvider.TryGetContentType(fileName, out string fileType);
                    if (fileBytes?.Length > 0)
                    {
                        MemoryStream ms = new(fileBytes);
                        mailMsg.Attachments.Add(new System.Net.Mail.Attachment(ms, fileName, fileType));
                    }
                }

                emailMessages.Add(mailMsg);
            }

            return emailMessages;
        }
        public async Task<string> SendEmails(EmailMessageDTO emailMessage)
        {
            if (UseEWS.GetValueOrDefault() == true)
                return await SendEmailsEWS(emailMessage);

            var messages = PrepareMailMessages(emailMessage);
            return await SendEmails(messages);
        }
        public async Task<string> SendEmails(List<MailMessage> emailMessages)
        {
            try
            {
                using SmtpClient client = new()
                {
                    Host = SubAddress,
                    Port = Port.GetValueOrDefault(),
                    EnableSsl = UseSsl.GetValueOrDefault(),
                    Credentials = new NetworkCredential(Login, Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                foreach (MailMessage mail in emailMessages)
                {
                    try
                    {
                        await client.SendMailAsync(mail);
                        VmxLogger.Instance.Info(logger, $"Message '{mail.Subject}' was sent to '{mail.To[0].Address}'");
                    }
                    catch (Exception ex)
                    {
                        VmxLogger.Instance.Error(logger, ex);
                    }
                    finally
                    {
                        mail.Dispose();
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                VmxLogger.Instance.Error(logger, ex);
                return $"{ex.Message}: {ex.InnerException}";
            }
        }

        public async Task<string> SendEmailsEWS(EmailMessageDTO emailMessage)
        {
            try
            {
                ExchangeService service = new()
                {
                    Credentials = new WebCredentials(Login, Password),
                    TraceEnabled = true,
                    TraceFlags = TraceFlags.All,
                    Url = new Uri($"https://{SubAddress}/EWS/Exchange.asmx")
                };

                foreach (string email in emailMessage.ToEmailsList)
                {
                    EmailMessage mail = new(service);
                    mail.ToRecipients.Add(email);
                    mail.Subject = emailMessage.Subject;
                    mail.Body = emailMessage.Body;
                    mail.Body.BodyType = emailMessage.UseHtmlBody ? BodyType.HTML : BodyType.Text;

                    foreach (var a in emailMessage.Attachments ?? new())
                    {
                        mail.Attachments.AddFileAttachment(a.Key, a.Value);
                        VmxLogger.Instance.Info(logger, $"add attachment {a.Key}");
                    }
                    mail.Send();
                    VmxLogger.Instance.Info(logger, $"Message '{mail.Subject}' was sent to '{email}' with EWS");
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                VmxLogger.Instance.Error(logger, ex);
                return ex.Message;
            }
        }
    }
}
