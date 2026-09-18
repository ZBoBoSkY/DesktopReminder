# 更新日志 CHANGELOG

本文件记录每个版本的变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)（主版本.次版本.修订号）。

## [v0.2.0] - 2026-09-19

### 新增（应用层 Core）
- 窗口三态状态机 `WindowStateMachine`：清晰 → 半透明 → 图标自动收起（超时可配置）、
  悬停/点击立即回清晰、提醒打断（图标弹回半透明保持一段时间）、状态变化事件
- 任务调度与提醒协调器 `ReminderCoordinator`：任务生命周期（新增/完成/稍后/恢复/删除）、
  新到期立即提醒、过期任务按提醒间隔重复提醒（默认 5 分钟）、
  图标数据（最早到期任务、角标数字）
- 接口定义：`IAppNameProvider`（软件名识别）、`IReminderNotifier`（提醒出口）

### 新增（基础设施 Infrastructure）
- JSON 持久化：任务仓储 + 配置仓储（便携版原则，数据在软件目录）
- 日志兜底 `FileLogger`：按天滚动，异常有据可查
- 前台窗口软件名识别 `ForegroundAppNameProvider`：Windows API 取前台进程 +
  内置 37 条中文映射表（explorer→文件管理器、doubaowork→豆包工作等）+ 用户自定义映射文件覆盖
- 系统提示音提醒 `SystemReminderNotifier`（不引入音频文件，保持体积小）

### 测试
- 新增 31 个单元测试（状态机 9、协调器 12、持久化 6、调度语义 4），累计 **53 个全部通过**

## [v0.1.0] - 2026-09-18

### 新增
- 工程骨架：解决方案 + 四层架构（Domain / Core / Infrastructure / App / Tests）
- 领域层完成：
  - 任务模型 `ReminderTask`（名称、备注、时长、到期时间、状态机：进行中/到期/已完成）
  - 任务核心计算 `ReminderScheduler`（到期判断、按到期时间排序、角标数字、倒计时格式化）
  - 配置模型 `AppSettings`（超时、透明度、提醒间隔、默认时长、主题、自启等）
  - 仓储接口（任务 / 配置）
- 22 个单元测试全部通过，覆盖：到期计算、排序、角标、倒计时格式、稍后重计时、完成/恢复

### 技术决策
- 技术栈：C# + WPF（替代 Python + PySide6，体积从 ~100MB 目标降至几 MB）
- 应用层命名 `DesktopReminder.Core`（规避与 WPF `Application` 类冲突）
- 任务状态与调度器命名 `ReminderTaskStatus` / `ReminderScheduler`（规避与 `System.Threading.Tasks` 冲突）
