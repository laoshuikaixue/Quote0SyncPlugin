using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Shared;
using Quote0SyncPlugin.Models;

namespace Quote0SyncPlugin.Views.SettingsPages;

[SettingsPageInfo("laoshui.quote0-sync", "Quote/0 课表同步")]
public partial class Quote0SyncSettingsPage : SettingsPageBase
{
    public Quote0SyncSettings Settings { get; } = IAppHost.GetService<Quote0SyncSettings>();

    public Quote0SyncService SyncService { get; } = IAppHost.GetService<Quote0SyncService>();

    public Quote0SyncSettingsPage()
    {
        InitializeComponent();
        DataContext = Settings;
    }

    private async void TestButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        button.IsEnabled = false;
        try
        {
            var (isSuccess, message) = await SyncService.TestSyncAsync();
            if (isSuccess)
                this.ShowSuccessToast(message);
            else
                this.ShowErrorToast(message);
        }
        catch (Exception ex)
        {
            this.ShowErrorToast("测试失败", ex);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
