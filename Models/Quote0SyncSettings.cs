using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Quote0SyncPlugin.Models;

/// <summary>
/// Quote/0 课表同步插件设置。
/// </summary>
public class Quote0SyncSettings : INotifyPropertyChanged
{
    private bool _isEnabled;
    private bool _showVoiceHubSchedule = true;
    private string _apiKey = "";
    private string _deviceId = "";
    private string _taskKey = "";

    /// <summary>
    /// 是否启用 Quote/0 同步。
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Dot. App 中创建的 API Key（以 dot_app_ 开头）。
    /// </summary>
    public string ApiKey
    {
        get => _apiKey;
        set { var normalized = value.Trim(); if (_apiKey == normalized) return; _apiKey = normalized; OnPropertyChanged(); }
    }

    /// <summary>
    /// 目标设备序列号。
    /// </summary>
    public string DeviceId
    {
        get => _deviceId;
        set { var normalized = value.Trim(); if (_deviceId == normalized) return; _deviceId = normalized; OnPropertyChanged(); }
    }

    /// <summary>
    /// Canvas API 内容的 taskKey。设备上只有一个 Canvas API 内容时留空。
    /// </summary>
    public string TaskKey
    {
        get => _taskKey;
        set { var normalized = value.Trim(); if (_taskKey == normalized) return; _taskKey = normalized; OnPropertyChanged(); }
    }

    /// <summary>
    /// 是否显示 VoiceHub 广播站排期。
    /// </summary>
    public bool ShowVoiceHubSchedule
    {
        get => _showVoiceHubSchedule;
        set { if (_showVoiceHubSchedule == value) return; _showVoiceHubSchedule = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
