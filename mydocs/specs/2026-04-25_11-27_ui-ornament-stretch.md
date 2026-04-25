# SDD Spec: 语音设置页装饰图纵向拉伸

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
None

## 1. Requirements (Context)
- **Goal**: 调整“语音设置”页面顶部与底部装饰图的纵向比例，让装饰图看起来不再被压扁，视觉上更接近原菜单风格。
- **In-Scope**:
  - 定位顶部与底部装饰图对应的 UI 构建代码。
  - 确认装饰图是容器尺寸问题、主题 sprite 问题，还是两者叠加。
  - 后续在不破坏现有布局的前提下，调整这两个装饰图的纵向展示比例。
- **Out-of-Scope**:
  - 不调整“停止词”“语音宏”内容区的排版。
  - 不修改按钮左右小装饰箭头。
  - 不替换主题资源或新增图片资源文件。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs`: 自定义“语音设置”窗口的完整 Unity UI 搭建与主题应用逻辑。顶部/底部装饰区域在 `CreateSection(window.transform, "TopOrnamentSection", 48f)` 和 `CreateSection(window.transform, "BottomOrnamentSection", 48f)` 创建；命名包含 `Section` 的面板最终在 `ApplyPanelTheme(...)` 中应用 `_theme.SectionSprite`。
  - `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs`: 从 `MenuResources` 与返回菜单 `MenuScreen` 中解析字体、sprite 和颜色；`SectionSprite` 来源于这里。
  - `HkVoiceMod/UI/VoiceSettingsTheme.cs`: 主题对象定义，承载 `SectionSprite`、`SectionTint`、`SectionSpriteIsSliced` 等展示参数。
- **Entry Points**:
  - `HkVoiceMod/Menu/VoiceSettingsMenuBuilder.cs`: 薄设置页入口按钮，点击后调用 `VoiceSettingsWindowController.Instance.ToggleFromMenu(...)` 打开自定义编辑窗口。
- **Data Models**:
  - 本任务无独立数据模型变更，主要涉及 Unity UI 视图构建参数。
- **Dependencies**:
  - `UnityEngine.UI`: `Image`、`LayoutElement`、`VerticalLayoutGroup`、`RectTransform` 等布局与渲染组件。
  - `Modding.Menu.MenuResources`: Hollow Knight 菜单公共资源入口，可能提供装饰 sprite 的原始素材。
  - `Satchel.BetterMenus`: 设置页入口菜单构建。

## 2. Architecture (Optional - Populated in INNOVATE)
- 最新观察结论：红框内的上下装饰并非单独静态资源文件，而是两个 section 容器复用菜单主题的 `SectionSprite`。
- 失败原因复盘：仅把 section 容器高度从 `48f` 提升到 `72f`，没有带来明显视觉变化，说明问题不在布局高度本身，而在装饰图 `Image` 的渲染方式。
- 高概率原因：`TopOrnamentSection` / `BottomOrnamentSection` 当前走通用 `ApplyPanelTheme(...)` 分支，使用 `_theme.SectionSprite + _theme.SectionSpriteIsSliced`。若该 sprite 被按 `Sliced` 方式渲染，增高容器只会改变切片区域或透明留白，中心装饰主体本身不会被明显纵向拉伸。
- 修正策略：对 `TopOrnamentSection` / `BottomOrnamentSection` 做单独渲染分支，不再完全复用普通 section 的通用渲染参数；需要显式控制该 image 的 `type` / `localScale.y`，让装饰主体产生可见的纵向拉伸。
- 已执行方案：保留装饰区高度 `72f`，并为上下装饰区单独强制 `Image.Type.Simple + localScale.y = 1.35f`，避免切片渲染吃掉纵向拉伸效果。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `internal sealed class VoiceSettingsWindowController : MonoBehaviour`
    - 新增常量：`private const float OrnamentSectionHeight = 72f;`
      - 作用：统一控制顶部与底部装饰区的纵向高度，替代当前硬编码的 `48f`。
    - 新增常量：`private const float OrnamentVerticalScale = 1.35f;`
      - 作用：对顶部与底部装饰图的 `Image.rectTransform.localScale.y` 做显式放大，确保视觉上出现明确的纵向拉伸。
    - 保持方法签名不变：`private GameObject CreateSection(Transform parent, string name, float preferredHeight, bool flexibleHeight = false)`
      - 本次不修改方法签名，不新增 helper，不改主题接口。
    - 保持方法签名不变：`private void ApplyImageTheme(Image image, Sprite? sprite, bool useSliced, Color tint)`
      - 但会补充默认复位逻辑：将 `image.rectTransform.localScale` 统一恢复为 `Vector3.one`，避免装饰图专属缩放影响其它图片。
    - 保持方法签名不变：`private void ApplyPanelTheme(Image image, string name, Color fallbackColor)`
      - 增加 `TopOrnamentSection` / `BottomOrnamentSection` 的专用分支。

### 3.2 File Changes
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - 在常量区新增 `OrnamentSectionHeight = 72f`。
  - 在常量区新增 `OrnamentVerticalScale = 1.35f`。
  - 将 `CreateSection(window.transform, "TopOrnamentSection", 48f);` 改为 `CreateSection(window.transform, "TopOrnamentSection", OrnamentSectionHeight);`。
  - 将 `CreateSection(window.transform, "BottomOrnamentSection", 48f);` 改为 `CreateSection(window.transform, "BottomOrnamentSection", OrnamentSectionHeight);`。
  - 在 `ApplyImageTheme(...)` 中增加 `image.rectTransform.localScale = Vector3.one;`，确保默认图片不残留专属拉伸状态。
  - 在 `ApplyPanelTheme(...)` 中为 `TopOrnamentSection` / `BottomOrnamentSection` 增加专用逻辑：
    - 继续使用 `_theme.SectionSprite` 和 `_theme.SectionTint`；
    - 强制 `useSliced = false`；
    - 再将 `image.rectTransform.localScale` 设为 `new Vector3(1f, OrnamentVerticalScale, 1f)`，让装饰主体出现明确纵向拉伸。
  - 不修改 `VoiceSettingsThemeResolver`、`VoiceSettingsTheme`，因为当前问题不是主题资源解析错误，而是 ornament section 需要独立渲染策略。

### 3.2 Implementation Checklist
- [x] 1. 在 `HkVoiceMod/UI/VoiceSettingsWindowController.cs` 常量区新增 `private const float OrnamentSectionHeight = 72f;`。
- [x] 2. 将顶部装饰区创建代码改为 `CreateSection(window.transform, "TopOrnamentSection", OrnamentSectionHeight);`。
- [x] 3. 将底部装饰区创建代码改为 `CreateSection(window.transform, "BottomOrnamentSection", OrnamentSectionHeight);`。
- [x] 4. 自检确认未改动按钮装饰、正文布局、主题 sprite 解析逻辑。
- [x] 5. 在 `HkVoiceMod/UI/VoiceSettingsWindowController.cs` 常量区新增 `private const float OrnamentVerticalScale = 1.35f;`。
- [x] 6. 在 `ApplyImageTheme(...)` 中增加默认 `localScale` 复位逻辑。
- [x] 7. 在 `ApplyPanelTheme(...)` 中为 `TopOrnamentSection` / `BottomOrnamentSection` 增加非切片渲染与纵向缩放分支。
- [x] 8. 自检确认仅上下装饰图使用专用纵向拉伸，普通 section、按钮 ornament、正文布局不受影响。
- [x] 9. 运行 `dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug` 验证编译结果。
- [x] 10. 重试部署到游戏 `Mods/HkVoiceMod` 目录；当前已成功部署到 `D:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Mods\HkVoiceMod`。

## 4. Follow-up Task: 不可点击按钮灰态

### 4.0 🚨 Open Questions
None

### 4.1 Requirements (Context)
- **Goal**: 对当前不可点击的按钮提供明确灰态展示，至少覆盖录制页面中“尚未开始录制时的停止录制按钮”这类禁用态按钮。
- **In-Scope**:
  - 检查录制页按钮的 `interactable` 切换逻辑。
  - 检查自定义按钮样式 `SettingsPageButtonStyle` 对禁用态的视觉处理。
  - 后续在不破坏现有 hover / selected ornament 表现的前提下，为禁用按钮增加灰色文字或灰色整体视觉。
- **Out-of-Scope**:
  - 不调整按钮文案。
  - 不修改按钮启用/禁用的业务条件。
  - 不修改非按钮控件（如输入框、列表项）的禁用态样式。

### 4.5 Code Map (Project Topology)
- **Core Logic**:
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:1405-1417`: 录制页根据当前录制状态切换 `_recordingStartButton`、`_recordingStopButton`、`_recordingClearButton` 的 `interactable`。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:2033-2068`: `CreateButton(...)` 创建按钮；当 `useSettingsPageStyle == true` 时挂载 `SettingsPageButtonStyle`。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:1666-1705`: `ApplyButtonTheme(...)` 给按钮设置 `ColorBlock` 和主题。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:2258-2365`: `SettingsPageButtonStyle` 是自定义视觉层，当前只处理 label 字体、ornament 显隐，不处理禁用态灰色文本。
- **Theme / Color**:
  - `HkVoiceMod/UI/VoiceSettingsTheme.cs:121-185`: `CreatePrimaryButtonColors()` / `CreateSecondaryButtonColors()` / `CreateDangerButtonColors()` 已经定义了 `disabledColor`，但当前自定义样式下禁用色没有明显传递到文本层。
- **Entry Points**:
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:421-423`: 录制弹窗按钮“开始录制 / 停止录制 / 清空”都使用 `useSettingsPageStyle = true`，因此受 `SettingsPageButtonStyle` 控制。

### 4.2 Research Findings / Architecture
- 当前“禁用态不明显”的根因不是缺少 `interactable = false`，而是视觉层没有把禁用态映射出来。
- 具体表现：
  - `Button.colors.disabledColor` 已存在；
  - 但 `SettingsPageButtonStyle.ApplyTheme(...)` 会把 `button.targetGraphic` 设为 `Color.clear`，并把 `LabelText.color` 固定成 `theme.TextColor`；
  - `RefreshVisualState()` 只控制左右 ornament 的显隐，不根据 `Button.interactable` 改变文字或其它颜色。
- 结果：即便按钮已禁用，用户看到的仍接近正常白色文字，只是无法点击、也没有 ornament 高亮，灰态不够明确。

### 4.3 Detailed Design & Implementation
- `File: HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `private sealed class SettingsPageButtonStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler`
    - 新增字段：`private VoiceSettingsTheme? _theme;`
      - 作用：缓存当前主题，供禁用态文字颜色和动态刷新复用。
    - 新增字段：`private bool _lastInteractable;`
      - 作用：记录上一次按钮可交互状态，用于检测 `interactable` 变化。
    - 新增方法：`private void Update()`
      - 返回类型：`void`
      - 作用：检测 `IsButtonInteractable()` 是否变化；若变化则刷新按钮视觉状态。
    - 新增方法：`private bool IsButtonInteractable()`
      - 返回类型：`bool`
      - 作用：统一判断按钮当前是否可交互，优先使用 `Button.IsInteractable()`，而不是仅依赖 `Button.interactable`，从而覆盖父级 `CanvasGroup` 等影响。
    - 保持方法签名不变：`public void ApplyTheme(VoiceSettingsTheme theme)`
      - 增加行为：缓存 `_theme`，并同步 `_lastInteractable = IsButtonInteractable()`。
    - 保持方法签名不变：`private void RefreshVisualState()`
      - 增加行为：
        - 若按钮可交互，则 `LabelText.color = _theme.TextColor`；
        - 若按钮不可交互，则 `LabelText.color = _theme.PlaceholderTextColor`，形成明确灰态；
        - 左右 ornament 的显示条件改为基于 `IsButtonInteractable()`，禁用时始终不显示。
- `File: HkVoiceMod/UI/VoiceSettingsTheme.cs`
  - 不修改接口与字段。
  - 原因：当前已有 `TextColor` / `PlaceholderTextColor`，足以表达正常态与灰态，无需新增主题字段。

### 4.3 Execution Notes
- 已执行方案：保持现有按钮业务逻辑不变，只在 `SettingsPageButtonStyle` 增加禁用态感知与视觉刷新。
- 已执行细节：
  - 缓存当前主题到 `_theme`；
  - 用 `_lastInteractable` 记录上一次真实可交互状态；
  - 新增 `Update()` 监听 `Button.IsInteractable()` 变化；
  - 在 `RefreshVisualState()` 中把不可点击按钮文字切到 `_theme.PlaceholderTextColor`；
  - 禁用态下不显示左右 ornament。
- 已验证交付：`dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug` 成功，且产物已部署到游戏 Mods 目录。

### 4.4 Implementation Checklist
- [x] 1. 在 `HkVoiceMod/UI/VoiceSettingsWindowController.cs` 的 `SettingsPageButtonStyle` 中新增 `_theme` 与 `_lastInteractable` 字段。
- [x] 2. 新增 `private bool IsButtonInteractable()`，统一读取按钮真实可交互状态。
- [x] 3. 新增 `private void Update()`，在 `interactable` 变化时触发视觉刷新。
- [x] 4. 更新 `ApplyTheme(VoiceSettingsTheme theme)`，缓存主题并初始化 `_lastInteractable`。
- [x] 5. 更新 `RefreshVisualState()`：可点击按钮保持正常文字色，不可点击按钮改为 `PlaceholderTextColor` 灰态。
- [x] 6. 更新 `RefreshVisualState()`：ornament 显示条件改为基于 `IsButtonInteractable()`。
- [x] 7. 自检确认未修改录制页按钮启用/禁用的业务条件，只修改视觉反馈。

## 5. Follow-up Task: 录制页按钮精简与录制中锁定

### 5.0 🚨 Open Questions
None

### 5.1 Requirements (Context)
- **Goal**:
  - 录制页移除“插入延迟”按钮。
  - 录制进行中，除了“停止录制”按钮外，其它录制页按钮都不可点击。
- **In-Scope**:
  - 调整录制弹窗 action row 的按钮组成。
  - 调整录制中的按钮 `interactable` 控制。
  - 保持录制停止后，原有可编辑/确认/取消流程继续可用。
- **Out-of-Scope**:
  - 不移除已有的 Delay 编辑能力本身；停止录制后仍可通过步骤行中的 Delay 项编辑延迟。
  - 不修改键盘快捷键（如 Backspace / Enter / Esc）的行为。
  - 不修改录制服务 `VoiceMacroCaptureService` 的采集逻辑。

### 5.5 Code Map (Project Topology)
- **Core Logic**:
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:418-426`
    - 录制弹窗 action row 当前包含：开始录制、停止录制、清空、插入延迟、确认、取消。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:596-612`
    - 录制步骤行的 `ValueButton` 已经在录制中自动禁用：`canEdit = !IsCapturing(...) && !IsCaptureSuspended(...) && string.IsNullOrEmpty(_editingPairId)`。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:1405-1417`
    - 当前只会动态切 `开始录制`、`停止录制`、`清空` 三个按钮的 `interactable`；`确认/取消` 尚未纳入录制态锁定。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:1205-1212`
    - `ConfirmRecordingFromButton()` / `CancelRecordingFromButton()` 当前始终可点击。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:697+` 与 `1248+`
    - Delay 编辑能力不仅来自 action row 的“插入延迟”按钮，也可通过点击步骤行中的 Delay 项进入 `OpenDelayModalForStep(...)` / `OpenDelayModal()` 相关流程。
- **Dependencies**:
  - `HkVoiceMod/Menu/VoiceMacroCaptureService.cs`
    - 键盘快捷键与录制状态管理在服务层；这次不改。

### 5.2 Research Findings / Architecture
- “插入延迟”按钮可以直接从录制弹窗 action row 移除，不会破坏停止录制后的 Delay 编辑能力，因为现有步骤行点击仍能打开 Delay 编辑流程。
- 当前 `OpenDelayModal()` 本身也不会再打开 Delay 弹窗，而是仅显示一条“事件流模式下不再支持手动追加尾部延迟”的状态提示，因此移除按钮不会减少现有可用功能。
- “录制时除了停止都不能点”当前已部分满足：
  - `开始录制`、`清空`：录制中已禁用；
  - 录制步骤行：录制中已禁用；
  - `停止录制`：录制中可点击；
  - `确认`、`取消`：录制中当前仍可点击，需要补禁用。
- 因为这次只是按钮编排与交互锁定，最小改动范围仍然集中在 `VoiceSettingsWindowController.cs` 单文件内。

### 5.3 Detailed Design & Implementation
- `File: HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `internal sealed class VoiceSettingsWindowController : MonoBehaviour`
    - 新增字段：
      - `private Button? _recordingConfirmButton;`
      - `private Button? _recordingCancelButton;`
    - 作用：把录制弹窗中的“确认 / 取消”按钮纳入 `RefreshDynamicContent()` 的动态 `interactable` 控制。
  - 保持方法签名不变：`private GameObject BuildRecordingModal(Transform parent)`
    - 删除 action row 中的 `RecordingDelay` 按钮创建。
    - 将 `RecordingConfirm` / `RecordingCancel` 的 `Button` 引用保存到新增字段。
  - 删除方法：`private void OpenDelayModal()`
    - 原因：移除唯一入口后，该方法不再被引用，且其当前行为仅为显示一条已不需要的状态提示。
  - 保持方法签名不变：`private void RefreshDynamicContent()`
    - 新增行为：
      - `RecordingConfirm` 在 `isRecording == true` 时禁用；
      - `RecordingCancel` 在 `isRecording == true` 时禁用；
      - 停止录制按钮的现有启用条件保持不变。

### 5.3 Execution Notes
- 已执行方案：
  - 从录制弹窗 action row 中移除 `插入延迟` 按钮；
  - 为 `确认 / 取消` 增加显式 `Button` 引用；
  - 在 `RefreshDynamicContent()` 中把 `确认 / 取消` 纳入录制态 `interactable` 控制。
- 已执行结果：
  - 录制中只有 `停止录制` 保持可点击；
  - `开始录制`、`清空`、`确认`、`取消` 在录制中全部禁用；
  - 停止录制后，步骤行编辑与 Delay 编辑能力仍通过点击步骤行保留。
- 已验证交付：`dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug` 成功，且产物已部署到游戏 Mods 目录。

### 5.4 Implementation Checklist
- [x] 1. 在 `HkVoiceMod/UI/VoiceSettingsWindowController.cs` 中新增 `_recordingConfirmButton` 与 `_recordingCancelButton` 字段。
- [x] 2. 在 `BuildRecordingModal(...)` 中移除 `RecordingDelay` 按钮。
- [x] 3. 在 `BuildRecordingModal(...)` 中保存 `RecordingConfirm` / `RecordingCancel` 的按钮引用。
- [x] 4. 删除不再使用的 `OpenDelayModal()` 方法。
- [x] 5. 在 `RefreshDynamicContent()` 中补充 `RecordingConfirm` / `RecordingCancel` 的 `interactable` 控制，使录制中仅 `RecordingStop` 可点击。
- [x] 6. 自检确认录制步骤行编辑能力仍保留在“停止录制后点击步骤行”的路径上。
- [x] 7. 运行 `dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug`，确认编译与部署成功。

## 6. Follow-up Bug: 二次打开编辑器后上下装饰图消失

### 6.0 🚨 Open Questions
None

### 6.1 Requirements (Context)
- **Goal**: 修复“打开编辑器 -> 返回 -> 再打开编辑器”后，顶部和底部装饰图案不显示的问题。
- **In-Scope**:
  - 检查编辑器每次打开时的主题解析与重应用链路。
  - 确认上下装饰图丢失是否由 sprite 丢失、image 被禁用、还是节点状态异常导致。
  - 后续以最小改动修复 reopen 后 ornament 稳定显示。
- **Out-of-Scope**:
  - 不重做整套主题解析策略。
  - 不修改按钮 ornament 或其它非顶部/底部装饰图的视觉方案。
  - 不改动返回/关闭编辑器的业务流程。

### 6.5 Code Map (Project Topology)
- **Core Logic**:
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:114-150`
    - `Show(...)` 每次打开编辑器都会重新执行 `ResolveTheme(returnScreen)`，随后 `ApplyThemeToExistingTree()` 重刷现有 UI 树。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:1562-1566`
    - `ResolveTheme(...)` 直接用当前 `returnScreen` 重新生成 `_theme`。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:1569-1634`
    - `ApplyThemeToExistingTree()` 会遍历现有 `Image` 并调用 `ApplyPanelTheme(...)`。
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs:1768-1773`
    - `TopOrnamentSection` / `BottomOrnamentSection` 专用分支直接依赖 `_theme.SectionSprite`。
  - `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs:22-34`
    - 主题解析优先尝试公共资源，再尝试从当前 `returnScreen` 的已加载 UI 对象中采样 sprite；若采样失败，会退回一个 sprite 可能为 `null` 的 theme。
  - `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs:85-135`
    - `TryResolveFromLoadedMenuObjects(...)` 是 `SectionSprite` 的主要来源。
- **Data Structures**:
  - `HkVoiceMod/UI/VoiceSettingsTheme.cs`
    - `VoiceSettingsTheme` 是不可变对象，`SectionSprite` / `SectionSpriteIsSliced` 都在构造时固定。

### 6.2 Research Findings / Architecture
- 上一轮修复结果：仅在 `resolvedTheme.SectionSprite == null` 时沿用旧 sprite，用户实测“没有修复”，说明问题不只是在二次打开时拿到 `null`。
- 更新后的高概率根因：ornament 在二次打开时并非单纯“sprite 变成 null”，而是 `ResolveTheme(...)` 可能解析到了“错误但非空”的 `SectionSprite`，随后 `ApplyThemeToExistingTree()` 把原本正确的 ornament sprite 覆盖掉了。
- 证据链：
  - `Show(...)` 每次打开都会重新跑 `ResolveTheme(returnScreen)`；
  - `TopOrnamentSection` / `BottomOrnamentSection` 的渲染完全依赖 `_theme.SectionSprite`；
  - `VoiceSettingsThemeResolver.Resolve(...)` 的采样依赖当前 `returnScreen` 树；它不仅可能返回 `null`，也可能返回不适合 ornament 的普通 panel sprite；
  - `ApplyThemeToExistingTree()` 在 reopen 时会再次遍历现有 ornament image 并重刷，从而把第一次打开时正确显示过的 sprite 覆盖掉；
  - 这种退化会优先影响上下 ornament，因为这两个区域最依赖 `SectionSprite`，而其它区域很多本来就是透明或纯色面板，因此问题最容易只体现在上下装饰图上。
- 最小修复方向：
  - 不再只依赖 `_theme.SectionSprite` 的当次解析结果；
  - 为上下 ornament 单独缓存“第一次成功显示过的 sprite”，后续 reopen 时优先使用该 sticky sprite；
  - 这样即便 `ResolveTheme(...)` 返回了错误但非空的 `SectionSprite`，也不会覆盖已经验证可用的 ornament 图案。

### 6.3 Detailed Design & Implementation
- `File: HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `internal sealed class VoiceSettingsWindowController : MonoBehaviour`
    - 新增字段：`private Sprite? _ornamentSectionSprite;`
      - 作用：缓存上下 ornament 第一次成功显示过的 sprite，作为后续 reopen 的稳定来源。
  - 保持方法签名不变：`private void ApplyPanelTheme(Image image, string name, Color fallbackColor)`
    - 在 `TopOrnamentSection` / `BottomOrnamentSection` 分支中：
      - 优先取 `_ornamentSectionSprite`；
      - 若其为空，则回退到 `image.sprite`（已有树上的旧正确 sprite）；
      - 再回退到 `_theme.SectionSprite`；
      - 一旦得到非空 sprite，就回写 `_ornamentSectionSprite`，形成 sticky cache。
    - 保持现有 `Image.Type.Simple + localScale.y = OrnamentVerticalScale` 不变。
  - `ResolveTheme(...)` 当前的 null-only 回退逻辑可保留，但本次主修复不再依赖它单独生效。
  - 不需要新增或修改 `VoiceSettingsTheme` 接口来解决 reopen 覆盖问题。

### 6.3 Execution Notes
- 已执行方案：
  - 在 `VoiceSettingsWindowController` 中新增 `_ornamentSectionSprite`；
  - 将 ornament 分支改为优先使用 `_ornamentSectionSprite`，其次使用 `image.sprite`，最后回退到 `_theme.SectionSprite`；
  - 一旦拿到非空 ornament sprite，立即回写到 `_ornamentSectionSprite`，避免后续 reopen 被错误主题覆盖。
- 已执行结果：
  - ornament 是否显示不再只依赖本次 `ResolveTheme(...)` 的瞬时采样结果；
  - reopen 时即便新 theme 解析到错误但非空的 `SectionSprite`，也不会覆盖第一次成功显示过的 ornament 图案；
  - 原有 `Image.Type.Simple + OrnamentVerticalScale` 渲染方式保持不变。
- 已验证交付：`dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug` 成功，且产物已部署到游戏 Mods 目录。

### 6.4 Implementation Checklist
- [x] 1. 在 `HkVoiceMod/UI/VoiceSettingsWindowController.cs` 中新增 `_ornamentSectionSprite` 字段。
- [x] 2. 调整 `ApplyPanelTheme(...)` 的 ornament 分支，使其优先使用 sticky ornament sprite。
- [x] 3. 自检确认 reopen 时即便新 theme 解析到错误 sprite，也不会覆盖第一次成功显示过的 ornament 图案。
- [x] 4. 运行 `dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug`，确认编译与部署成功。
