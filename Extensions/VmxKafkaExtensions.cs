using Microsoft.Extensions.DependencyInjection;
using Vmx.Messenger.Services.Kafka;
using Vmx.Common.Extensions;

namespace Vmx.Messenger.Extensions
{
    public static class VmxKafkaExtensions
    {
        public static IServiceCollection AddKafkaService(this IServiceCollection services, bool addBackgrounds)
        {
            services.AddScoped<IKafkaService, KafkaService>();

            if (addBackgrounds)
            {
                services.AddKafkaBackgrounds();
            }

            return services;
        }

        public static IServiceCollection AddKafkaConsumerService(this IServiceCollection services)
        {
            services.AddScoped<IKafkaConsumerService, KafkaConsumerService>();
            services.AddNewVmxBackgroundService(typeof(IKafkaConsumerService));
            return services;
        }

        public static IServiceCollection AddKafkaBackgrounds(this IServiceCollection services)
        {
            services.AddNewVmxBackgroundService(typeof(IKafkaService));
            return services;
        }
    }
}
