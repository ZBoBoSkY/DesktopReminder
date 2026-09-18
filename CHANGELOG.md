# 更新日志 CHANGELOG

本文件记录每个版本的变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)（主版本.次版本.修订号）。

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
