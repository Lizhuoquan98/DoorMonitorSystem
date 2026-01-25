# 项目优化与待改进项总结 (Project Optimization & Roadmap)

## 1. 近期工作回顾 (Recent Optimizations)

自上一次提交以来，我们主要对项目的 **核心服务层稳定性** 和 **代码可维护性** 进行了深度优化：

### 1.1 核心服务层 (Core Services)
*   **通讯引擎 (`CommRuntime.cs`)**:
    *   **线程安全增强**: 修复了 Modbus TCP/RTU 通讯中的潜在竞态条件 (Race Condition)。在核心的 `Read` 和 `Write` 方法中引入了 `lock` 锁机制，确保同一通道在多线程环境下不会发生指令混淆，显著提高了设备通讯的稳定性。
    *   **日志性能优化**: 移除了高频调用的冗余 `Debug.WriteLine` 输出，统一收口至 `LogHelper`。这不仅减少了大量 IO 开销，也避免了调试模式下的界面卡顿。
    *   **文档标准化**: 为所有核心方法添加了详细的 XML 中文注释。

*   **设备通讯服务 (`DeviceCommunicationService.cs`)**:
    *   **去重日志逻辑**: 重构了日志记录逻辑，解决了同一错误或状态在日志文件中重复刷屏的问题。
    *   **代码注释**: 补充了完整的类与方法说明。

*   **时间同步服务 (`TimeSyncManager.cs` / `NtpService.cs`)**:
    *   完成了对时间同步逻辑（包括 NTP 客户端/服务端、PLC 对时协议）的完整文档化，明确了业务流程。

### 1.2 视图模型与 UI (ViewModels & Views)
*   **文档覆盖**: 完成了核心 ViewModel (`MainViewModel`, `TimeSyncViewModel`, `MainWindowViewModel`) 以及 View Code-Behind 文件的中文注释工作。
*   **逻辑梳理**: 明确了 UI 的数据驱动模式，解释了 `SelectedDoor` 联动以及弹窗逻辑的实现细节。

---

## 2. 待改进与建议 (Pending Improvements & Recommendations)

为了进一步提升系统的健壮性和扩展性，建议在后续版本中关注以下场景：

### 2.1 架构与模式
*   **引入依赖注入 (Dependency Injection)**: 
    *   *现状*: 目前代码中大量使用单例 (`Instance`) 和静态访问 (`GlobalData`)。
    *   *建议*: 引入 `Microsoft.Extensions.DependencyInjection`。将 `DeviceCommunicationService`, `NtpService` 等注册为单例服务，将 `MainViewModel` 注册为瞬态或单例。这将极大提高代码的可测试性（单元测试）和模块解耦。

### 2.2 UI 性能与体验
*   **海量设备渲染优化**:
    *   *现状*: `MainViewModel` 在启动时一次性加载并渲染所有 `StationViewModel`。
    *   *建议*: 如果站点或门禁设备数量增加到数百个，UI初始化会变慢。建议在 `ItemsControl` 或 `ListBox` 中启用 **UI 虚拟化 (Virtualization)**，或采用 **延迟加载/分页加载** 策略。
*   **异步操作反馈**:
    *   *建议*: 在执行耗时操作（如手动下发校时、重新连接设备）时，增加 UI 层的 `BusyIndicator` 或进度条反馈，提升用户体验。

### 2.3 异常处理与监控
*   **精细化错误分类**:
    *   *现状*: `CommRuntime` 将大多数异常统一捕获并简单记录。
    *   *建议*: 区分 **网络超时 (Timeout)**、**连接被拒 (ConnectionRefused)** 和 **协议错误 (ProtocolError)**。UI 可以根据这些不同状态显示不同的图标颜色（例如：超时显示黄色警告，拒绝连接显示红色断线），帮助运维人员更精确地定位硬件故障。

### 2.4 配置管理
*   **配置集中化**:
    *   *建议*: 将目前散落在代码常量或各个分散 JSON 文件的配置（如 NTP 默认端口、重试次数、心跳间隔）统一整合到标准的 `appsettings.json` 或数据库配置表中，并提供统一的配置读写接口。

---
**提交时间**: 2026-01-26
**提交人**: Antigravity AI
