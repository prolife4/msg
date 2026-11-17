using Microsoft.Extensions.Caching.Memory;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.Models.DTO;
using Vmx.Common.Logger;
using Vmx.Common.Services;
using Vmx.Common.Services.Background;
using Vmx.Common.Extensions;
using Vmx.Common.Models.DTO;

namespace Vmx.Messenger.Services.Kafka
{
    public interface IKafkaService : IBackgroundWorker
    {
        Task<KafkaClientDTO> AddKafkaClient(KafkaClientDTO kafkaClient);
        Task<KafkaClientDTO> UpdateKafkaClient(KafkaClientDTO kafkaClient);
        Task<KafkaClientDTO> DeleteKafkaClient(KafkaClientDTO kafkaClient);
        Task<List<KafkaClientDTO>> GetKafkaClients();
        Task<KafkaClientDTO> GetKafkaClientById(int clientId);
        Task<KafkaSentMessageDTO> AddKafkaSentMessage(KafkaSentMessageDTO kafkaMessage);
        Task<KafkaSentMessageDTO> UpdateKafkaSentMessage(KafkaSentMessageDTO kafkaMessage);
        Task<KafkaSentMessageDTO> DeleteKafkaSentMessage(KafkaSentMessageDTO kafkaMessage);
        Task<KafkaReceivedMessageDTO> AddKafkaReceivedMessage(KafkaReceivedMessageDTO kafkaMessage);
        Task<KafkaReceivedMessageDTO> UpdateKafkaReceivedMessage(KafkaReceivedMessageDTO kafkaMessage);
        Task<KafkaReceivedMessageDTO> DeleteKafkaReceivedMessage(KafkaReceivedMessageDTO kafkaMessage);
    }

    public class KafkaService : VmxBaseService, IKafkaService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(KafkaService));

        private KafkaDataModel Model => (KafkaDataModel)_model;
        public KafkaService(MessengerDbContext dbContext,
            IMemoryCache memoryCache)
        {
            _model = new KafkaDataModel(dbContext, memoryCache);
        }

        public async Task<KafkaClientDTO> AddKafkaClient(KafkaClientDTO kafkaClient) =>
            await Model.AddKafkaClient(kafkaClient);
        public async Task<KafkaClientDTO> UpdateKafkaClient(KafkaClientDTO kafkaClient) =>
            await Model.UpdateKafkaClient(kafkaClient);
        public async Task<KafkaClientDTO> DeleteKafkaClient(KafkaClientDTO kafkaClient) =>
            await Model.DeleteKafkaClient(kafkaClient);
        public async Task<List<KafkaClientDTO>> GetKafkaClients() =>
            await Model.GetKafkaClients();
        public async Task<KafkaClientDTO> GetKafkaClientById(int clientId)
        { 
            List<KafkaClientDTO> kafkaClients = await Model.GetKafkaClients();
            if (kafkaClients.CheckEmptyOrFailedVmxCollection(out KafkaClientDTO failedElement))
            { 
                return failedElement;
            }

            KafkaClientDTO kafkaClient = kafkaClients.Find(k => k.Id == clientId);
            if (kafkaClient == null)
            {
                return VmxBaseDTO.ToFailedDTO<KafkaClientDTO>(VmxLogger.VMXLOGGER_WARN_CODE, 
                    $"Kafka client with id '{clientId}' not found in data base");
            }

            return kafkaClient;
        }

        public async Task<KafkaSentMessageDTO> AddKafkaSentMessage(KafkaSentMessageDTO kafkaMessage) =>
            await Model.AddSentMessage(kafkaMessage);
        public async Task<KafkaSentMessageDTO> UpdateKafkaSentMessage(KafkaSentMessageDTO kafkaMessage) =>
            await Model.UpdateSentMessage(kafkaMessage);
        public async Task<KafkaSentMessageDTO> DeleteKafkaSentMessage(KafkaSentMessageDTO kafkaMessage) =>
            await Model.DeleteSentMessage(kafkaMessage);

        public async Task<KafkaReceivedMessageDTO> AddKafkaReceivedMessage(KafkaReceivedMessageDTO kafkaMessage) =>
            await Model.AddReceivedMessage(kafkaMessage);
        public async Task<KafkaReceivedMessageDTO> UpdateKafkaReceivedMessage(KafkaReceivedMessageDTO kafkaMessage) =>
            await Model.UpdateReceivedMessage(kafkaMessage);
        public async Task<KafkaReceivedMessageDTO> DeleteKafkaReceivedMessage(KafkaReceivedMessageDTO kafkaMessage) =>
            await Model.DeleteReceivedMessage(kafkaMessage);

        public async Task AddKafkaMessageToSend(KafkaSentMessageDTO message)
        {
            List<KafkaClientDTO> kafkaClients = await GetKafkaClients();
            if (kafkaClients.Count == 0)
            {
                VmxLogger.Instance.Error(logger, $"Couldn't add Kafka message '{message.Records}' becouse no one Email Clients found");
                return;
            }

            foreach (KafkaClientDTO kafkaClient in kafkaClients)
            {
                message.ClientId = kafkaClient.Id;
                message = await AddKafkaSentMessage(message);

                kafkaClient.SentMessages.Enqueue(message);
                VmxLogger.Instance.Info(logger, $"Message '{message.Records}' was added to Kafka client '{kafkaClient.DatasetTopic}'");
            }
        }

        #region Background implementation
        //TODO: move to settings param
        public TimeSpan GetInterval() => TimeSpan.FromSeconds(5);
        public string GetName() => nameof(KafkaService);

        public async Task RunWorker()
        {
            List<KafkaClientDTO> kafkaClients = await GetKafkaClients();
            if (kafkaClients.Count == 0)
            {
                return;
            }

            foreach (KafkaClientDTO kafkaClient in kafkaClients)
            {
                if (kafkaClient.SentMessages.IsEmpty)
                {
                    VmxLogger.Instance.Debug(logger, $"No one message to send for topic '{kafkaClient.DatasetTopic}'");
                    continue;
                }

                await kafkaClient.SendData();
            }
        }
        #endregion
    }
}
