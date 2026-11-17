using Microsoft.Extensions.DependencyInjection;
using Vmx.Messenger.Services.Telegram;
using Vmx.Common.Extensions;

namespace Vmx.Messenger.Extensions;

public static class VmxTelegramExtensions
{
    public static IServiceCollection AddTelegramService(this IServiceCollection services, bool addBackgrounds)
    {
        services.AddScoped<ITelegramService, TelegramService>();

        if (addBackgrounds)
        {
            services.AddTelegramBackgrounds();
        }

        return services;
    }

    public static IServiceCollection AddTelegramBackgrounds(this IServiceCollection services)
    {
        services.AddNewVmxBackgroundService(typeof(ITelegramService));
        return services;
    }
}
