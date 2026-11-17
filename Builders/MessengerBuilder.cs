using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Vmx.Common.Builders.WebAppBuilder;
using Vmx.Common.Settings.Services;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.Extensions;

namespace Vmx.Messenger.Builders;

public class MessengerBuilder : IBaseWABuilder
{
    private IServiceCollection Services { get; set; }
    public bool UseDataBase => true;

    public void AddEmailSender()
    {

    }

    public void AddKendoUI()
    {

    }

    public void AddLogger(ILoggerFactory logger)
    {

    }

    public void AddProjectControlles()
    {
        
    }

    public void AddProjectServices()
    {
        Services.AddTelegramService(true);
        Services.AddEmailService(true); 
    }

    public void AddSignalR()
    {

    }

    public void ConfigureDbContext()
    {
        Services.AddDbContext<MessengerDbContext>(options => SQLServiceBuilder.Instance.OnVmxConfigure(options));
    }

    public void ConfigureWASettings()
    {
        VmxSettings.SetInstance(new VmxSettings());
    }

    public List<Assembly> GetAndLoadProjectAssemblies()
    {
        List<Assembly> assemblies = new()
            {
                Assembly.GetExecutingAssembly()
            };

        LoadAssemblies(assemblies);
        return assemblies;
    }

    private void LoadAssemblies(List<Assembly> assemblies)
    {
        if (assemblies != null)
        {
            foreach (Assembly assembly in assemblies)
            {
                Services.AddMvc().AddApplicationPart(assembly).AddControllersAsServices();
            }
        }
    }

    public void OnInit()
    {

    }

    public void OnStart(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=HomeApi}/{action=Index}/{id?}");
        });
    }

    public void SetConfigs(IServiceCollection services)
    {
        Services = services;
    }
}
