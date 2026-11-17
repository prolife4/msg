using Microsoft.Extensions.DependencyInjection;
using Vmx.Common.Extensions;
using Vmx.Messenger.Services.Email;

namespace Vmx.Messenger.Extensions
{
    public static class VmxEmailExtensions
    {
        public static IServiceCollection AddEmailService(this IServiceCollection services, bool addBackgrounds)
        {
            services.AddScoped<IEmailService, EmailService>();

            if (addBackgrounds)
            {
                services.AddEmailBackgrounds();
            }

            return services;
        }

        public static IServiceCollection AddEmailBackgrounds(this IServiceCollection services)
        {
            services.AddNewVmxBackgroundService(typeof(IEmailService));
            return services;
        }
    }
}
