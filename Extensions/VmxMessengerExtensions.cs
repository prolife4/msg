using Microsoft.Extensions.DependencyInjection;
using Vmx.Messenger.DataBase;
using Vmx.Common.Builders.WebAppBuilder;

namespace Vmx.Messenger.Extensions
{
    public static class VmxMessengerExtensions
    {
        public static IServiceCollection AddMessengerServices(this IServiceCollection services,
            bool addEmailService = false, 
            bool addKafkaService = false,
            bool addMqttService = false, 
            bool addTeleramService = false, 
            bool addBackgrounds = false,
            bool addKafkaConsumer = false,
            bool addMaxService = false
            )
        {

            services.AddDbContext<MessengerDbContext>(options => SQLServiceBuilder.Instance.OnVmxConfigure(options));

            if (addEmailService)
            {
                services.AddEmailService(addBackgrounds);
            }

            if (addKafkaService)
            {
                services.AddKafkaService(addBackgrounds);
            }

            if (addKafkaConsumer)
            {
                services.AddKafkaConsumerService();
            }

            if (addMqttService)
            {
                services.AddMQTTService();
            }

            if (addTeleramService)
            {
                services.AddTelegramService(addBackgrounds);
            }

            if (addMaxService)
            {
                services.AddMAXService(addBackgrounds);
            }

            return services;
        }
    }
}
