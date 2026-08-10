using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassIsland.Core.Abstractions.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quote0SyncPlugin.Models;

namespace Quote0SyncPlugin;

/// <summary>
/// 将课程信息通过 Canvas API 同步到 Quote/0 (Dot.) 墨水屏。
/// </summary>
public class Quote0SyncService : IHostedService
{
    private const string DotBaseUrl = "https://dot.mindreset.tech";
    private const string DotTaskAlias = "ClassIsland Schedule";
    private static readonly TimeSpan DotDebounce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan[] DotRetryDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];
    private static readonly int[] DotCountdownThresholds = [30, 15, 5, 1];

    private static readonly JsonElement DotCanvasWindowData = JsonDocument.Parse("""
        {
          "default": [
            {
              "type": "div",
              "props": {
                "tw": "flex flex-col w-full h-full bg-white text-black p-[4px] gap-[2px] box-border overflow-hidden",
                "children": [
                  {
                    "type": "div",
                    "props": {
                      "tw": "flex flex-row items-center justify-between min-w-0 h-[16px]",
                      "children": [
                        {
                          "type": "div",
                          "props": {
                            "tw": "text-11-chillduansans font-semibold shrink-0",
                            "style": { "fontSize": "11px", "lineHeight": "14px", "whiteSpace": "nowrap" },
                            "children": "{{get inputData \"dateLine\" default=\"-\"}}"
                          }
                        },
                        {
                          "type": "div",
                          "props": {
                            "tw": "text-10-chillduansans min-w-0 flex-1",
                            "style": { "fontSize": "10px", "lineHeight": "13px", "lineClamp": 1, "overflow": "hidden", "textOverflow": "ellipsis", "whiteSpace": "nowrap", "textAlign": "center" },
                            "children": "{{get inputData \"weather\" default=\"\"}}"
                          }
                        },
                        {
                          "type": "div",
                          "props": {
                            "tw": "text-10-chillduansans shrink-0",
                            "style": { "fontSize": "10px", "lineHeight": "13px", "whiteSpace": "nowrap", "textAlign": "right" },
                            "children": "{{get inputData \"currentTime\" default=\"\"}}"
                          }
                        }
                      ]
                    }
                  },
                  {
                    "type": "div",
                    "props": {
                      "tw": "flex flex-row items-stretch min-w-0 flex-1 border-t border-b border-black gap-[8px]",
                      "style": { "paddingTop": "4px", "paddingBottom": "4px" },
                      "children": [
                        {
                          "type": "div",
                          "props": {
                            "tw": "flex flex-col flex-1 min-w-0 justify-center gap-[2px]",
                            "children": [
                              {
                                "type": "div",
                                "props": {
                                  "tw": "text-10-chillduansans",
                                  "style": { "fontSize": "10px", "lineHeight": "11px" },
                                  "children": "{{get inputData \"state\" default=\"\"}}"
                                }
                              },
                              {
                                "type": "div",
                                "props": {
                                  "tw": "text-26-chillduansans font-bold min-w-0",
                                  "style": { "fontSize": "26px", "fontWeight": 700, "lineHeight": "28px", "lineClamp": 1, "overflow": "hidden", "textOverflow": "ellipsis", "whiteSpace": "nowrap" },
                                  "children": "{{get inputData \"title\" default=\"-\"}}"
                                }
                              },
                              {
                                "type": "div",
                                "props": {
                                  "tw": "w-full h-[6px] rounded-full bg-black/10 border border-black overflow-hidden shrink-0",
                                  "children": [
                                    {
                                      "type": "div",
                                      "props": {
                                        "tw": "h-full rounded-full bg-black",
                                        "style": { "width": "{{get inputData \"progressPercent\" default=\"0\"}}%" }
                                      }
                                    }
                                  ]
                                }
                              },
                              {
                                "type": "div",
                                "props": {
                                  "tw": "text-10-chillduansans",
                                  "style": { "fontSize": "10px", "lineHeight": "12px", "whiteSpace": "nowrap" },
                                  "children": "{{get inputData \"remaining\" default=\"\"}}"
                                }
                              }
                            ]
                          }
                        },
                        {
                          "type": "div",
                          "props": {
                            "tw": "flex flex-col items-center justify-center shrink-0 border border-black rounded-[6px]",
                            "style": { "width": "64px", "paddingLeft": "4px", "paddingRight": "4px" },
                            "children": [
                              {
                                "type": "div",
                                "props": {
                                  "tw": "text-9-chillduansans",
                                  "style": { "fontSize": "9px", "lineHeight": "11px" },
                                  "children": "今日剩余"
                                }
                              },
                              {
                                "type": "div",
                                "props": {
                                  "tw": "text-22-chillduansans font-bold",
                                  "style": { "fontSize": "22px", "fontWeight": 700, "lineHeight": "24px" },
                                  "children": "{{get inputData \"remainingCount\" default=\"0\"}}"
                                }
                              },
                              {
                                "type": "div",
                                "props": {
                                  "tw": "text-9-chillduansans",
                                  "style": { "fontSize": "9px", "lineHeight": "11px" },
                                  "children": "节课程"
                                }
                              }
                            ]
                          }
                        }
                      ]
                    }
                  },
                  {
                    "type": "div",
                    "props": {
                      "tw": "flex flex-col min-w-0 gap-[1px]",
                      "children": [
                        {
                          "type": "div",
                          "props": {
                            "tw": "text-9-chillduansans font-semibold",
                            "style": { "fontSize": "9px", "fontWeight": 600, "lineHeight": "11px" },
                            "children": "{{get inputData \"scheduleTitle\" default=\"今日课表\"}}"
                          }
                        },
                        {
                          "type": "div",
                          "props": {
                            "tw": "text-10-chillduansans min-w-0",
                            "style": { "fontSize": "10px", "lineHeight": "13px", "whiteSpace": "normal", "wordBreak": "break-word" },
                            "children": "{{get inputData \"upcomingText\" default=\"\"}}"
                          }
                        }
                      ]
                    }
                  },
                  {
                    "type": "div",
                    "props": {
                      "tw": "text-10-chillduansans min-w-0 h-[13px] border-t border-black",
                      "style": { "fontSize": "10px", "lineHeight": "12px", "lineClamp": 1, "overflow": "hidden", "textOverflow": "ellipsis", "whiteSpace": "nowrap" },
                      "children": "{{get inputData \"footer\" default=\"暂无近期广播排期\"}}"
                    }
                  }
                ]
              }
            }
          ]
        }
        """).RootElement.Clone();

    private static readonly JsonElement DotCanvasLayoutFull = JsonDocument.Parse("""
        {
          "tw": "p-0 bg-white",
          "style": { "padding": 0, "backgroundColor": "#FFFFFF" }
        }
        """).RootElement.Clone();

    private ILessonsService LessonsService { get; }
    private IProfileService ProfileService { get; }
    private IWeatherService WeatherService { get; }
    private Quote0SyncSettings Settings { get; }
    private ILogger<Quote0SyncService> Logger { get; }
    private HttpClient DotHttpClient { get; } = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly object _snapshotStateLock = new();
    private readonly object _dotStateLock = new();
    private readonly SemaphoreSlim _payloadBuildSemaphore = new(1, 1);
    private readonly SemaphoreSlim _dotRequestSemaphore = new(1, 1);
    private readonly SemaphoreSlim _dotPumpSignal = new(0, 1);
    private readonly HashSet<string> _activeDotFingerprints = [];
    private DateTimeOffset _lastDotEvaluationTime = DateTimeOffset.MinValue;
    private SyncPayload? _latestPayload;
    private DateTimeOffset _latestPayloadBuiltAt = DateTimeOffset.MinValue;

    private DotSyncRequest? _pendingDotRequest;
    private bool _dotPumpRunning;
    private bool _dotConfigurationBlocked;
    private int _dotConfigurationRevision;
    private int _dotFailureCount;
    private DateTimeOffset _dotRetryAt = DateTimeOffset.MinValue;
    private string? _lastDotFingerprint;
    private string? _lastDotStateKey;
    private string? _dotCountdownStateKey;
    private int _dotCountdownLastDisplayedMinutes;
    private string _dotCountdownText = "";

    public Quote0SyncService(
        ILessonsService lessonsService,
        IProfileService profileService,
        IWeatherService weatherService,
        Quote0SyncSettings settings,
        ILogger<Quote0SyncService> logger)
    {
        LessonsService = lessonsService;
        ProfileService = profileService;
        WeatherService = weatherService;
        Settings = settings;
        Logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LessonsService.PostMainTimerTicked += LessonsServiceOnPostMainTimerTicked;
        Settings.PropertyChanged += SettingsOnPropertyChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        LessonsService.PostMainTimerTicked -= LessonsServiceOnPostMainTimerTicked;
        Settings.PropertyChanged -= SettingsOnPropertyChanged;
        return Task.CompletedTask;
    }

    private void LessonsServiceOnPostMainTimerTicked(object? sender, EventArgs e)
    {
        if (!Settings.IsEnabled)
            return;

        var now = DateTime.Now;
        EvaluateDotFromLatestSnapshot(now);
    }

    private void SettingsOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        lock (_dotStateLock)
        {
            _dotConfigurationRevision++;
            _dotConfigurationBlocked = false;
            _dotFailureCount = 0;
            _dotRetryAt = DateTimeOffset.MinValue;
            _lastDotFingerprint = null;
            _lastDotStateKey = null;
            _pendingDotRequest = null;
            _dotCountdownStateKey = null;
            _dotCountdownText = "";
        }
        WakeDotPump();

        if (Settings.IsEnabled)
            _ = RefreshDotAfterSettingsChangeAsync();
    }

    private async Task RefreshDotAfterSettingsChangeAsync()
    {
        try
        {
            var payload = await GetSyncPayloadAsync(forceRefresh: true);
            if (payload != null)
                QueueDotSync(payload, immediate: true);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "刷新 Quote/0 设置后的课表快照失败。");
        }
    }

    /// <summary>
    /// 测试推送。供设置页的“测试”按钮调用。
    /// </summary>
    public async Task<(bool IsSuccess, string Message)> TestSyncAsync()
    {
        var validationMessage = ValidateDotConfiguration();
        if (validationMessage != null)
            return (false, validationMessage);

        var data = await GetSyncPayloadAsync(forceRefresh: true);
        if (data == null)
            return (false, "当前没有加载的课表，无法生成测试画面。");

        int revision;
        lock (_dotStateLock)
        {
            _dotConfigurationRevision++;
            revision = _dotConfigurationRevision;
            _dotConfigurationBlocked = false;
            _dotFailureCount = 0;
            _dotRetryAt = DateTimeOffset.MinValue;
            _pendingDotRequest = null;
        }

        var request = BuildDotSyncRequest(data, revision, DateTimeOffset.MinValue);
        lock (_dotStateLock)
            _activeDotFingerprints.Add(request.Fingerprint);
        var outcome = await SendDotRequestAsync(request);
        ApplyDotOutcome(request, outcome, retryAutomatically: Settings.IsEnabled);
        return (outcome.IsSuccess, outcome.Message);
    }

    private void EvaluateDotFromLatestSnapshot(DateTime now)
    {
        var timestamp = new DateTimeOffset(now);
        if (timestamp - _lastDotEvaluationTime < TimeSpan.FromMilliseconds(250))
            return;

        _lastDotEvaluationTime = timestamp;
        var payload = GetLatestPayload();
        if (payload != null)
            QueueDotSync(payload);
    }

    private async Task<SyncPayload?> GetSyncPayloadAsync(bool forceRefresh = false)
    {
        var requestedAt = DateTimeOffset.Now;
        await _payloadBuildSemaphore.WaitAsync();
        try
        {
            lock (_snapshotStateLock)
            {
                if (_latestPayload != null &&
                    (_latestPayloadBuiltAt >= requestedAt ||
                     !forceRefresh && requestedAt - _latestPayloadBuiltAt < TimeSpan.FromSeconds(1)))
                    return _latestPayload;
            }

            var payload = await BuildPayloadAsync();
            lock (_snapshotStateLock)
            {
                _latestPayload = payload;
                _latestPayloadBuiltAt = DateTimeOffset.Now;
            }
            return payload;
        }
        finally
        {
            _payloadBuildSemaphore.Release();
        }
    }

    private SyncPayload? GetLatestPayload()
    {
        lock (_snapshotStateLock)
            return _latestPayload;
    }

    private async Task<SyncPayload?> BuildPayloadAsync()
    {
        var plan = LessonsService.CurrentClassPlan;
        if (plan == null)
            return null;

        var courses = new List<CoursePayload>();
        foreach (var classInfo in plan.Classes)
        {
            if (!ProfileService.Profile.Subjects.TryGetValue(classInfo.SubjectId, out var subject))
                continue;

            var timeItem = classInfo.CurrentTimeLayoutItem;
            courses.Add(new CoursePayload
            {
                Name = subject.Name,
                StartTime = timeItem.StartTime.ToString(@"hh\:mm"),
                EndTime = timeItem.EndTime.ToString(@"hh\:mm")
            });
        }

        var weatherInfo = WeatherService.LastWeatherInfo;
        var rainMin = weatherInfo?.Minutely?.Precipitation?.RainRemainingMinutes ?? 0;
        var rainMessage = "";
        if (rainMin > 0)
        {
            rainMessage = $"预计 {MinutesToApproxText(rainMin)} 后下雨";
        }
        else if (rainMin < 0)
        {
            rainMessage = $"预计 {MinutesToApproxText(-rainMin)} 后雨停";
        }

        var tomorrowCourses = new List<CoursePayload>();
        var tomorrowPlan = LessonsService.GetClassPlanByDate(DateTime.Today.AddDays(1), out _);
        if (tomorrowPlan != null)
        {
            foreach (var classInfo in tomorrowPlan.Classes)
            {
                if (!ProfileService.Profile.Subjects.TryGetValue(classInfo.SubjectId, out var subject))
                    continue;

                var timeItem = classInfo.CurrentTimeLayoutItem;
                tomorrowCourses.Add(new CoursePayload
                {
                    Name = subject.Name,
                    StartTime = timeItem.StartTime.ToString(@"hh\:mm"),
                    EndTime = timeItem.EndTime.ToString(@"hh\:mm")
                });
            }
        }

        return new SyncPayload
        {
            Date = DateTime.Today.ToString("yyyy-MM-dd"),
            Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            Weather = new WeatherPayload
            {
                Text = weatherInfo != null ? WeatherService.GetWeatherTextByCode(weatherInfo.Current.Weather) : "未知",
                Temperature = weatherInfo?.Current?.Temperature?.Value ?? "0",
                Rain = rainMessage,
                Warning = weatherInfo?.Alerts?.FirstOrDefault()?.Title ?? "",
                Alerts = weatherInfo?.Alerts?.Select(a => new AlertPayload
                {
                    Type = a.Type,
                    Level = a.Level,
                    Title = a.Title
                }).ToArray() ?? []
            },
            Courses = courses,
            TomorrowCourses = tomorrowCourses,
            VoiceHub = await GetVoiceHubTextAsync()
        };
    }

    private static async Task<string> GetVoiceHubTextAsync()
    {
        try
        {
            using var httpVoiceHub = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await httpVoiceHub.GetAsync("https://voicehub.lao-shui.top/api/songs/public");
            if (!response.IsSuccessStatusCode)
                return "";

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return "";

            var today = DateTime.Today;
            var futureDates = new HashSet<string>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("playDate", out var playDateElement))
                    continue;
                var playDate = playDateElement.GetString();
                if (string.IsNullOrEmpty(playDate) || playDate.Length < 10)
                    continue;
                var dateText = playDate[..10];
                if (DateTime.TryParse(dateText, out var date) && date.Date >= today)
                    futureDates.Add(dateText);
            }

            if (futureDates.Count == 0)
                return "";

            var targetDate = futureDates.OrderBy(x => x).First();
            var result = $"广播站排期 {targetDate}: ";
            var index = 1;
            var targetItems = document.RootElement.EnumerateArray()
                .Where(item => item.TryGetProperty("playDate", out var date) &&
                               date.GetString()?.StartsWith(targetDate) == true &&
                               item.TryGetProperty("song", out _))
                .OrderBy(item => item.TryGetProperty("sequence", out var sequence) ? sequence.GetInt32() : 999);

            foreach (var item in targetItems)
            {
                var song = item.GetProperty("song");
                var title = song.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? "" : "";
                var artist = song.TryGetProperty("artist", out var artistElement) ? artistElement.GetString() ?? "" : "";
                var requester = song.TryGetProperty("requester", out var requesterElement) ? requesterElement.GetString() ?? "" : "";
                result += $"#{index} {title}-{artist}";
                if (!string.IsNullOrEmpty(requester))
                    result += $"-{requester}";
                result += "  ";
                index++;
            }

            return result;
        }
        catch (Exception)
        {
            return "";
        }
    }

    private void QueueDotSync(SyncPayload data, bool immediate = false)
    {
        if (!Settings.IsEnabled)
            return;

        if (ValidateDotConfiguration() != null)
            return;

        lock (_dotStateLock)
        {
            var now = DateTimeOffset.Now;
            var request = BuildDotSyncRequest(data, _dotConfigurationRevision, now);
            if (request.Fingerprint == _lastDotFingerprint || _activeDotFingerprints.Contains(request.Fingerprint))
                return;

            if (request.Fingerprint == _pendingDotRequest?.Fingerprint)
            {
                if (immediate && _pendingDotRequest.NotBefore > now)
                {
                    _pendingDotRequest = _pendingDotRequest with { NotBefore = now };
                    WakeDotPump();
                }
                return;
            }

            var isStateTransition = _lastDotStateKey == null || request.StateKey != _lastDotStateKey;
            request = request with { NotBefore = immediate || isStateTransition ? now : now + DotDebounce };

            _pendingDotRequest = request;
            if (_dotConfigurationBlocked)
                return;
            if (_dotPumpRunning)
            {
                WakeDotPump();
                return;
            }

            _dotPumpRunning = true;
            _ = Task.Run(RunDotPumpAsync);
        }
    }

    private async Task RunDotPumpAsync()
    {
        while (true)
        {
            DotSyncRequest? request = null;
            TimeSpan delay;
            lock (_dotStateLock)
            {
                if (_dotConfigurationBlocked || _pendingDotRequest == null)
                {
                    _dotPumpRunning = false;
                    return;
                }

                var now = DateTimeOffset.Now;
                var notBefore = _pendingDotRequest.NotBefore > _dotRetryAt ? _pendingDotRequest.NotBefore : _dotRetryAt;
                delay = notBefore > now ? notBefore - now : TimeSpan.Zero;
                if (delay <= TimeSpan.Zero)
                {
                    request = _pendingDotRequest;
                    _pendingDotRequest = null;
                    _activeDotFingerprints.Add(request.Fingerprint);
                }
            }

            if (request == null)
            {
                await _dotPumpSignal.WaitAsync(delay);
                continue;
            }

            var outcome = await SendDotRequestAsync(request);
            ApplyDotOutcome(request, outcome, retryAutomatically: true);
        }
    }

    private void WakeDotPump()
    {
        try
        {
            _dotPumpSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // 已有唤醒信号，无需重复排队。
        }
    }

    private async Task<DotSendOutcome> SendDotRequestAsync(DotSyncRequest request)
    {
        await _dotRequestSemaphore.WaitAsync();
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{DotBaseUrl}/api/authV2/open/device/{Uri.EscapeDataString(request.Configuration.DeviceId)}/canvas");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.Configuration.ApiKey);

            var body = new Dictionary<string, object?>
            {
                ["refreshNow"] = true,
                ["taskAlias"] = DotTaskAlias,
                ["data"] = request.Data,
                ["windowData"] = DotCanvasWindowData,
                ["layoutFull"] = DotCanvasLayoutFull,
                ["border"] = 0
            };
            if (!string.IsNullOrWhiteSpace(request.Configuration.TaskKey))
                body["taskKey"] = request.Configuration.TaskKey;

            if (JsonSerializer.SerializeToUtf8Bytes(request.Data).Length > 64 * 1024)
                return new DotSendOutcome(false, false, null, "Dot Canvas 数据超过 64KB，已取消发送。");

            httpRequest.Content = JsonContent.Create(body);
            using var response = await DotHttpClient.SendAsync(httpRequest);
            if (response.IsSuccessStatusCode)
                return new DotSendOutcome(true, false, null, "Dot 当前课表已推送。");

            var errorDetail = await TryReadErrorDetailAsync(response);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new DotSendOutcome(false, true, null, $"Dot API 拒绝访问（HTTP {(int)response.StatusCode}），请检查 API Key。{errorDetail}");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new DotSendOutcome(false, true, null, $"Dot 返回 HTTP 404，请检查设备 ID，并确认循环任务中已添加 Canvas API 内容。{errorDetail}");
            if ((int)response.StatusCode == 429)
                return new DotSendOutcome(false, false, GetRetryAfter(response), "Dot API 请求过于频繁，已按服务端要求延后重试。");

            return new DotSendOutcome(false, false, null, $"Dot Canvas 请求失败（HTTP {(int)response.StatusCode}）。{errorDetail}");
        }
        catch (TaskCanceledException)
        {
            return new DotSendOutcome(false, false, null, "Dot Canvas 请求超时。");
        }
        catch (HttpRequestException)
        {
            return new DotSendOutcome(false, false, null, "无法连接 Dot OpenAPI。");
        }
        catch (Exception)
        {
            return new DotSendOutcome(false, false, null, "Dot Canvas 请求发生未知错误。");
        }
        finally
        {
            _dotRequestSemaphore.Release();
        }
    }

    private void ApplyDotOutcome(DotSyncRequest request, DotSendOutcome outcome, bool retryAutomatically)
    {
        lock (_dotStateLock)
        {
            _activeDotFingerprints.Remove(request.Fingerprint);
            if (request.ConfigurationRevision != _dotConfigurationRevision)
                return;

            if (outcome.IsSuccess)
            {
                _lastDotFingerprint = request.Fingerprint;
                _lastDotStateKey = request.StateKey;
                _dotFailureCount = 0;
                _dotRetryAt = DateTimeOffset.MinValue;
                Logger.LogInformation("Dot 当前课表同步成功。");
                return;
            }

            if (outcome.IsConfigurationError)
            {
                _dotConfigurationBlocked = true;
                Logger.LogWarning("{Message} 修改 Quote/0 设置或手动测试后将重新尝试。", outcome.Message);
                return;
            }

            var fallbackDelay = DotRetryDelays[Math.Min(_dotFailureCount, DotRetryDelays.Length - 1)];
            _dotFailureCount++;
            var retryDelay = outcome.RetryAfter is { } requestedDelay && requestedDelay > TimeSpan.Zero
                ? requestedDelay
                : fallbackDelay;
            _dotRetryAt = DateTimeOffset.Now + retryDelay;
            if (retryAutomatically && Settings.IsEnabled &&
                (_pendingDotRequest == null || _pendingDotRequest.Fingerprint == request.Fingerprint))
            {
                _pendingDotRequest = request with { NotBefore = _dotRetryAt };
            }
            if (_pendingDotRequest != null && !_dotPumpRunning && !_dotConfigurationBlocked)
            {
                _dotPumpRunning = true;
                _ = Task.Run(RunDotPumpAsync);
            }
            Logger.LogWarning("{Message} 将在 {Delay} 后重试。", outcome.Message, retryDelay);
        }
    }

    private DotSyncRequest BuildDotSyncRequest(
        SyncPayload payload,
        int configurationRevision,
        DateTimeOffset notBefore)
    {
        var data = BuildDotCanvasData(payload, DateTime.Now);
        var fingerprint = string.Join('\u001f',
            configurationRevision.ToString(CultureInfo.InvariantCulture),
            Settings.DeviceId,
            Settings.TaskKey,
            JsonSerializer.Serialize(data));
        return new DotSyncRequest(data, data.StateKey, GetDotConfiguration(), configurationRevision, fingerprint, notBefore);
    }

    private DotCanvasData BuildDotCanvasData(SyncPayload payload, DateTime now)
    {
        var today = ToCourseViews(payload.Courses);
        var tomorrow = ToCourseViews(payload.TomorrowCourses);
        var currentTime = now.TimeOfDay;
        var currentIndex = today.FindIndex(course => currentTime >= course.Start && currentTime < course.End);
        var nextIndex = today.FindIndex(course => course.Start > currentTime);

        var state = "";
        var title = "";
        var remaining = "";
        var referenceIndex = 0;
        var useTomorrowRows = false;
        var remainingCount = 0;
        var progressPercent = 0;
        string countdownStateKey;

        if (today.Count == 0)
        {
            state = "今日无课";
            title = "今天没有课程";
            useTomorrowRows = true;
            countdownStateKey = $"{payload.Date}:no-classes";
        }
        else if (currentIndex >= 0)
        {
            var current = today[currentIndex];
            state = "上课中";
            title = current.Name;
            remaining = GetAdaptiveCountdownText(
                $"{payload.Date}:class:{currentIndex}:{current.Start}",
                MinutesUntil(current.End, currentTime),
                "剩余 ",
                "即将下课");
            referenceIndex = currentIndex;
            remainingCount = today.Count - currentIndex;
            var totalMinutes = (current.End - current.Start).TotalMinutes;
            var elapsedMinutes = (currentTime - current.Start).TotalMinutes;
            progressPercent = totalMinutes > 0
                ? Math.Clamp((int)Math.Round(elapsedMinutes / totalMinutes * 100), 0, 100)
                : 0;
            countdownStateKey = $"{payload.Date}:class:{currentIndex}:{current.Start}";
        }
        else if (nextIndex >= 0)
        {
            var upcoming = today[nextIndex];
            var isBeforeSchool = nextIndex == 0;
            state = isBeforeSchool ? "未上课" : "课间";
            title = upcoming.Name;
            remaining = GetAdaptiveCountdownText(
                $"{payload.Date}:{state}:{nextIndex}:{upcoming.Start}",
                MinutesUntil(upcoming.Start, currentTime),
                "距上课 ",
                "即将上课");
            referenceIndex = nextIndex;
            remainingCount = today.Count - nextIndex;
            countdownStateKey = $"{payload.Date}:{state}:{nextIndex}:{upcoming.Start}";
        }
        else
        {
            state = "已放学";
            title = "今日课程结束";
            useTomorrowRows = true;
            countdownStateKey = $"{payload.Date}:after-school";
        }

        if (string.IsNullOrEmpty(remaining))
            SetCountdownState(countdownStateKey);

        // 剩余课程文本（横向 · 分隔，自动换行）：列举所有未上完的课程；已放学/无课时显示提示。
        string upcomingText;
        if (useTomorrowRows)
        {
            upcomingText = tomorrow.Count > 0
                ? $"明日 {tomorrow[0].Start:hh\\:mm} {tomorrow[0].Name}"
                : "明日暂无课程";
        }
        else
        {
            var parts = new List<string>();
            for (var i = referenceIndex; i < today.Count; i++)
            {
                var marker = i == currentIndex ? "● " : "";
                parts.Add($"{marker}{today[i].Start:hh\\:mm} {TruncateText(today[i].Name, 8)}");
            }
            upcomingText = string.Join(" · ", parts);
        }

        var weatherDetail = !string.IsNullOrWhiteSpace(payload.Weather.Warning)
            ? payload.Weather.Warning
            : payload.Weather.Rain;
        var weather = $"{payload.Weather.Text} {payload.Weather.Temperature}℃";
        if (!string.IsNullOrWhiteSpace(weatherDetail))
            weather += $" · {weatherDetail}";

        return new DotCanvasData
        {
            StateKey = countdownStateKey,
            DateLine = $"{now:M月d日} {GetChineseWeekday(now.DayOfWeek)}",
            Weather = TruncateText(weather, 12),
            CurrentTime = now.ToString("HH:mm"),
            State = state,
            Title = TruncateText(title, 8),
            Remaining = remaining,
            RemainingCount = remainingCount,
            ProgressPercent = progressPercent,
            ScheduleTitle = useTomorrowRows ? "明日课表" : "今日课表",
            UpcomingText = upcomingText,
            Footer = TruncateText(string.IsNullOrWhiteSpace(payload.VoiceHub) ? "暂无近期广播排期" : payload.VoiceHub, 52)
        };
    }

    private string GetAdaptiveCountdownText(string stateKey, int remainingMinutes, string prefix, string finalText)
    {
        lock (_dotStateLock)
        {
            if (_dotCountdownStateKey != stateKey)
            {
                _dotCountdownStateKey = stateKey;
                _dotCountdownLastDisplayedMinutes = remainingMinutes;
                _dotCountdownText = remainingMinutes <= 1 ? finalText : $"{prefix}{remainingMinutes} 分钟";
                return _dotCountdownText;
            }

            var crossedThreshold = DotCountdownThresholds
                .Where(threshold => remainingMinutes <= threshold && _dotCountdownLastDisplayedMinutes > threshold)
                .LastOrDefault();
            if (crossedThreshold > 0)
            {
                _dotCountdownLastDisplayedMinutes = crossedThreshold;
                _dotCountdownText = crossedThreshold == 1 ? finalText : $"{prefix}{crossedThreshold} 分钟";
            }

            return _dotCountdownText;
        }
    }

    private void SetCountdownState(string stateKey)
    {
        lock (_dotStateLock)
        {
            if (_dotCountdownStateKey == stateKey)
                return;
            _dotCountdownStateKey = stateKey;
            _dotCountdownLastDisplayedMinutes = 0;
            _dotCountdownText = "";
        }
    }

    private static List<CourseView> ToCourseViews(IEnumerable<CoursePayload> courses)
    {
        var result = new List<CourseView>();
        foreach (var course in courses)
        {
            if (!TimeSpan.TryParseExact(course.StartTime, @"hh\:mm", CultureInfo.InvariantCulture, out var start) ||
                !TimeSpan.TryParseExact(course.EndTime, @"hh\:mm", CultureInfo.InvariantCulture, out var end))
                continue;
            result.Add(new CourseView(course.Name, start, end));
        }
        return result.OrderBy(course => course.Start).ToList();
    }

    private static int MinutesUntil(TimeSpan target, TimeSpan current) =>
        Math.Max(1, (int)Math.Ceiling((target - current).TotalMinutes));

    private static string TruncateText(string text, int maxLength) =>
        text.Length <= maxLength ? text : $"{text[..Math.Max(0, maxLength - 1)]}…";

    private static string GetChineseWeekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日"
    };

    /// <summary>
    /// 将分钟数转换为近似时长文本，用于降水提示。
    /// </summary>
    private static string MinutesToApproxText(int minutes)
    {
        var absMinutes = Math.Abs(minutes);
        if (absMinutes == 0)
            return "0min";

        if (absMinutes >= 60)
        {
            var hours = absMinutes / 60.0;
            var rounded = Math.Round(hours * 2.0, MidpointRounding.AwayFromZero) / 2.0;
            var text = Math.Abs(rounded % 1.0) < 1e-9
                ? ((int)rounded).ToString(CultureInfo.CurrentCulture)
                : rounded.ToString("0.0", CultureInfo.CurrentCulture);
            return $"~{text}h";
        }

        return $"{absMinutes.ToString(CultureInfo.CurrentCulture)}min";
    }

    private DotConfiguration GetDotConfiguration() => new(
        Settings.ApiKey,
        Settings.DeviceId,
        Settings.TaskKey);

    private string? ValidateDotConfiguration()
    {
        if (!Settings.IsEnabled)
            return "未启用 Quote/0 同步。";
        if (string.IsNullOrWhiteSpace(Settings.ApiKey))
            return "请填写 Dot API Key。";
        if (!Settings.ApiKey.StartsWith("dot_app_", StringComparison.Ordinal))
            return "Dot API Key 格式无效，应以 dot_app_ 开头。";
        if (string.IsNullOrWhiteSpace(Settings.DeviceId))
            return "请填写 Dot 设备 ID。";
        return null;
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return delta;
        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.Now;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }
        return null;
    }

    /// <summary>
    /// 尝试从非成功响应的响应体中提取服务端的错误详情（message 字段），用于向用户展示具体失败原因。
    /// </summary>
    private static async Task<string> TryReadErrorDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
                return "";
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                var message = messageElement.GetString();
                return string.IsNullOrWhiteSpace(message) ? "" : $" 详情：{message}";
            }
            return "";
        }
        catch
        {
            return "";
        }
    }

    private sealed record DotConfiguration(string ApiKey, string DeviceId, string TaskKey);
    private sealed record DotSyncRequest(
        DotCanvasData Data,
        string StateKey,
        DotConfiguration Configuration,
        int ConfigurationRevision,
        string Fingerprint,
        DateTimeOffset NotBefore);
    private sealed record DotSendOutcome(
        bool IsSuccess,
        bool IsConfigurationError,
        TimeSpan? RetryAfter,
        string Message);
    private sealed record CourseView(string Name, TimeSpan Start, TimeSpan End);

    private sealed class SyncPayload
    {
        public string Date { get; init; } = "";
        public long Timestamp { get; init; }
        public WeatherPayload Weather { get; init; } = new();
        public IReadOnlyList<CoursePayload> Courses { get; init; } = [];
        public IReadOnlyList<CoursePayload> TomorrowCourses { get; init; } = [];
        public string VoiceHub { get; init; } = "";
    }

    private sealed class CoursePayload
    {
        public string Name { get; init; } = "";
        public string StartTime { get; init; } = "";
        public string EndTime { get; init; } = "";
    }

    private sealed class WeatherPayload
    {
        public string Text { get; init; } = "";
        public string Temperature { get; init; } = "";
        public string Rain { get; init; } = "";
        public string Warning { get; init; } = "";
        public IReadOnlyList<AlertPayload> Alerts { get; init; } = [];
    }

    private sealed class AlertPayload
    {
        public string Type { get; init; } = "";
        public string Level { get; init; } = "";
        public string Title { get; init; } = "";
    }

    private sealed class DotCanvasData
    {
        [JsonIgnore] public string StateKey { get; init; } = "";
        [JsonPropertyName("dateLine")] public string DateLine { get; init; } = "";
        [JsonPropertyName("weather")] public string Weather { get; init; } = "";
        [JsonPropertyName("currentTime")] public string CurrentTime { get; init; } = "";
        [JsonPropertyName("state")] public string State { get; init; } = "";
        [JsonPropertyName("title")] public string Title { get; init; } = "";
        [JsonPropertyName("remaining")] public string Remaining { get; init; } = "";
        [JsonPropertyName("remainingCount")] public int RemainingCount { get; init; }
        [JsonPropertyName("progressPercent")] public int ProgressPercent { get; init; }
        [JsonPropertyName("scheduleTitle")] public string ScheduleTitle { get; init; } = "";
        [JsonPropertyName("upcomingText")] public string UpcomingText { get; init; } = "";
        [JsonPropertyName("footer")] public string Footer { get; init; } = "";
    }
}
