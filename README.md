# Quote0SyncPlugin | Quote/0 课表同步插件

将 ClassIsland 的当前课表、天气和广播排期通过 Canvas API 推送到 [Quote/0](https://sspai.com/create/quote0) (Dot.) 墨水屏。

## 功能

- 课程状态变化时立即刷新（上课/课间/放学/换课）。
- 倒计时仅在 30、15、5、1 分钟节点刷新，避免容易过期的秒级时钟，降低墨水屏耗电、闪烁和残影。
- 相同内容不重复请求；网络请求保持单请求在途并只保留最新状态。
- 支持失败自动重试（30s / 2m / 5m / 15m，遵循服务端 429 Retry-After）。

## 准备设备

1. 在 Dot App 的“更多 → API Keys”中创建 API Key（`dot_app_` 开头）。
2. 在内容工作室中为目标设备的循环任务添加一个 **Canvas API** 内容。不要添加 Image API。
3. 打开 ClassIsland“设置 → 插件 → Quote/0 课表同步”。
4. 启用同步，填写 API Key 和设备序列号。
5. 设备只有一个 Canvas API 内容时，`Canvas Task Key` 留空；存在多个时才填写目标内容的 `taskKey`。
6. 点击“测试”，确认当前课表可以显示。

## 开发说明

插件开发期通过 `ProjectReference` 引用仓库内的 `ClassIsland.Core` 以使用最新 API（`IWeatherService.LastWeatherInfo`）。
待相关 API 随 ClassIsland 发布后，请切换为 `ClassIsland.PluginSdk` 包引用。

---

Powered By LaoShui @ 2026
