using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.DataBase.Models;
using Vmx.Messenger.Models.DTO;
using Vmx.Common.Logger;
using Vmx.Common.Services.BaseModels;

namespace Vmx.Messenger.Services.MQTT
{
    internal class MQTTDataModel : BaseServiceDataModel
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MQTTDataModel));

        private MessengerDbContext DbContext => (MessengerDbContext)_dbContext;
        internal MQTTDataModel(MessengerDbContext dbContext, IMemoryCache memoryCahe) 
            : base(dbContext, memoryCahe)
        {
        }

        internal async Task<MQTTClientDTO> AddMQTTClient(MQTTClientDTO MQTTClient)
        {
            try
            {
                MqttClients MQTTClientDb = MQTTClient.ToDbEntry();
                await DbContext.MqttClients.AddAsync(MQTTClientDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return new(MQTTClientDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<MQTTClientDTO> UpdateMQTTClient(MQTTClientDTO MQTTClient)
        {
            try
            {
                MqttClients MQTTClientDb = MQTTClient.ToDbEntry();
                DbContext.MqttClients.Update(MQTTClientDb);
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return new(MQTTClientDb);
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<MQTTClientDTO> DeleteMQTTClient(MQTTClientDTO MQTTClient)
        {
            try
            {
                DbContext.MqttClients.Remove(await DbContext.MqttClients.FirstOrDefaultAsync(e => e.Id == MQTTClient.Id));
                await DbContext.SaveChangesAsync();

                DbContext.ChangeTracker.Clear();

                return MQTTClient;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new() { Message = new() { Error = new(404, e.Message) } };
            }
        }

        internal async Task<List<MQTTClientDTO>> GetMqttClients()
        {
            try
            {
                List<MqttClients> MqttClientsDb = await DbContext.MqttClients.AsNoTracking().ToListAsync();

                return MqttClientsDb.ConvertAll<MQTTClientDTO>(e => new(e)); ;
            }
            catch (Exception e)
            {
                VmxLogger.Instance.Error(logger, e);
                return new();
            }
        }
    }
}
