# 绑定路径及方法说明文档

本文档详细说明了系统中点位生成的绑定路径 (`UiBinding`) 规则，以及 UI 界面如何与底层数据进行绑定的原理。

## 1. 绑定路径 (UiBinding) 生成规则

在“批量生成”功能中，系统会自动为每个点位生成一个唯一的 `UiBinding` 字符串。这个字符串主要用于日志记录、调试以及作为点位的唯一可读标识。

生成逻辑遵循以下模式：**`{对象名称}_{模板描述}`**

### 1.1 站台门 (Door)
*   **格式**: `${DoorName}_${Description}`
*   **变量来源**:
    *   `DoorName`: 门定义中的名称 (例如 "Door1", "DP01")
    *   `Description`: `DoorBitConfig` 表中的描述字段 (例如 "OpenState", "Fault")
*   **示例**:
    *   `Door1_OpenState` (一号门的开门到位信号)
    *   `Door1_CloseState` (一号门的关门到位信号)
    *   `DP02_Isolate` (二号门的隔离信号)

### 1.2 监控面板 (Panel)
*   **格式**: `${PanelName}_{Description}`
*   **变量来源**:
    *   `PanelName`: 面板定义中的名称 (例如 "Panel1", "PSC_Cabinet")
    *   `Description`: `PanelBitConfig` 表中的描述字段 (例如 "PowerStatus", "FanFault")
*   **示例**:
    *   `Panel1_PowerAC` (一号面板的交流电源状态)
    *   `PSC_Cabinet_DoorOpen` (PSC机柜的柜门打开报警)

---

## 2. UI 数据绑定原理 (核心机制)

**注意**：本系统的核心业务UI（如监控大屏、站台图）**并不直接使用** `UiBinding` 字符串作为绑定源。数据驱动是基于 **对象模型 (Object Model)** 的。

### 2.1 数据流向
1.  **PLC 通讯层**：采集到底层位数据 (Bit Value)。
2.  **映射层**: 识别出该点位属于哪个对象 (`TargetObjId`) 和哪种功能 (`TargetBitConfigId`)。
3.  **数据中心 (DataManager)**: 将变化推送到对应的 ViewModel (如 `DoorViewModel`)。
4.  **UI 层 (View)**: XAML 控件直接绑定到 ViewModel 的属性。

### 2.2 如何开发新界面 (Binding Method)
如果您需要添加一个新的状态指示灯，请遵循以下步骤：

1.  **找到对应的 ViewModel**: 
    *   门状态: `DoorViewModel`
    *   面板状态: `PanelViewModel` (如有)
2.  **设置 Binding**:
    在 XAML 中，直接绑定到翻译后的**布尔属性**或**状态属性**，而不是原始的 `UiBinding` 字符串。

    *错误做法 (系统不支持):*
    ```xml
    <!-- ❌ 系统没有这种基于 Key 的全局字典 -->
    <Indicator State="{Binding GlobalValues['Door1_OpenState']}"/>
    ```

    *正确做法 (推荐):*
    ```xml
    <!-- ✅ 绑定到 ViewModel 的属性 -->
    <Indicator State="{Binding CurrentDoor.IsOpen}"/>
    ```

---

## 3. 批量生成页面的绑定说明 (DevvarlistView)

如果您是在维护或修改“设备变量列表”页面本身，以下是该页面的关键绑定关系：

### 3.1 视图模型
*   **ViewModel**: `DoorMonitorSystem.ViewModels.DevvarlistViewModel`
*   **View**: `DoorMonitorSystem.Views.DevvarlistView`

### 3.2 批量生成弹窗绑定
该弹窗内的控件直接绑定到 ViewModel 的简单属性上：

| UI 控件 | 绑定属性 (Path) | 数据类型 | 说明 |
| :--- | :--- | :--- | :--- |
| **切换按钮** | `IsBatchPopupOpen` | `bool` | 控制弹窗的显示/隐藏 |
| **生成对象 (单选)** | `BatchTargetType` | `int` | `0`=站台门, `1`=监控面板 (需配合 `IntToBoolConverter`) |
| **起始地址 (文本)** | `BatchStartAddress` | `string` | 手动输入的DB号或寄存器地址 |
| **对象步长 (文本)** | `BatchDoorStride` | `int` | 每个对象占用的字节偏移量 |
| **执行按钮** | `BatchGenerateCommand` | `ICommand` | 触发 `BatchGenerate` 方法 |

### 3.3 转换器 (Converters)
页面资源中定义了以下转换器用于逻辑处理：

*   **`IntToBoolConverter`** (`Key="IntToBool"`): 
    *   用于将 `BatchTargetType` (int) 映射到 RadioButton 的 `IsChecked` (bool)。
    *   参数: `ConverterParameter=0` 或 `1`。
*   **`DataTypeToVisibilityConverter`** (`Key="DataTypeToVis"`):
    *   用于根据数据类型控制某些高级列的显示/隐藏。
