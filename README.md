# Quote0SyncPlugin | Quote/0 课表同步插件

将 ClassIsland 的当前课表、天气和广播排期通过 Canvas API 推送到 [Quote/0](https://sspai.com/create/quote0) (Dot.) 墨水屏。

## 功能

- 课程状态变化时立即刷新（上课/课间/放学/换课）。
- 上课倒计时和长课间倒计时仅在 30、20、10、5、1 分钟节点刷新；15 分钟以内的短课间按分钟递减刷新，降低墨水屏耗电和残影。
- 剩余课程较多时按每页 4 项分页，每 5 分钟切换一页。
- 存在多个气象预警时按页轮播并显示序号；无预警时仍展示降水提示。
- VoiceHub 广播站排期可在设置中隐藏；关闭后底部一行改为显示接下来的课程。
- 相同内容不重复请求；网络请求保持单请求在途并只保留最新状态。
- 支持失败自动重试（30s / 2m / 5m / 15m，遵循服务端 429 Retry-After）。

## 准备设备

1. 在 Dot App 的“更多 → API Keys”中创建 API Key（`dot_app_` 开头）。
2. 在内容工作室中为目标设备的循环任务添加一个 **Canvas API** 内容。不要添加 Image API。
3. 打开 ClassIsland“设置 → 插件 → Quote/0 课表同步”。
4. 启用同步，填写 API Key 和设备序列号。
5. 设备只有一个 Canvas API 内容时，`Canvas Task Key` 留空；存在多个时才填写目标内容的 `taskKey`。
6. 可选：点击“测试”，立即检查当前课表能否显示。

---

Powered By LaoShui @ 2026
