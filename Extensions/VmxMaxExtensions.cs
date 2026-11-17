using Microsoft.Extensions.DependencyInjection;
using Vmx.Messenger.Services.MAX;
using Vmx.Common.Extensions;

namespace Vmx.Messenger.Extensions;

public static class VmxMaxExtensions
{
    public static IServiceCollection AddMAXService(this IServiceCollection services, bool addBackgrounds)
    {
        services.AddScoped<IMAXService, MAXService>();

        if (addBackgrounds)
        {
            services.AddMaxBackgrounds();
        }

        return services;
    }

    public static IServiceCollection AddMaxBackgrounds(this IServiceCollection services)
    {
        services.AddNewVmxBackgroundService(typeof(IMAXService));
        return services;
    }
}
