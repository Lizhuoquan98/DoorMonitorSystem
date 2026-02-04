# 🚇 站台门监控系统 (Platform Screen Door Monitor System)

一个基于 WPF 的工业级站台门监控系统，专为地铁、轻轨等轨道交通站台门设备设计。支持与西门子 PLC 通信，实现实时设备监控、故障诊断、配置管理和数据记录功能。

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)
![PLC](https://img.shields.io/badge/PLC-Siemens%20S7-009999)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 📋 目录

- [系统简介](#-系统简介)
- [功能特性](#-功能特性)
- [技术栈](#-技术栈)
- [项目结构](#-项目结构)
- [快速开始](#-快速开始)
- [系统要求](#-系统要求)
- [配置说明](#-配置说明)
- [核心功能](#-核心功能)
- [应用场景](#-应用场景)
- [贡献指南](#-贡献指南)

---

## 🚆 系统简介

**站台门监控系统**是专为轨道交通（地铁、轻轨、城际铁路）站台门设备开发的综合监控平台。系统通过与站台门控制 PLC 实时通信，监控站台门的开关状态、故障信息、应急状态等，确保站台门系统的安全可靠运行。

### 适用场景
- 🚇 **地铁站台门监控** - 全高/半高站台门系统
- 🚄 **轻轨站台门监控** - BRT、APM 等系统
- 🏢 **车站综合监控中心** - 多站点集中监控
- 🔧 **站台门维护管理** - 故障诊断与维修记录

---

## ✨ 功能特性

### 核心功能
- 🔌 **PLC 实时通信** - 支持西门子 S7 系列 PLC，实时采集站台门设备数据
- 📊 **多站点监控** - 支持多个地铁站、多条线路的集中监控
- 🚪 **门状态监控** - 实时显示每扇站台门的开/关/故障状态
- ⚠️ **故障报警** - 多级别报警模式，及时发现设备异常
- 🎨 **图形化界面** - 可视化的站台门布局图，直观显示设备状态
- 📝 **运行记录** - 完整记录站台门开关动作、故障事件
- ⚙️ **设备配置** - 灵活的设备参数配置和管理
- 📈 **数据统计** - 站台门运行数据统计与分析

### 高级功能
- 🔧 **远程控制** - 支持远程开关门操作（需权限）
- 🧪 **故障诊断** - 智能故障诊断与维修建议
- 📋 **维护管理** - 维护计划、维修记录管理
- 💾 **历史数据** - 长期历史数据存储与查询
- 🖥️ **多屏显示** - 支持大屏幕显示模式

---

## 🛠️ 技术栈

### 核心框架
- **.NET 8.0** - 最新的 .NET 平台
- **WPF (Windows Presentation Foundation)** - 桌面应用程序框架
- **C# Preview** - 使用最新的 C# 语言特性
- **MVVM 架构** - 模型-视图-视图模型设计模式

### 主要依赖库
| 库名 | 版本 | 用途 |
|------|------|------|
| **Sharp7** | 1.1.84 | 西门子 S7 PLC 通信协议 |
| **MySql.Data** | 9.3.0 | MySQL 数据库连接 |
| **Dapper** | 2.1.66 | 轻量级 ORM 框架 |
| **Newtonsoft.Json** | 13.0.3 | JSON 序列化/反序列化 |
| **Extended.Wpf.Toolkit** | 4.7.25104.5739 | WPF 扩展控件库 |
| **Microsoft.Xaml.Behaviors.Wpf** | 1.1.135 | XAML 行为支持 |

### 工业协议支持
- **S7 协议** - 支持 S7-200/300/400/1200/1500 系列
- **位级通信** - 支持位（Bit）级别的状态读取
- **高频采集** - 毫秒级数据刷新

---

## 📁 项目结构

```
DoorMonitorSystem/
│
├── Views/                          # 视图层 (XAML 界面)
│   ├── MainWindow.xaml            # 主窗口
│   ├── MainView.xaml              # 主视图
│   ├── StationView.xaml           # 站点监控视图 ⭐
│   ├── GraphicEditingView.xaml    # 图形编辑视图（站台门布局）
│   ├── DevvarlistView.xaml        # 设备变量列表
│   ├── DeployView.xaml            # 部署配置视图
│   └── ConfigureView.xaml         # 系统配置视图
│
├── ViewModels/                     # 视图模型层 (业务逻辑)
│   ├── MainWindowViewModel.cs
│   ├── StationViewModel.cs        # 站点监控业务逻辑 ⭐
│   ├── GraphicEditingViewModel.cs # 图形编辑业务逻辑
│   ├── DevvarlistViewModel.cs     # 设备变量管理
│   ├── DeployViewModel.cs         # 部署配置逻辑
│   └── ConfigureViewModel.cs      # 系统配置逻辑
│
├── Models/                         # 数据模型层
│   ├── ConfigEntity/              # 配置实体
│   │   ├── Door/                  # 站台门相关实体 ⭐
│   │   │   ├── DoorEntity.cs          # 站台门实体
│   │   │   ├── DoorTypeEntity.cs      # 站台门类型（全高/半高）
│   │   │   ├── DoorBitConfigEntity.cs # 站台门位配置
│   │   │   └── DoorGroupEntity.cs     # 站台门分组
│   │   ├── Group/                 # 面板组配置
│   │   │   ├── PanelEntity.cs         # 监控面板实体
│   │   │   ├── PanelGroupEntity.cs    # 面板组
│   │   │   └── PanelBitConfigEntity.cs # 面板位配置
│   │   ├── StationEntity.cs       # 站点实体（地铁站） ⭐
│   │   └── BitColorEntity.cs      # 状态颜色配置
│   │
│   ├── RunModels/                 # 运行时模型
│   │   ├── DoorModel.cs           # 站台门运行模型
│   │   ├── DoorGroup.cs           # 站台门组
│   │   ├── DoorVisualResult.cs    # 站台门可视化结果 ⭐
│   │   ├── PanelModel.cs          # 监控面板模型
│   │   ├── PanelGroup.cs          # 面板组
│   │   └── StationMainGroup.cs    # 站点主组 ⭐
│   │
│   ├── Points/                    # 点位模型（PLC 数据点）
│   │   ├── DevicePoint.cs         # 设备点位
│   │   ├── AlarmModeDict.cs       # 报警模式字典
│   │   ├── RecordLevelDict.cs     # 记录级别字典
│   │   └── BitDescriptionDict.cs  # 位描述字典（状态说明）
│   │
│   └── system/                    # 系统模型
│       └── SysCfg.cs              # 系统配置
│
├── Assets/                         # 资源文件
│   ├── Commlib/                   # 通信库
│   │   ├── S7Comm.cs              # S7 通信协议实现 ⭐
│   │   └── ProtocolLoader.cs      # 协议加载器
│   │
│   ├── Converter/                 # 值转换器
│   │   ├── DictIdToNameConverter.cs   # 字典ID转名称
│   │   ├── S7WordLengthConverter.cs   # S7字长转换
│   │   ├── CountToVisibilityConverter.cs # 数量转可见性
│   │   └── ...更多转换器
│   │
│   ├── Helper/                    # 帮助类
│   │   ├── DatabaseHelper.cs      # 数据库帮助类
│   │   ├── SQLHelper.cs           # SQL 帮助类
│   │   └── UiPathHelper.cs        # UI 路径帮助类
│   │
│   ├── Navigation/                # 导航服务
│   │   ├── INavigationService.cs  # 导航接口
│   │   ├── NavigationService.cs   # 导航实现
│   │   └── NavigationCache.cs     # 导航缓存
│   │
│   ├── Services/                  # 服务层
│   │   └── StationDataService.cs  # 站点数据服务 ⭐
│   │
│   └── Database/                  # 数据库相关
│       └── LoadDefaultData.cs     # 加载默认数据
│
├── UControl/                       # 自定义用户控件
│   ├── InputDialog.xaml           # 输入对话框
│   └── DoorControl.xaml           # 站台门控件 ⭐
│
├── Config/                         # 配置文件目录
│   ├── devices.json               # PLC 设备配置 ⭐
│   └── SystemConfig.json          # 系统配置
│
├── appsettings.json               # 应用程序配置
├── GlobalData.cs                  # 全局数据
├── Enumeration.cs                 # 枚举定义
└── DoorMonitorSystem.csproj       # 项目文件
```

---

## 🚀 快速开始

### 1. 克隆项目

```bash
git clone https://github.com/Lizhuoquan98/DoorMonitorSystem.git
cd DoorMonitorSystem
```

### 2. 安装依赖项目

本项目依赖以下本地项目：

- `Communication.Protocol` - 通信协议定义库
- `Communicationlib` - 通信底层库
- `ControlLibrary` - 自定义控件库

**目录结构**：
```
E:\VS2022\WPF\
├── DoorMonitorSystem\          # 当前项目
├── Communication.Protocol\     # 依赖项目
├── Communicationlib\           # 依赖项目
└── ControlLibrary\             # 依赖项目
```

### 3. 配置数据库

1. **安装 MySQL 数据库**（推荐 5.7 或更高版本）

2. **创建数据库**：
   ```sql
   CREATE DATABASE door_monitor
   CHARACTER SET utf8mb4
   COLLATE utf8mb4_unicode_ci;
   ```

3. **配置连接信息** - 编辑 `appsettings.json`：
   ```json
   {
     "Database": {
       "ServerAddress": "localhost",
       "DatabaseName": "door_monitor",
       "UserName": "root",
       "UserPassword": "your_password"
     }
   }
   ```

### 4. 配置 PLC 设备

编辑 `Config/devices.json`，配置站台门控制 PLC 的连接信息：

```json
{
  "Devices": [
    {
      "Id": 1,
      "StationName": "1号线-天安门站",
      "DeviceName": "站台门PLC-上行",
      "IP": "192.168.1.100",
      "Rack": 0,
      "Slot": 1,
      "PLCType": "S7-1200"
    },
    {
      "Id": 2,
      "StationName": "1号线-天安门站",
      "DeviceName": "站台门PLC-下行",
      "IP": "192.168.1.101",
      "Rack": 0,
      "Slot": 1,
      "PLCType": "S7-1200"
    }
  ]
}
```

### 5. 运行项目

**方式一：使用 Visual Studio 2022**

```bash
# 打开解决方案
DoorMonitorSystem.sln
```

**方式二：使用命令行**

```bash
dotnet restore
dotnet build
dotnet run
```

---

## 💻 系统要求

### 开发环境
- **操作系统**: Windows 7/8/10/11
- **开发工具**: Visual Studio 2022 或更高版本
- **.NET SDK**: .NET 8.0 或更高版本
- **数据库**: MySQL 5.7+ 或 MariaDB 10.3+

### 硬件要求
- **处理器**: 双核 2.0 GHz 或更高
- **内存**: 4 GB RAM（推荐 8 GB）
- **硬盘空间**: 1 GB 可用空间
- **网络**: 千兆以太网（用于 PLC 通信）
- **显示器**: 1920x1080 或更高分辨率（推荐双屏）

### 网络要求
- PLC 与监控电脑需在同一局域网内
- 建议使用独立的工控网络
- 支持网络隔离与安全策略

---

## ⚙️ 配置说明

### appsettings.json - 主配置文件

```json
{
  "Database": {
    "ServerAddress": "localhost",    // 数据库服务器地址
    "DatabaseName": "door_monitor",  // 数据库名称
    "UserName": "root",              // 数据库用户名
    "UserPassword": "your_password"  // 数据库密码
  }
}
```

### Config/devices.json - PLC 设备配置

```json
{
  "Devices": [
    {
      "Id": 1,                           // 设备ID
      "StationName": "1号线-XX站",       // 所属站点
      "DeviceName": "站台门PLC-上行",    // 设备名称
      "IP": "192.168.1.100",            // PLC IP地址
      "Rack": 0,                        // 机架号
      "Slot": 1,                        // 插槽号
      "PLCType": "S7-1200",             // PLC型号
      "ScanInterval": 100,              // 扫描间隔(ms)
      "Enabled": true                   // 是否启用
    }
  ]
}
```

### Config/SystemConfig.json - 系统配置

系统级参数配置，包括：
- 报警阈值设置
- 界面刷新频率
- 日志保存策略
- 用户权限配置

---

## 🎯 核心功能

### 1. 站点实时监控 ⭐

**功能描述**：
- 实时显示站台门的开关状态（开门、关门、故障）
- 支持上行/下行站台分别显示
- 可视化的站台门布局图
- 每扇门的详细状态信息

**监控内容**：
- 门开到位 / 门关到位
- 门锁状态
- 障碍物检测
- 应急解锁状态
- 电机故障
- 通信状态

### 2. PLC 实时通信 ⭐

**技术特性**：
- 基于 **Sharp7** 库实现 S7 协议
- 支持西门子 S7-200/300/400/1200/1500 系列
- 毫秒级数据采集（可配置 100ms-1000ms）
- 自动断线重连
- 数据缓冲与校验

**通信方式**：
```
监控系统 <--TCP/IP--> S7 PLC <--现场总线--> 站台门设备
```

### 3. 故障报警系统

**报警级别**：
- 🔴 **紧急报警** - 影响运营安全（门无法关闭、紧急按钮触发）
- 🟠 **重要报警** - 设备故障（电机故障、传感器故障）
- 🟡 **一般报警** - 轻微异常（通信延迟、门开关时间过长）
- 🔵 **提示信息** - 正常操作提示

**报警功能**：
- 声光报警
- 报警记录存储
- 报警确认机制
- 报警统计分析

### 4. 图形编辑器 ⭐

**功能**：
- 拖拽式布局编辑
- 自定义站台门位置
- 实时预览效果
- 导入/导出布局配置

**应用场景**：
- 配置新站点的监控界面
- 调整站台门显示布局
- 适配不同站台的门数量和位置

### 5. 数据记录与统计

**记录内容**：
- 站台门开关动作记录
- 故障事件记录
- 操作日志
- 设备运行时长统计

**统计分析**：
- 日/月/年开关门次数统计
- 故障率分析
- 设备可用性统计
- 维护周期提醒

### 6. 设备配置管理

**配置项**：
- **站点配置** - 地铁线路、站点信息
- **门组配置** - 站台门分组（上行/下行）
- **设备配置** - PLC 连接参数
- **位配置** - 每个数据位的含义和颜色

---

## 🚇 应用场景

### 场景一：地铁线路监控中心
**需求**：集中监控整条地铁线路所有站点的站台门状态

**解决方案**：
- 配置多个站点，每个站点对应一个地铁站
- 通过导航切换查看不同站点
- 统一的报警中心接收所有站点报警

### 场景二：单站监控室
**需求**：车站值班人员监控本站站台门

**解决方案**：
- 配置单个站点
- 大屏显示模式，实时监控
- 本地报警处理

### 场景三：维护管理
**需求**：维护人员查看设备历史数据，进行故障诊断

**解决方案**：
- 查询历史故障记录
- 分析设备运行数据
- 生成维护报告

### 场景四：新站部署
**需求**：新开通地铁站，快速配置监控系统

**解决方案**：
- 使用图形编辑器配置站台门布局
- 配置 PLC 连接参数
- 导入站台门点位配置
- 测试验证后投入使用

---

## 🏗️ 架构设计

### MVVM 模式

```
┌─────────────────────────────────────────┐
│              View (XAML)                │
│         用户界面 / 数据绑定              │
└────────────────┬────────────────────────┘
                 │ Data Binding
┌────────────────▼────────────────────────┐
│           ViewModel (C#)                │
│     业务逻辑 / 命令 / 属性通知           │
└────────────────┬────────────────────────┘
                 │ 调用
┌────────────────▼────────────────────────┐
│            Model (C#)                   │
│      数据模型 / 服务 / 数据访问          │
└─────────────────────────────────────────┘
```

### 通信架构

```
┌──────────────────────────────────────────────┐
│           WPF 监控界面                        │
└─────────────────┬────────────────────────────┘
                  │
┌─────────────────▼────────────────────────────┐
│         StationDataService                   │
│           站点数据服务                        │
└─────────────────┬────────────────────────────┘
                  │
┌─────────────────▼────────────────────────────┐
│            S7Comm 通信层                     │
│         (Sharp7 封装)                        │
└─────────────────┬────────────────────────────┘
                  │ TCP/IP (S7 Protocol)
┌─────────────────▼────────────────────────────┐
│          西门子 S7 PLC                       │
│       (S7-1200/1500)                        │
└─────────────────┬────────────────────────────┘
                  │ Profibus / Profinet
┌─────────────────▼────────────────────────────┐
│          站台门控制单元                       │
│         (现场设备)                           │
└──────────────────────────────────────────────┘
```

---

## 🔐 安全特性

- **用户认证** - 登录权限验证
- **操作审计** - 所有操作记录可追溯
- **权限分级** - 操作员/维护员/管理员
- **数据加密** - 敏感数据加密存储
- **网络隔离** - 工控网络与办公网络隔离

---

## 🤝 贡献指南

欢迎贡献！请遵循以下步骤：

1. **Fork** 本仓库
2. 创建特性分支 (`git checkout -b feature/NewFeature`)
3. 提交更改 (`git commit -m 'Add NewFeature'`)
4. 推送到分支 (`git push origin feature/NewFeature`)
5. 创建 **Pull Request**

### 代码规范
- 遵循 C# 编码规范
- 使用有意义的命名
- 添加必要的注释
- 确保编译无警告

---

## 📄 许可证

本项目采用 **MIT License** 开源协议。

---

## 📧 联系方式

- **作者**: Lizhuoquan
- **邮箱**: lizhuoquan98@gmail.com
- **GitHub**: [@Lizhuoquan98](https://github.com/Lizhuoquan98)

---

## 🙏 致谢

感谢以下开源项目：

- [Sharp7](https://github.com/fbarresi/Sharp7) - S7 PLC 通信库
- [Dapper](https://github.com/DapperLib/Dapper) - 高性能 ORM
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) - JSON 处理
- [Extended WPF Toolkit](https://github.com/xceedsoftware/wpftoolkit) - WPF 扩展控件

---

## 📊 项目统计

- **语言**: C#
- **框架**: WPF (.NET 8.0)
- **文件数**: 80+
- **代码行数**: 6600+
- **行业应用**: 轨道交通

---

## 🗺️ 开发路线图

- [x] 基础监控功能
- [x] S7 PLC 通信
- [x] 图形化界面
- [ ] 完善数据库表结构
- [x] 完善用户权限管理（四级权限体系）
- [ ] 支持更多 PLC 品牌（三菱、欧姆龙）
- [ ] 增加报表导出功能
- [ ] Web 版远程监控
- [ ] 移动端 APP
- [ ] AI 故障预测

---

## 📸 系统截图

> 待补充实际运行截图...

---

**⭐ 如果这个项目对你有帮助，请给个 Star！**

**🚇 专为轨道交通行业打造的站台门监控解决方案**
