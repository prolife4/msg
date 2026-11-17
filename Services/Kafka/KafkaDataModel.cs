using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.DataBase.Models;
using Vmx.Messenger.Models.DTO;
using Vmx.Common.Logger;
using Vmx.Common.Services.BaseModels;
using Vmx.Messenger.Models;

namespace Vmx.Messenger.Services.Kafka
{
    public class KafkaDataModel : BaseServiceDataModel
    {
        #region consts
        public const string ENTRYKEY_KAFKA_CLINET = "vmxKafkaClients";
        #endregion

        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(KafkaDataModel));

        private MessengerDbContext DbContext => (MessengerDbContext)_dbContext;
        public KafkaDataModel(MessengerDbContext dbContext, 
            IMemoryCache memoryCahe) : base(dbContext, memoryCahe)
        {
        }

        internal async Task<KafkaClientDTO> AddKafkaClient(KafkaClientDTO kafkaClient)
        {
            try
            {
                KafkaClients kafkaClientDb = kafkaClient.ToDbEntry();
                await DbContext.KafkaClients.AddAsync(kafkaClientDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();
                BaseServiceDataModel.ClearCache();
                return new(kafkaClientDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<KafkaClientDTO> UpdateKafkaClient(KafkaClientDTO kafkaClient)
        {
            try
            {
                KafkaClients kafkaClientDb = kafkaClient.ToDbEntry();
                DbContext.KafkaClients.Update(kafkaClientDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();
                BaseServiceDataModel.ClearCache();
                return new(kafkaClientDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<KafkaClientDTO> DeleteKafkaClient(KafkaClientDTO kafkaClient)
        {
            try
            {
                DbContext.KafkaClients.Remove(await DbContext.KafkaClients.FirstOrDefaultAsync(e => e.Id == kafkaClient.Id));
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();
                BaseServiceDataModel.ClearCache();
                return kafkaClient;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<List<KafkaClientDTO>> GetKafkaClients()
        {
            try
            {
                List<KafkaClientDTO> kafkaClients = GetItemsFromCache<List<KafkaClientDTO>>(ENTRYKEY_KAFKA_CLINET);
                if (kafkaClients == null || kafkaClients.Count == 0)
                {
                    List<KafkaClients> kafkaClientsDb = await DbContext.KafkaClients.AsNoTracking().ToListAsync();
                    kafkaClients = kafkaClientsDb.ConvertAll<KafkaClientDTO>(e => new(e));

                    foreach (KafkaClientDTO kafkaClient in kafkaClients)
                    {
                        List<KafkaSentMessageDTO> sentMessages = await GetKafkaClientSentMessages(kafkaClient.Id);
                        foreach (KafkaSentMessageDTO sentMessage in sentMessages)
                        {
                            kafkaClient.SentMessages.Enqueue(sentMessage);
                        }

                        kafkaClient.ReceivedMessages = await GetKafkaClientReceivedMessages(kafkaClient.Id);
                    }

                    if (kafkaClients.Count == 0)
                    {
                        //VmxLogger.Instance.Debug(logger, "No one Kafka Client were found");
                        return new();
                    }

                    AddItemsToCache(ENTRYKEY_KAFKA_CLINET, kafkaClients);
                }

                return kafkaClients;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new();
            }
        }

        internal async Task<KafkaSentMessageDTO> AddSentMessage(KafkaSentMessageDTO kafkaMessage)
        {
            try
            {
                kafkaMessage.Id = default;
                KafkaSentMessage kafkaMessageDb = kafkaMessage.ToDbEntry();
                await DbContext.KafkaSentMessage.AddAsync(kafkaMessageDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return new(kafkaMessageDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<KafkaSentMessageDTO> UpdateSentMessage(KafkaSentMessageDTO kafkaMessage)
        {
            try
            {
                KafkaSentMessage kafkaMessageDb = kafkaMessage.ToDbEntry();
                DbContext.KafkaSentMessage.Update(kafkaMessageDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return new(kafkaMessageDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<KafkaSentMessageDTO> DeleteSentMessage(KafkaSentMessageDTO kafkaMessage)
        {
            try
            {
                DbContext.KafkaSentMessage.Remove(await DbContext.KafkaSentMessage.FirstOrDefaultAsync(e => e.Id == kafkaMessage.Id));
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return kafkaMessage;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<List<KafkaSentMessageDTO>> GetKafkaClientSentMessages(int clientId)
        {
            try
            {
                List<KafkaSentMessage> kafkaMessages = await DbContext.KafkaSentMessage.AsNoTracking()
                    .Where(m => m.ClientId == clientId && m.Status == (int)MessageStatus.InQueue)
                    .OrderByDescending(m => m.DateCreated)
                    .ToListAsync();


                return kafkaMessages.ConvertAll<KafkaSentMessageDTO>(m => new(m));
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new();
            }
        }

        internal async Task<KafkaReceivedMessageDTO> AddReceivedMessage(KafkaReceivedMessageDTO kafkaMessage)
        {
            try
            {
                kafkaMessage.Id = default;
                KafkaReceivedMessage kafkaMessageDb = kafkaMessage.ToDbEntry();
                await DbContext.KafkaReceivedMessage.AddAsync(kafkaMessageDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return new(kafkaMessageDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<KafkaReceivedMessageDTO> UpdateReceivedMessage(KafkaReceivedMessageDTO kafkaMessage)
        {
            try
            {
                KafkaReceivedMessage kafkaMessageDb = kafkaMessage.ToDbEntry();
                DbContext.KafkaReceivedMessage.Update(kafkaMessageDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return new(kafkaMessageDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<KafkaReceivedMessageDTO> DeleteReceivedMessage(KafkaReceivedMessageDTO kafkaMessage)
        {
            try
            {
                DbContext.KafkaReceivedMessage.Remove(await DbContext.KafkaReceivedMessage.FirstOrDefaultAsync(e => e.Id == kafkaMessage.Id));
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return kafkaMessage;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<List<KafkaReceivedMessageDTO>> GetKafkaClientReceivedMessages(int clientId)
        {
            try
            {
                List<KafkaReceivedMessage> kafkaMessages = await DbContext.KafkaReceivedMessage.AsNoTracking()
                    .Where(m => (clientId == - 1 || m.ClientId == clientId) && m.Status == (int)MessageStatus.InQueue)
                    .OrderByDescending(m => m.DateCreated)
                    .ToListAsync();


                return kafkaMessages.ConvertAll<KafkaReceivedMessageDTO>(m => new(m));
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new();
            }
        }
    }
}
