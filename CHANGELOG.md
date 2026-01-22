# 更新日志 (Changelog)

本文档记录了站台门监控系统的所有重要更改。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

---

## [未发布] - Unreleased

### 📝 计划中
- [ ] 完善数据库表结构和初始化脚本
- [ ] 添加用户认证和权限管理系统
- [ ] 支持更多 PLC 品牌（三菱、欧姆龙）
- [ ] 增加 Excel/PDF 报表导出功能
- [ ] 实现 Web 版远程监控
- [ ] 添加单元测试和集成测试
- [ ] 国际化支持（中英文切换）
- [ ] AI 故障预测功能

---

## [1.0.0] - 2024-01-XX

### ✨ 新增功能

#### 核心功能
- ✅ **站点实时监控** - 支持多站点、多站台的实时监控
- ✅ **PLC 通信** - 基于 Sharp7 实现西门子 S7 协议通信
- ✅ **站台门状态显示** - 实时显示每扇门的开关、锁定、故障状态
- ✅ **图形编辑器** - 可视化编辑站台门布局和监控界面
- ✅ **设备变量管理** - 管理和配置 PLC 数据点位
- ✅ **配置管理** - 灵活的系统配置和设备配置

#### 数据管理
- ✅ **MySQL 数据库集成** - 使用 Dapper ORM 进行数据访问
- ✅ **配置文件支持** - JSON 格式的设备和系统配置
- ✅ **数据持久化** - 配置数据和运行数据的存储

#### 用户界面
- ✅ **MVVM 架构** - 清晰的代码结构，视图与逻辑分离
- ✅ **导航服务** - 灵活的页面导航和视图管理
- ✅ **自定义控件** - 站台门控件、输入对话框等
- ✅ **值转换器** - 多种数据绑定转换器

#### 报警功能
- ✅ **多级报警** - 支持紧急、重要、一般、提示四个级别
- ✅ **报警字典** - 可配置的报警模式和描述
- ✅ **记录级别** - 可配置的数据记录策略

### 🛠️ 技术实现

#### 框架和库
- ✅ **.NET 8.0** - 采用最新的 .NET 平台
- ✅ **WPF** - Windows Presentation Foundation 桌面应用
- ✅ **Sharp7 1.1.84** - S7 PLC 通信库
- ✅ **MySql.Data 9.3.0** - MySQL 数据库驱动
- ✅ **Dapper 2.1.66** - 轻量级 ORM 框架
- ✅ **Newtonsoft.Json 13.0.3** - JSON 序列化
- ✅ **Extended.Wpf.Toolkit** - WPF 扩展控件
- ✅ **Microsoft.Xaml.Behaviors.Wpf** - XAML 行为库

#### 架构设计
- ✅ **MVVM 模式** - Model-View-ViewModel 架构
- ✅ **依赖注入** - 服务定位和依赖注入
- ✅ **导航服务** - 自定义导航框架
- ✅ **数据绑定** - 丰富的值转换器

### 📁 项目结构

```
DoorMonitorSystem/
├── Views/              # 视图层
├── ViewModels/         # 视图模型层
├── Models/             # 数据模型层
│   ├── ConfigEntity/   # 配置实体
│   ├── RunModels/      # 运行时模型
│   ├── Points/         # 点位模型
│   └── system/         # 系统模型
├── Assets/             # 资源文件
│   ├── Commlib/        # 通信库
│   ├── Converter/      # 值转换器
│   ├── Helper/         # 帮助类
│   ├── Navigation/     # 导航服务
│   ├── Services/       # 服务层
│   └── Database/       # 数据库
├── UControl/           # 自定义控件
├── Config/             # 配置文件
└── Base/               # 基础类库
```

### 🎯 主要模块

#### 1. 站点监控模块
- **StationViewModel** - 站点监控业务逻辑
- **StationMainGroup** - 站点主组模型
- **StationDataService** - 站点数据服务
- **StationEntity** - 站点配置实体

#### 2. 站台门管理模块
- **DoorModel** - 站台门运行模型
- **DoorEntity** - 站台门配置实体
- **DoorGroup** - 站台门分组
- **DoorTypeEntity** - 站台门类型（全高/半高）
- **DoorBitConfig** - 站台门位配置
- **DoorVisualResult** - 站台门可视化

#### 3. PLC 通信模块
- **S7Comm** - S7 协议通信实现
- **ProtocolLoader** - 协议加载器
- **DevicePoint** - 设备点位模型

#### 4. 监控面板模块
- **PanelModel** - 监控面板模型
- **PanelEntity** - 面板配置实体
- **PanelGroup** - 面板分组
- **PanelBitConfig** - 面板位配置

#### 5. 图形编辑模块
- **GraphicEditingViewModel** - 图形编辑业务逻辑
- **GraphicEditingView** - 图形编辑界面

#### 6. 配置管理模块
- **ConfigureViewModel** - 配置管理业务逻辑
- **BitColorEntity** - 位颜色配置
- **SysCfg** - 系统配置

### 📋 配置文件

#### appsettings.json
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

#### Config/devices.json
- PLC 设备配置
- IP 地址、机架号、插槽号
- 设备名称和类型

#### Config/SystemConfig.json
- 系统级参数配置
- 报警阈值
- 刷新频率

### 🔧 开发工具

- **Visual Studio 2022** - 主要开发环境
- **Git** - 版本控制
- **GitHub** - 代码托管
- **MySQL** - 数据库

### 📝 文档

- ✅ **README.md** - 项目说明文档
- ✅ **LICENSE** - MIT 开源协议
- ✅ **CHANGELOG.md** - 更新日志（本文档）
- ✅ **.gitignore** - Git 忽略文件配置

### 🐛 已知问题

暂无

### 🔒 安全性

- 数据库密码配置在 appsettings.json 中
- 建议使用环境变量或加密配置管理敏感信息
- 工控网络建议与办公网络隔离

---

## 版本说明

### 版本号格式

版本号格式为：`主版本号.次版本号.修订号`

- **主版本号**：不兼容的 API 修改
- **次版本号**：向下兼容的功能性新增
- **修订号**：向下兼容的问题修正

### 更新类型

- ✨ **新增 (Added)**: 新增功能
- 🔄 **变更 (Changed)**: 功能变更
- 🗑️ **弃用 (Deprecated)**: 即将移除的功能
- ❌ **移除 (Removed)**: 已移除的功能
- 🐛 **修复 (Fixed)**: Bug 修复
- 🔒 **安全 (Security)**: 安全性修复

---

## 贡献者

- [@Lizhuoquan98](https://github.com/Lizhuoquan98) - 项目创建者和主要维护者

---

## 反馈与支持

如有问题或建议，请通过以下方式联系：

- **GitHub Issues**: https://github.com/Lizhuoquan98/DoorMonitorSystem/issues
- **Email**: lizhuoquan98@gmail.com

---

**感谢使用站台门监控系统！** 🚇
