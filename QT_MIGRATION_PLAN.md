# 🚀 站台门监控系统 - Qt/C++ 迁移计划

## 1. 🎯 目标
将现有的 WPF 站台门监控系统移植到 **C++ & Qt**，以实现：
- **极低的 CPU/内存占用**：针对老旧硬件和工控机优化。
- **原生高性能**：无 .NET 运行时开销，启动速度快。
- **高稳定性**：利用 Qt 健壮的信号/槽机制。

## 2. 🛠 技术栈

| 组件 | WPF (旧方案) | Qt (新方案) | 说明 |
| :--- | :--- | :--- | :--- |
| **编程语言** | C# (.NET 8) | **C++ 17** | 高性能工业软件标准 |
| **UI 框架** | WPF (XAML) | **Qt Widgets** | 相比 QML 更适合老旧硬件，CPU 绘制效率极高 |
| **构建系统** | Solution (.sln) | **CMake** | 跨平台标准，易于管理依赖 |
| **PLC 通信** | Sharp7 | **Snap7** | Sharp7 的 C++ 原型库 (极速) |
| **数据库** | MySQL.Data + Dapper | **Qt SQL (QSqlDatabase)** | 原生驱动支持 (QMYSQL) |
| **JSON** | Newtonsoft.Json | **QJsonDocument** | Qt 内置的高性能解析器 |
| **日志** | Custom/NLog | **SpdLog** / **QDebug** | C++ 领域推荐 SpdLog |

## 3. 🏗 架构设计

### 3.1 项目结构 (CMake)
```
DoorMonitorSystem_Qt/
├── CMakeLists.txt          # 主构建配置
├── src/
│   ├── main.cpp            # 程序入口
│   ├── Core/               # 核心单例 (ConfigManager, DataHub)
│   ├── Model/              # 数据实体 (Structs)
│   ├── View/               # UI 界面 (.h, .cpp, .ui)
│   │   ├── MainWindow      # 主窗口框架
│   │   ├── StationWidget   # 站点监控视图 (核心)
│   │   └── LoginDialog     # 登录弹窗
│   ├── Controller/         # 业务逻辑控制 (替代 ViewModels)
│   ├── Comm/               # PLC 通信封装 (Snap7)
│   └── Utils/              # 工具类 (加密 MD5, 助手函数)
├── resources/
│   ├── icons/              # 图标资源
│   └── styles/             # QSS (Qt样式表) - 用于 UI 美化
└── third_party/
    └── snap7/              # PLC 通信库源码
```

### 3.2 核心逻辑映射

#### A. 数据绑定 (最大变化)
*   **WPF**: 自动双向绑定 (XAML `<->` ViewModel)。
*   **Qt**: 显式更新机制。
    *   使用 **信号与槽 (Signals & Slots)**。
    *   例如：当 PLC 数据到达 -> 通信层发出 `dataUpdated(struct StationData)` 信号 -> 界面层槽函数接收并直接更新 `QLabel` 或重绘控件。

#### B. PLC 通信线程
*   必须使用 `QThread` 或 `QtConcurrent` 将 Snap7 的轮询操作放入后台，**绝对避免阻塞主 UI 线程**。
*   `CommManager` 将在独立线程运行，仅在数据变化时通知 UI。

#### C. 用户权限系统
*   使用 `UserManager` 单例重写四级权限逻辑。
*   UI 元素（按钮/菜单）通过检查 `UserManager::instance()->currentUser().role` 来调用 `setVisible(true/false)`。

## 4. 📅 迁移步骤

### 第一阶段：基础设施 (骨架)
1.  搭建 CMake 项目环境。
2.  集成 **Snap7** 库并验证 PLC 连接。
3.  实现 `ConfigManager`，解析现有的 `devices.json` 和 `SystemConfig.json` 配置文件。

### 第二阶段：核心 UI 与登录
1.  实现 `LoginDialog` (移植 MD5 加密逻辑)。
2.  创建 `MainWindow` 布局 (菜单栏、底部状态栏、中央堆栈窗口)。
3.  实现用户管理相关逻辑。

### 第三阶段：站点监控 (核心引擎)
1.  创建 `StationWidget` 自定义控件。
2.  **重写绘图逻辑**：在 `paintEvent` 中使用 `QPainter` 绘制站台门，这是在低配设备上流畅运行的关键。
3.  绑定 PLC 实时数据流。

### 第四阶段：高级功能
1.  日志视图 (使用 `QTableView` + `QSqlQueryModel`)。
2.  参数设置对话框。
3.  针对老旧设备的性能调优 (降低后台 Tab 的刷新率)。

## 5. 💡 老旧设备优化技巧
*   **绘图优化**: 坚决使用 `QPainter` 直接绘制大量重复的站台门状态，而不是创建成百上千个重型 Widget 控件。这将大幅降低内存和 CPU 开销。
*   **数据库**: 使用事务 (Transaction) 进行日志的批量插入。
*   **内存管理**: 在循环渲染中避免频繁的堆内存分配 (new/delete)，使用预分配缓冲区。

---

**下一步建议:**
是否开始创建 **CMake 项目结构** 并生成 `main.cpp` 和基础目录布局？这将为您开启 Qt 开发之旅。
