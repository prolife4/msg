using Microsoft.Extensions.Caching.Memory;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.Models.DTO;
using Vmx.Common.Logger;
using Vmx.Common.Services;

namespace Vmx.Messenger.Services.MQTT
{
    public interface IMQTTService
    {
        Task<MQTTClientDTO> AddNewMQTTClient(MQTTClientDTO client);
        Task<MQTTClientDTO> UpdateMQTTClient(MQTTClientDTO client);
        Task<MQTTClientDTO> DeleteMQTTClient(MQTTClientDTO client);
        Task<List<MQTTClientDTO>> GetMQTTClients();
        Task SendMessage(MQTTMessageDTO message);
    }
    public class MQTTService : VmxBaseService, IMQTTService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MQTTService));

        private MQTTDataModel Model => (MQTTDataModel)_model;
        public MQTTService(MessengerDbContext dbContext,
            IMemoryCache memoryCache) 
        { 
            _model = new MQTTDataModel(dbContext, memoryCache);
        }

        public async Task<MQTTClientDTO> AddNewMQTTClient(MQTTClientDTO client) =>
            await Model.AddMQTTClient(client);
        public async Task<MQTTClientDTO> UpdateMQTTClient(MQTTClientDTO client) =>
            await Model.UpdateMQTTClient(client);
        public async Task<MQTTClientDTO> DeleteMQTTClient(MQTTClientDTO client) =>
            await Model.DeleteMQTTClient(client);
        public async Task<List<MQTTClientDTO>> GetMQTTClients() =>
            await Model.GetMqttClients();

        public async Task SendMessage(MQTTMessageDTO message)
        {
            List<MQTTClientDTO> clients = await GetMQTTClients();
            if (clients.Count == 0)
            {
                VmxLogger.Instance.Error(logger, $"No one client were found to send message: {message}");
                return;
            }

            MQTTClientDTO client = clients.FirstOrDefault(c => c.IsActive);
            if (message.ClientId != -1)
            {
                client = clients.Find(c => c.Id == message.ClientId);
            }

            if (client == null)
            {
                VmxLogger.Instance.Error(logger, $"No one client were found to send message: {message}");
                return;
            }

            message.ClientId = client.Id;

            int attemptCount = 0;
            while (attemptCount < message.AttemptCount)
            {
                attemptCount++;
                message.AttemptCount = attemptCount;
                if (await client.SendMessage(message.GetMessageBuilder()))
                {
                    message.DateSent = DateTime.Now;
                    break;
                }
            }
        }
    }
}
