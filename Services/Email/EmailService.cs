using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;
using Vmx.Common.Logger;
using Vmx.Common.Services;
using Vmx.Common.Services.Background;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.Models;
using Vmx.Messenger.Models.DTO;

namespace Vmx.Messenger.Services.Email
{
    public interface IEmailService : IBackgroundWorker
    {
        Task<EmailClientDTO> AddEmailClient(EmailClientDTO emailClient);
        Task<EmailClientDTO> UpdateEmailClient(EmailClientDTO emailClient);
        Task<EmailClientDTO> DeleteEmailClient(EmailClientDTO emailClient);
        Task<List<EmailClientDTO>> GetEmailClients();
        IQueryable<EmailMessageDTO> GetMessagesQuery(int clientId);
        Task<List<EmailMessageDTO>> GetEmailMessages(int clientId);
        Task<EmailMessageDTO> AddEmailMessage(EmailMessageDTO emailMessage);
        Task<EmailMessageDTO> UpdateEmailMessage(EmailMessageDTO emailMessage);
        Task<EmailMessageDTO> DeleteEmailMessage(EmailMessageDTO emailMessage);
        Task AddEmailMessageToSend(EmailMessageDTO message);
        Task<string> SendTestMessage(EmailClientDTO client);
    }

    public class EmailService : VmxBaseService, IEmailService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(EmailService));

        private EmailDataModel Model => (EmailDataModel)_model;
        public EmailService(MessengerDbContext dbContext,
            IMemoryCache memoryCache)
        {
            _model = new EmailDataModel(dbContext, memoryCache);
        }

        public async Task<EmailClientDTO> AddEmailClient(EmailClientDTO emailClient) =>
            await Model.AddEmailClient(emailClient);
        public async Task<EmailClientDTO> UpdateEmailClient(EmailClientDTO emailClient) =>
            await Model.UpdateEmailClient(emailClient);
        public async Task<EmailClientDTO> DeleteEmailClient(EmailClientDTO emailClient) =>
            await Model.DeleteEmailClient(emailClient);
        public async Task<List<EmailClientDTO>> GetEmailClients() =>
            await Model.GetEmailClients();

        public IQueryable<EmailMessageDTO> GetMessagesQuery(int clientId) =>
            Model.GetMessagesQuery(clientId);
        public async Task<List<EmailMessageDTO>> GetEmailMessages(int clientId) =>
            await Model.GetEmailClientMessages(clientId);
        public async Task<EmailMessageDTO> AddEmailMessage(EmailMessageDTO emailMessage) =>
            await Model.AddEmailMessage(emailMessage);
        public async Task<EmailMessageDTO> UpdateEmailMessage(EmailMessageDTO emailMessage) =>
            await Model.UpdateEmailMessage(emailMessage);
        public async Task<EmailMessageDTO> DeleteEmailMessage(EmailMessageDTO emailMessage) =>
            await Model.DeleteEmailClient(emailMessage);

        public async Task AddEmailMessageToSend(EmailMessageDTO message)
        {
            List<EmailClientDTO> emailClients = await Model.GetEmailClients();
            if (emailClients.Count == 0)
            {
                VmxLogger.Instance.Error(logger, $"Couldn't add Email message '{message.Subject}' because no one Email Clients found");
                return;
            }

            foreach (EmailClientDTO emailClient in emailClients)
            {
                if (!emailClient.IsActive.GetValueOrDefault())
                    continue;

                message.ClientId = emailClient.Id;
                message = await Model.AddEmailMessage(message);
                VmxLogger.Instance.Info(logger, $"Message '{message.Subject}' was added to Email client '{emailClient.Address}'");
            }
        }

        private static bool IsValidEmail(string email)
        {
            var attribute = new EmailAddressAttribute();
            return attribute.IsValid(email);
        }

        public async Task<string> SendTestMessage(EmailClientDTO client)
        {
            if (string.IsNullOrEmpty(client.TestAddress))
                return "Не указан адрес для отправки";

            if (!IsValidEmail(client.TestAddress))
                return "Не валидный адрес email";

            try
            {

                return await client.SendEmails(new EmailMessageDTO()
                {
                    Subject = $"VMX: Тестовое сообщение",
                    Body = @$"Это тестовое письмо для проверки работы почтового ящика.                              
                              Если вы получили это письмо, значит:
                                - Адрес электронной почты введён корректно
                                - Почтовый сервер работает
                                - Доставка писем настроена правильно

                              отправлено {DateTime.Now:yyyy-MM-dd HH:mm:ss}

                              С уважением, ВидеоМатрикс",
                    Type = EmailType.Notification,
                    UseHtmlBody = false,
                    ToEmails = client.TestAddress,
                    ClientId = client.Id
                });
            }
            catch (Exception ex)
            {
                ServiceLogging<EmailClientDTO>(logger, 500, $"{ex.Message} - ${ex.InnerException}");
                return ex.Message;
            }
        }


        #region Background implementation
        public async Task RunWorker()
        {
            List<EmailClientDTO> emailClients = await Model.GetEmailClients(true);
            if (emailClients.Count == 0)
            {
                return;
            }

            foreach (EmailClientDTO emailClient in emailClients)
            {
                List<EmailMessageDTO> emailMsg = await Model.GetEmailClientMessages(emailClient.Id);
                foreach (EmailMessageDTO emailMessage in emailMsg)
                {
                    _ = await emailClient.PrepareEmailAttacments(emailMessage);
                    emailMessage.AttemptCount = emailMessage.AttemptCount.GetValueOrDefault() + 1;
                    var sendResult = await emailClient.SendEmails(emailMessage);
                    if (string.IsNullOrEmpty(sendResult))
                    {
                        emailMessage.DateSent = DateTime.Now;
                        emailMessage.Status = MessageStatus.Sent;
                    }
                    else
                    {
                        emailMessage.Status = MessageStatus.Failed;
                        emailMessage.Comment = sendResult;
                    }

                    var vmxMessage = await Model.UpdateEmailMessage(emailMessage);
                    if (vmxMessage.IsFailedDTO)
                    {
                        VmxLogger.Instance.Error(logger, vmxMessage.FailedText);
                    }
                }
            }

            await Model.DeleteOldMessages(DateTime.Now.AddDays(-15));
        }

        //TODO: move to settings param
        public TimeSpan GetInterval() => TimeSpan.FromSeconds(60);
        public string GetName() => nameof(EmailService);


        #endregion
    }
}
