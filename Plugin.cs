using System.IO;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quote0SyncPlugin.Models;
using Quote0SyncPlugin.Views.SettingsPages;

namespace Quote0SyncPlugin;

[PluginEntrance]
public class Plugin : PluginBase
{
    public Quote0SyncSettings Settings { get; set; } = new();

    private string SettingsPath => Path.Combine(PluginConfigFolder, "Settings.json");

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        Settings = ConfigureFileHelper.LoadConfig<Quote0SyncSettings>(SettingsPath);
        Settings.PropertyChanged += (_, _) => ConfigureFileHelper.SaveConfig(SettingsPath, Settings);

        services.AddSingleton(Settings);
        services.AddSettingsPage<Quote0SyncSettingsPage>();
        // 同时注册为单例和托管服务，使设置页可通过依赖注入解析到与后台推送相同的实例。
        services.AddSingleton<Quote0SyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<Quote0SyncService>());
    }
}
