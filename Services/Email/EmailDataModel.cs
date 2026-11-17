using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.DataBase.Models;
using Vmx.Messenger.Models.DTO;
using Vmx.Common.Logger;
using Vmx.Common.Services.BaseModels;
using Vmx.Messenger.Models;

namespace Vmx.Messenger.Services.Email
{
    internal class EmailDataModel : BaseServiceDataModel
    {
        #region consts
        public const string ENTRYKEY_EMAIL_CLINET = "vmxEmailClients";
        #endregion

        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(EmailDataModel));

        private MessengerDbContext DbContext => (MessengerDbContext)_dbContext;
        public EmailDataModel(MessengerDbContext dbContext, IMemoryCache memoryCahe)
            : base(dbContext, memoryCahe)
        {
        }

        internal async Task<EmailClientDTO> AddEmailClient(EmailClientDTO emailClient)
        {
            try
            {
                EmailClients emailClientDb = emailClient.ToDbEntry();
                await DbContext.EmailClients.AddAsync(emailClientDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();
                BaseServiceDataModel.ClearCache();
                return new(emailClientDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<EmailClientDTO> UpdateEmailClient(EmailClientDTO emailClient)
        {
            try
            {
                EmailClients emailClientDb = emailClient.ToDbEntry();
                DbContext.EmailClients.Update(emailClientDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();
                BaseServiceDataModel.ClearCache();
                return new(emailClientDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<EmailClientDTO> DeleteEmailClient(EmailClientDTO emailClient)
        {
            try
            {
                DbContext.EmailClients.Remove(await DbContext.EmailClients.FirstOrDefaultAsync(e => e.Id == emailClient.Id));
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();
                BaseServiceDataModel.ClearCache();
                return emailClient;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<List<EmailClientDTO>> GetEmailClients(bool noCache = false)
        {
            try
            {
                List<EmailClientDTO> emailClients = GetItemsFromCache<List<EmailClientDTO>>(ENTRYKEY_EMAIL_CLINET);
                if (emailClients == null || emailClients.Count == 0 || noCache)
                {
                    List<EmailClients> emailClientsDb = await DbContext.EmailClients.AsNoTracking().ToListAsync();
                    emailClients = emailClientsDb.ConvertAll<EmailClientDTO>(e => new(e));

                    if (emailClients.Count == 0)
                    {
                        return new();
                    }

                    AddItemsToCache(ENTRYKEY_EMAIL_CLINET, emailClients);
                }

                return emailClients;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new();
            }
        }

        internal async Task<EmailMessageDTO> AddEmailMessage(EmailMessageDTO emailMessage)
        {
            try
            {
                emailMessage.Id = default;
                EmailMessages emailMessageDb = emailMessage.ToDbEntry();
                await DbContext.EmailMessages.AddAsync(emailMessageDb);
                await DbContext.SaveChangesAsync();
                VmxLogger.Instance.Info(logger, $"attachment cnt {emailMessage.Attachments.Values.Count} > {emailMessageDb.AttachmentCount}");

                if (emailMessage.Attachments.Count > 0)
                {
                    VmxLogger.Instance.Info(logger, $"add email attachment to cache id {emailMessageDb.Id.ToString()} cnt {emailMessage.Attachments.Values.Count}");
                    AddItemsToCache<Dictionary<string, byte[]>>(emailMessageDb.Id.ToString(), emailMessage.Attachments);
                }

                DbContext.ChangeTracker.Clear();

                return new(emailMessageDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<EmailMessageDTO> UpdateEmailMessage(EmailMessageDTO emailMessage)
        {
            try
            {
                EmailMessages emailMessageDb = emailMessage.ToDbEntry();
                DbContext.EmailMessages.Update(emailMessageDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                if (emailMessage.Status == MessageStatus.Sent)
                    DeleteItemFromCache(emailMessage.Id.ToString());

                return new(emailMessageDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<EmailMessageDTO> DeleteEmailClient(EmailMessageDTO emailMessage)
        {
            try
            {
                DbContext.EmailMessages.Remove(await DbContext.EmailMessages.FirstOrDefaultAsync(e => e.Id == emailMessage.Id));
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return emailMessage;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<List<EmailMessageDTO>> GetEmailClientMessages(int clientId)
        {
            try
            {
                List<EmailMessages> emailMessages = await DbContext.EmailMessages.AsNoTracking()
                    .Where(m => m.ClientId == clientId && (m.Status == (int)MessageStatus.InQueue || (m.Status == (int)MessageStatus.Failed && m.AttemptCount.GetValueOrDefault() < 5)))
                    .OrderBy(m => m.DateCreated)
                    .ToListAsync();
                var result = emailMessages.ConvertAll<EmailMessageDTO>(m => new(m));
                foreach (var r in result)
                {
                    VmxLogger.Instance.Info(logger, $"get attach id {r.Id} from dict");
                    var attachments = GetItemsFromCache<Dictionary<string, byte[]>>(r.Id.ToString());
                    r.Attachments = attachments;
                }

                return result;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new();
            }
        }

        internal IQueryable<EmailMessageDTO> GetMessagesQuery(int clientId)
        {
            try
            {
                return DbContext.EmailMessages.AsNoTracking()
                    .Where(m => m.ClientId == clientId)
                    .OrderBy(m => m.DateCreated)
                    .Select(x => new EmailMessageDTO(x));
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new List<EmailMessageDTO>().AsQueryable();
            }
        }
        internal async Task DeleteOldMessages(DateTime olderThan)
        {
            try
            {
                DbContext.EmailMessages.RemoveRange(DbContext.EmailMessages.Where(m => m.DateCreated <= olderThan && m.Status != (int)MessageStatus.InQueue));
                await DbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
            }
        }
    }
}
