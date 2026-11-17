using Microsoft.Extensions.DependencyInjection;
using Vmx.Messenger.Services.MQTT;

namespace Vmx.Messenger.Extensions
{
    public static class VmxMQTTServiceExtensions
    {
        public static IServiceCollection AddMQTTService(this IServiceCollection services)
        {
            services.AddScoped<IMQTTService, MQTTService>();

            return services;
        }
    }
}
