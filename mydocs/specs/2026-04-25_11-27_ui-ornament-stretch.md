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
- `HkVoiceMod/Menu/VoiceSettingsMenuBuilder.cs`: 薄设置页入口按钮，点击后调用 `VoiceSettingsWindowController.Instance.OpenFromMenu(...)` 打开自定义编辑窗口。
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

## 7. Follow-up Task: 入口按钮改为只打开，修复首开缩小闪现

### 7.0 🚨 Open Questions
None

### 7.1 Requirements (Context)
- **Goal**:
  - 薄设置页入口按钮的文案从“打开 / 关闭 自定义编辑器”调整为仅表达“打开自定义编辑器”。
  - 修复首次打开自定义编辑器时，先短暂显示一个缩小版窗口、随后才恢复正常尺寸的问题。
- **In-Scope**:
  - 调整薄设置页入口按钮文本与说明文案。
  - 调整该按钮的调用行为，使其不再承担“关闭”语义。
  - 检查自定义编辑器首次建树与首次显示顺序，避免布局未稳定时先被渲染出来。
- **Out-of-Scope**:
  - 不修改编辑器关闭路径；关闭仍然通过编辑器内部的“返回 / 放弃更改 / Esc”等现有路径处理。
  - 不重做整套 UI 构建方式。

### 7.2 Code Map (Project Topology)
- `HkVoiceMod/Menu/VoiceSettingsMenuBuilder.cs`
  - 薄设置页入口按钮文案与点击行为定义位置。
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `Show(...)`: 首次打开时的主题解析、建树、重建内容、最终显示顺序。
  - `EnsureBuilt(...)`: 首次创建 overlay canvas 与 window 树的位置。
  - `SetVisible(...)`: 整个 overlay 的显隐入口。

### 7.3 Research Findings / Architecture
- 当前入口按钮虽然用户只能“打开”它，但文案和调用方法仍然保留了 `Toggle` 语义，和真实交互不一致。
- “首次打开先闪一个缩小版窗口”只发生在首开，高概率不是关闭/重开状态残留，而是**第一次建树完成后，窗口在最终布局稳定前就已经进入渲染**。
- 最小修复方向：
  - 薄设置页入口改为只调用 `OpenFromMenu(...)`，如果窗口已可见则直接忽略，不再走关闭分支。
  - 首次建树时先让 overlay 保持隐藏；`RebuildFromDraft()` 完成后，先尝试同帧强制刷新 canvas 与 window 布局，再执行 `SetVisible(true)`。
- 用户复测结论：仅做“同帧 `Canvas.ForceUpdateCanvases()` + `LayoutRebuilder.ForceRebuildLayoutImmediate(...)`”仍不足以消除首开缩小闪现，说明窗口尺寸还会在后续帧继续稳定。
- 修正后的更稳妥方案：
  - 保留首次建树时 overlay 隐藏；
  - `Show(...)` 完成内容准备后不立即显示，而是启动一次短生命周期 reveal coroutine；
  - 在 2 个布局稳定 pass（`yield return null` + `WaitForEndOfFrame`）后，再做最终布局刷新并 `SetVisible(true)`。

### 7.4 Detailed Design & Implementation
- `File: HkVoiceMod/Menu/VoiceSettingsMenuBuilder.cs`
  - 将入口按钮标题改为：`打开自定义编辑器`
  - 将按钮说明从“显式切换”改为“打开”语义。
  - 点击行为从 `VoiceSettingsWindowController.Instance.ToggleFromMenu(...)` 改为 `VoiceSettingsWindowController.Instance.OpenFromMenu(...)`。
- `File: HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - 将 `ToggleFromMenu(...)` 改为 `OpenFromMenu(...)`：
    - 若窗口已显示，则直接返回，不再关闭窗口；
    - 否则继续调用 `Show(...)`。
  - 在 `EnsureBuilt(...)` 中，`CanvasGroup` 创建后立即把 overlay 保持为隐藏状态，避免首次建树过程中被提前渲染。
  - 在 `Show(...)` 中，`RebuildFromDraft()` 后不直接 `SetVisible(true)`，而是改为启动 reveal coroutine。
  - 新增字段：`private Coroutine? _pendingRevealCoroutine;`
    - 用于标记当前是否存在尚未完成的首开显示流程，避免重复打开或关闭时残留 reveal。
  - 新增 helper：统一执行主窗口与宏列表的强制布局刷新。
  - 新增 coroutine：连续等待 2 次布局稳定 pass 后，再做最终布局刷新并执行 `SetVisible(true)` 与 `FocusInputField(...)`。
  - 在 `CloseWindow()` / 重新打开前，若存在未完成 reveal coroutine，先停止并清理。

### 7.5 Execution Notes
- 已执行方案：
  - 薄设置页入口按钮标题改为 `打开自定义编辑器`，说明文案改为纯打开语义；
  - 菜单入口调用由 `OpenFromMenu(...)` 接管，窗口已显示时直接忽略，不再反向关闭；
  - 首次建树时在 `CanvasGroup` 创建后立即保持 overlay 隐藏；
  - `Show(...)` 在 `RebuildFromDraft()` 后、`SetVisible(true)` 前先尝试同帧强制执行 canvas/layout 刷新。
- 已执行结果：
  - 薄设置页入口文案与真实交互一致，不再暗示“关闭”能力；
  - 首次打开时不再把未稳定布局的窗口直接暴露给玩家，缩小版闪现概率降低；
  - 关闭路径仍保持在编辑器内部的 `返回 / 放弃更改 / Esc` 逻辑中。
- 已验证交付：`dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug` 成功，且产物已部署到游戏 Mods 目录。
- 用户复测反馈：首次打开仍会闪一下，说明同帧强刷布局还不够，本节继续升级为“延迟 reveal 到布局稳定帧”的方案。
- 二次执行结果：
  - `VoiceSettingsWindowController` 已新增 `_pendingRevealCoroutine`；
  - `Show(...)` 现在在内容准备完成后启动 reveal coroutine，而不是同帧直接显示；
  - coroutine 会做 2 次布局稳定 pass，再执行最终布局刷新与 `SetVisible(true)`；
  - `OpenFromMenu(...)` 在 reveal 未完成时会忽略重复打开；
  - `CloseWindow()` / `OnDestroy()` 会清理未完成的 reveal coroutine，避免残留显示。
- 已再次验证交付：`dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug` 成功，且产物已部署到游戏 Mods 目录。

### 7.6 Implementation Checklist
- [x] 1. 修改薄设置页入口按钮标题与说明文案，移除“打开 / 关闭 / 显式切换”语义。
- [x] 2. 将菜单入口调用从 `ToggleFromMenu(...)` 改为 `OpenFromMenu(...)`。
- [x] 3. 调整 `VoiceSettingsWindowController` 的入口方法，使其只负责打开，不再负责关闭。
- [x] 4. 在首次建树阶段保持 overlay 隐藏，避免未完成布局的窗口被提前渲染。
- [x] 5. 在 `Show(...)` 最终显示前强制刷新布局，降低首开缩小闪现概率。
- [x] 6. 运行 `dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug`，确认编译与部署成功。
- [x] 7. 新增延迟 reveal coroutine，在布局稳定后再显示窗口。
- [x] 8. 在重新打开或关闭窗口时清理未完成的 reveal coroutine。
- [x] 9. 再次运行 `dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug`，确认编译与部署成功。

## 8. Follow-up Task: 自定义按钮左右 ornament 对齐原生悬浮动画

### 8.0 🚨 Open Questions
None

### 8.1 Requirements (Context)
- **Goal**: 让自定义编辑器里按钮左右两侧的 ornament，在鼠标悬浮/选中时使用和游戏原生菜单一致的出现/隐藏动画，而不是当前的瞬时出现。
- **In-Scope**:
  - 检查当前自定义按钮 ornament 的显示逻辑。
  - 对齐原生菜单按钮 cursor 的 show/hide 驱动方式。
  - 在不破坏当前禁用态灰色文本、选中态 ornament 逻辑的前提下，给自定义按钮接入同类动画。
- **Out-of-Scope**:
  - 不重做按钮文本、底图或闪光效果。
  - 不修改非按钮 ornament（如页面顶部/底部 ornament section）。

### 8.2 Code Map (Project Topology)
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `CreateSettingsPageButtonOrnament(...)`: 当前自定义 ornament 节点创建位置。
  - `SettingsPageButtonStyle.RefreshVisualState()`: 当前 ornament 直接 `enabled = true/false` 的逻辑位置。
  - `SettingsPageButtonOrnament`: 当前为空 marker component，可扩展成 ornament 动画控制入口。
- `hkapi/UnityEngine/UI/MenuSelectable.cs`
  - 原生菜单按钮在 `OnSelect(...)` / `ValidateDeselect()` 中对左右 cursor 的 `Animator` 发送 `show/hide` trigger。
- `hkapi/Modding/Menu/MenuResources.cs`
  - `MenuResources.MenuCursorAnimator` 提供原生菜单 cursor 使用的 animator controller，名称为 `Menu Fleur`。
- `hkapi/Modding/Menu/MenuButtonContent.cs`
  - Satchel/Modding 菜单按钮创建左右 cursor 时，直接挂 `Animator + Image`，并复用 `MenuResources.MenuCursorAnimator`。

### 8.3 Research Findings / Architecture
- 当前自定义按钮 ornament 只是 `Image.enabled = showOrnaments`，因此只有瞬时出现/隐藏，没有动画。
- 原生菜单的左右 cursor 不是手写 Lerp，而是挂了 `Animator`，并通过 `show/hide` trigger 驱动。
- 最小且最贴近原生的方案不是重新猜一套动画曲线，而是直接复用 `MenuResources.MenuCursorAnimator`。
- 兼容性考虑：如果某些运行时拿不到 `MenuCursorAnimator`，则保留当前静态 ornament 作为 fallback，避免按钮完全失去 ornament。

### 8.4 Detailed Design & Implementation
- `File: HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `CreateSettingsPageButtonOrnament(...)`
    - 创建 ornament 时尝试挂载 `Animator`，并复用 `MenuResources.MenuCursorAnimator`。
    - 若 animator 可用，则 ornament 进入“原生 cursor 模式”；否则保留当前静态 image 模式。
  - `SettingsPageButtonOrnament`
    - 从空 marker component 扩展为 ornament 控制器。
    - 负责：
      - 初始化 `Image/Animator`；
      - 判断当前是否使用原生 animator；
      - 对外暴露 `Show()` / `Hide()`，内部改为发送 `show/hide` trigger；
      - 在无 animator 时回退到 `Image.enabled` 直显逻辑。
  - `SettingsPageButtonStyle`
    - `LeftArrow` / `RightArrow` 改为通过 `SettingsPageButtonOrnament` 控制显隐，而不是直接切 `Image.enabled`。
    - 保留现有 `showOrnaments = isInteractable && (_isHovered || _isSelected)` 判定，只替换底层显示方式。
    - 增加 ornament 可见状态缓存，避免每次 `RefreshVisualState()` 都重复触发 `show/hide`。
- `File: HkVoiceMod/HkVoiceMod.csproj`
  - 新增 `UnityEngine.AnimationModule` 引用。
  - 原因：`Animator` 类型位于该模块，若不显式引用则当前工程无法编译通过。
- 不修改按钮业务逻辑；hover/selected/interactable 的判定链路保持不变。

### 8.5 Execution Notes
- 已执行方案：
  - 将 `SettingsPageButtonOrnament` 从空 marker component 扩展为 ornament 控制器；
  - ornament 优先尝试接入 `MenuResources.MenuCursorAnimator`，并通过 `show/hide` trigger 驱动；
  - 当运行时拿不到 `MenuCursorAnimator` 时，自动回退到原有静态 image ornament；
  - `SettingsPageButtonStyle` 改为通过 ornament controller 控制显隐，并增加 `_ornamentsVisible` 缓存，避免重复打 trigger；
  - `HkVoiceMod.csproj` 已新增 `UnityEngine.AnimationModule` 引用，解决 `Animator` 编译依赖。
- 已执行结果：
  - 自定义按钮左右 ornament 不再是瞬时 `enabled` 开关；
  - 在资源可用的情况下，会走和原生菜单一致的 `MenuCursorAnimator + show/hide` 驱动链路；
  - 资源不可用时仍保留静态 ornament，不会让按钮失去装饰。
- 回归修正：
  - 首次 animator 接入后，ornament 一度“完全不出现”；
  - 直接原因是 animator 模式下把 ornament `Image.color` 错误设成了透明，导致原生 cursor 动画整体不可见；
  - 已修正为 `Color.white`，保持原生动画输出可见。
- 视觉回调：
  - 用户复测后反馈 animator 模式下的左右 ornament “太大、太开”；
  - 根因是直接沿用了原生菜单 `CursorLeft/CursorRight` 的外侧偏移参数（`65f`）和 `0.4f` 缩放，这对当前自定义按钮宽度来说过大、过远；
  - 修正策略是不改动画 controller，只回调自定义 ornament 的本地布局参数：
    - 缩放下调；
    - 偏移从按钮外侧收回到按钮边缘内侧。
- 本轮已执行的具体参数：
  - `NativeScale`: `0.4f -> 0.22f`
  - `NativeOffsetX`: `65f -> 24f`
  - `ApplyNativeLayout()` 的 anchoredPosition 符号从“外侧偏移”改为“内侧偏移”
- 二次微调：
  - 用户反馈当前大小合适，但距离“有点太近”；
  - 仅继续回调间距，不改大小与动画；
  - `NativeOffsetX`: `24f -> 30f`
- 三次观察结论：
  - 用户进一步反馈 spacing 不是统一偏近，而是**不同按钮差异很大**：
    - `录制` 按钮基本合适；
    - `删除` 按钮偏近；
    - 底部 footer 按钮与录制页按钮明显更近。
  - 根因不是单一常量值错误，而是当前所有按钮共用固定 `NativeOffsetX`，但按钮宽度（如 `120 / 140 / 170 / 220`）和文案长度差异很大，固定偏移无法同时适配。
  - 修正方向升级为：
    - 不再用单一固定 ornament 偏移；
    - 改为基于“按钮实际宽度 + 当前文案宽度 + ornament 可视半宽”动态计算每个按钮的左右距离；
    - 必要时允许 ornament 轻微越出按钮边缘，以保持不同按钮上的观感更一致。
- 本轮已执行：
  - `SettingsPageButtonStyle` 新增动态 ornament layout 逻辑；
  - 每次按钮宽度或文案变化时，重新计算：
    - `edgeOffset = buttonWidth / 2 - labelWidth / 2 - targetGap - ornamentHalfWidth`
  - 并把结果同步到左右 ornament controller；
  - 这样 `120 / 140 / 170 / 220` 宽度按钮不再共用同一个固定间距。
- 录制事件行专项修正：
  - 用户确认其它按钮都已正常，仅 `当前完整事件序列` 列表里的 value 按钮仍显得太近；
  - 根因是这类按钮的文本是 `MiddleLeft` 左对齐，但仍沿用了普通按钮的默认左内边距，导致左 ornament 顶到文本起点；
  - 已在 `BuildRecordingStepRow(...)` 中把 value 文本左内边距提升到 `64f`，仅影响该类录制事件行按钮。
- 已验证交付：`dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug` 成功，且产物已部署到游戏 Mods 目录。

### 8.6 Implementation Checklist
- [x] 1. 扩展 `SettingsPageButtonOrnament`，接入 `MenuResources.MenuCursorAnimator`。
- [x] 2. 调整 `CreateSettingsPageButtonOrnament(...)`，让 ornament 节点支持原生 animator 模式。
- [x] 3. 调整 `SettingsPageButtonStyle`，改为通过 ornament controller 触发 `show/hide`，而不是直接切 `Image.enabled`。
- [x] 4. 保留 animator 缺失时的静态 fallback。
- [x] 5. 运行 `dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug`，确认编译与部署成功。

## 9. Follow-up Task: 菜单切换时对齐原生 hide 特效

### 9.0 🚨 Open Questions
None

### 9.1 Requirements (Context)
- **Goal**:
  - 从游戏原生菜单进入宏设置时，原菜单不要瞬间消失，而要走原生下级菜单切换时的 hide 特效。
  - 关闭录制页或关闭设置页时，自定义页面也要有与原生菜单一致风格的消失特效，而不是瞬间隐藏。
- **In-Scope**:
  - 检查原生菜单切换时的 hide/show 动画入口。
  - 调整“进入宏设置”时对原生 `MenuScreen` 的隐藏方式。
  - 为自定义设置页与录制页增加原生风格的分段淡出时序。
- **Out-of-Scope**:
  - 不重做整套自定义窗口结构。
  - 不改业务流程（保存、取消、返回、录制状态机）。

### 9.2 Code Map (Project Topology)
- `hkapi/UIManager.cs`
  - `HideMenu(MenuScreen menu)`: 原生菜单 hide 特效入口。
  - `ShowMenu(MenuScreen menu)`: 原生菜单 show 特效入口。
  - `FadeOutCanvasGroup(...)` / `FadeInCanvasGroup(...)`: 原生菜单淡入淡出节奏与速度来源。
- `hkapi/MenuScreen.cs`
  - `title / topFleur / content / controls / bottomFleur / screenCanvasGroup` 定义了原生菜单切换的分段结构。
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `Show(...)`: 进入宏设置时的入口。
  - `HideNativeMenu(...)` / `RestoreNativeMenu(...)`: 当前仍是 `SetActive(false/true)` 的位置。
  - `CloseRecordingModal(...)`: 关闭录制页的位置。
  - `CloseWindow()`: 关闭设置页并回到底层菜单的位置。

### 9.3 Research Findings / Architecture
- 原生菜单切到下一级时，并不是简单 `SetActive(false)`。
- `UIManager.HideMenu(MenuScreen)` 的真实时序是：
  - 先淡出 `title`
  - `0.1s` 后触发 `topFleur hide`
  - 再 `0.1s` 后淡出 `content / controls`
  - 再 `0.1s` 后触发 `bottomFleur hide`
  - 最后淡出整个 `screenCanvasGroup`
- 因此我们当前 `HideNativeMenu(returnScreen)` / `RestoreNativeMenu()` 的直接 `SetActive(false/true)` 会显得非常突兀。
- 最小修复方向分两层：
  - 对原生菜单本体：直接复用 `UIManager.HideMenu/ShowMenu`
  - 对自定义设置页/录制页：按相同的 `0.1s` 分段节奏复刻 hide 序列，用现有 `CanvasGroup` 做淡出

### 9.4 Detailed Design & Implementation
- `File: HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - 进入设置页：
    - 改造当前 native menu 隐藏流程，不再直接 `returnScreen.gameObject.SetActive(false)`；
    - 改为先准备自定义 overlay，但延后真正显示；
    - 然后调用 `UIManager.instance.HideMenu(returnScreen)`；
    - hide 完成后，再 reveal 自定义设置页。
  - 关闭设置页：
    - 为自定义设置页的 header / content / footer / ornament / root 增加独立 `CanvasGroup` 引用；
    - 关闭时按原生 hide 节奏分段淡出；
    - 自定义页完全隐藏后，调用 `UIManager.instance.ShowMenu(returnScreen)` 恢复底层菜单。
  - 关闭录制页：
    - 为 recording modal 的 title / body / actions / root 增加 `CanvasGroup` 引用；
    - 关闭时按同样的 `0.1s` 分段 hide 时序淡出；
    - 淡出结束后再切回设置页主体。
  - 新增 transition coroutine 管理：
    - 防止 open/close 动画未完成时重复触发下一次切换。
  - 若运行时拿不到 `UIManager.instance`，则保留当前直接显隐作为 fallback。

### 9.5 Execution Notes
- 已执行方案：
  - 进入宏设置时，不再直接 `SetActive(false)` 隐藏原生 `MenuScreen`；
  - 改为先准备自定义 overlay，再调用 `UIManager.HideMenu(returnScreen)`，hide 完成后才 reveal 宏设置窗口；
  - 关闭设置页时，先对自定义设置页执行分段淡出，再调用 `UIManager.ShowMenu(returnScreen)` 恢复底层菜单；
  - 关闭录制页时，对 recording modal 执行分段淡出，结束后再切回设置页主体；
  - 新增 transition coroutine 管理，避免切换动画未结束时重复触发新的 open/close。
- 已落地的自定义 hide 时序：
  - 设置页：`header -> top ornament -> content/footer -> bottom ornament -> window root`
  - 录制页：`title -> hint/preview/status -> actions -> modal root`
  - 各阶段之间统一使用 `0.1s` 间隔，淡出速度沿用原生菜单的 `MENU_FADE_SPEED`，缺失时回退到 `3.2f`。
- 兼容性与 fallback：
  - 若运行时拿不到 `UIManager.instance`，原生菜单仍会回退到直接显隐，不阻塞功能；
  - 自定义设置页与录制页的淡出由本地 `CanvasGroup` 驱动，不依赖原生 `MenuScreen` 结构。
- 已验证交付：`dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug` 成功，且产物已部署到游戏 Mods 目录。

### 9.6 Implementation Checklist
- [x] 1. 进入宏设置时改为复用 `UIManager.HideMenu(returnScreen)`。
- [x] 2. 关闭设置页后改为复用 `UIManager.ShowMenu(returnScreen)` 恢复底层菜单。
- [x] 3. 为自定义设置页增加分段 hide 的 `CanvasGroup` 引用与 coroutine。
- [x] 4. 为录制页增加分段 hide 的 `CanvasGroup` 引用与 coroutine。
- [x] 5. 增加 transition coroutine 防重入与清理逻辑。
- [x] 6. 运行 `dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug`，确认编译与部署成功。

### 9.7 Regression Note: 设置页按钮全部不可点击
- 回归根因：第 9 节为设置页主体分段淡出而新增的多个 `CanvasGroup`（如 `StopContent`、`MacroContent`、`FooterContent`）在可见态被初始化为 `interactable = false`、`blocksRaycasts = false`。
- 这类 `CanvasGroup` 不只是视觉组件；挂在按钮容器上时，会让其下所有 `Selectable` 被父级判定为不可交互，导致设置页一打开所有按钮都处于不可点击状态。
- 最小修复策略：
  - 这些“仅用于过渡淡出”的分段 `CanvasGroup` 在正常可见态必须保持 `interactable = true`、`blocksRaycasts = true`；
  - 只有在 `FadeOutCanvasGroup(...)` 开始执行淡出时，才临时切换为不可交互，避免关闭动画期间误触。
- 这样既保留分段淡出能力，也不会破坏设置页和录制页内按钮的正常输入。

### 9.8 Regression Note: 设置页返回 / Esc 无响应
- 回归现象：设置页打开后，点击“返回”或直接按 `Esc`，用户体感上没有关闭反应。
- 根因拆分：
  - `FinalizeReveal()` 会在窗口显示完成后立即 `FocusInputField(_stopWakeWordInput)`，导致主设置页默认处于文本输入焦点内；
  - `Update()` 中主设置页的 `Esc` 处理又带了 `!IsEditingTextInput()` 条件，因此刚打开页面时按 `Esc` 会被直接拦掉；
  - 同时，默认焦点留在输入框上也会让“我只是想退出页面”的交互显得迟钝，和原生菜单的返回直觉不一致。
- 最小修复策略：
  - 主设置页 reveal 完成后不再自动聚焦停止词输入框，改为清空当前选择；
  - 主设置页无 modal 时，`Esc` 直接走 `RequestBack()`，不再受文本输入焦点限制；
  - delay modal 仍保留现有 `Enter / Esc` 输入行为，不改其编辑流程。

### 9.9 Regression Note: 录制页返回设置后仍无法退出
- 回归现象：首次进入设置页时可以正常关闭；但只要进过一次录制页，再返回设置页后，“返回”和 `Esc` 都像失效一样。
- 根因：
  - `CloseRecordingModalWithTransition()` 在关闭录制页时，先执行 `_recordingModal.SetActive(false)` 与 `HideModalHostIfIdle()`；
  - 但随后又调用 `ResetRecordingModalTransitionState()`，而这个 reset 会把 `_recordingModalCanvasGroup.gameObject.SetActive(true)`；
  - 由于此时父级 `_modalHost` 已被隐藏，录制页不会显示出来，但 `_recordingModal.activeSelf` 会重新变成 `true`；
  - 后续 `RequestBack()` 与 `Update()` 仍按 `activeSelf` 判断“录制页是否打开”，于是主设置页始终被误判为仍处于录制态，返回逻辑被错误导向 `CancelRecordingFromButton()`。
- 最小修复策略：
  - 调整录制页关闭后的 reset 顺序，先恢复下一次打开所需的 alpha / interactable 状态，再把录制页 root 明确设回 inactive；
  - 同时把主设置页对 modal 可见性的判断改为基于 `activeInHierarchy`，避免再次被“父隐藏、子 activeSelf 为 true”的中间态误伤。

### 9.10 Enhancement Note: 设置页与录制页打开动画
- 新需求：设置页和录制页在打开时，也需要和关闭时同风格的过渡效果，避免内容瞬间跳出来。
- 现状问题：
  - 设置页进入时，原生菜单虽然已经先执行 `UIManager.HideMenu(returnScreen)`，但自定义窗口仍是 `SetVisible(true)` 后直接完整出现；
  - 录制页进入时，`_modalHost.SetActive(true)` 与 `_recordingModal.SetActive(true)` 后也是整页瞬间出现。
- 设计策略：
  - 复用现有第 9 节的 `CanvasGroup` 分组，不新增新的 UI 结构；
  - 增加与 `FadeOutCanvasGroup(...)` 对称的 `FadeInCanvasGroup(...)`；
  - 为设置页与录制页分别增加“打开前预置为 alpha 0”的 prepare helper；
  - 打开时按接近原生 `UIManager.ShowMenu(...)` 的节奏分段 reveal，而不是简单一次性显示。
- 计划中的 reveal 时序：
  - 设置页：`window root + header -> top ornament -> stop/macro/footer + bottom ornament`
  - 录制页：`modal root + title -> hint/preview/status -> actions`
  - 各阶段之间同样使用 `0.1s` 间隔，fade speed 继续复用 `MENU_FADE_SPEED` / `DefaultMenuFadeSpeed`。
- 兼容性要求：
  - 保持关闭动画逻辑不变；
  - 保持 delay modal 的输入行为不变；
  - 若 reveal coroutine 被中断，下一次进入前仍可用现有 reset helper 回到完整可见状态。

### 9.11 Execution Notes: 设置页与录制页打开动画
- 已执行方案：
  - 新增与 `FadeOutCanvasGroup(...)` 对称的 `FadeInCanvasGroup(...)`；
  - 新增 `PrepareWindowRevealTransitionState()` / `PrepareRecordingModalRevealTransitionState()`，在真正显示前先把分段 `CanvasGroup` 预置为 `alpha = 0`；
  - 设置页打开时，不再在 layout settle 后直接 `SetVisible(true)` 完整出现，而是改为分段 reveal；
  - 录制页打开时，不再在 `_modalHost.SetActive(true)` 后直接整页显示，而是改为先 hidden-state 准备，再执行 reveal coroutine。
- 已落地的 reveal 时序：
  - 设置页：`window root + header -> top ornament -> stop/macro/footer + bottom ornament`
  - 录制页：`modal root + title -> hint/preview/status -> actions`
  - 各阶段之间继续沿用 `0.1s` 间隔，fade speed 继续复用 `MENU_FADE_SPEED` / `DefaultMenuFadeSpeed`。
- 兼容性说明：
  - 关闭动画链路未改，仍使用现有 hide coroutine；
  - delay modal 行为未改；
  - 录制页入口现在会把 `_pendingTransitionCoroutine` 占用到 reveal 完成，避免打开过程中重复触发关闭或再次进入。
- 已验证交付：`dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug` 成功，且产物已部署到游戏 Mods 目录。

### 9.12 Implementation Checklist
- [x] 1. 为设置页/录制页新增对称的 `FadeInCanvasGroup(...)`。
- [x] 2. 为设置页新增 reveal 前 hidden-state prepare helper。
- [x] 3. 为录制页新增 reveal 前 hidden-state prepare helper。
- [x] 4. 设置页打开改为分段 reveal，而不是直接完整显示。
- [x] 5. 录制页打开改为分段 reveal，而不是直接完整显示。
- [x] 6. 运行 `dotnet build HkVoiceMod/HkVoiceMod.csproj -c Debug`，确认编译与部署成功。
