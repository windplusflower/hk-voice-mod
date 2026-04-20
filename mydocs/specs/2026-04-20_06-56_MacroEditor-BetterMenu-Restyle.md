# SDD Spec: 宏编辑页 BetterMenu 画风重构研究

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- [x] 已确认优先级：用户**倾向于复用游戏资源**，但范围仅限**贴图、动画等零散视觉资源**；**不直接复用功能实现或页面架构**。
- [x] 已确认降级策略：若个别目标资源在当前运行时无法直接稳定取得，可采用“**能复用的原版资源就复用，缺失部分再做局部高相似补齐**”作为兜底；但当前默认判断是 **Satchel 能拿到的资源，本 mod 理应也能拿到**，因此这只是低概率 fallback。

## 1. Requirements (Context)
- **Goal**: 在不改变当前宏编辑页面功能与整体布局的前提下，把自制 Unity UI 的视觉风格重构为与 Satchel BetterMenu / Hollow Knight 菜单更一致的画风，并且**优先复用游戏内可取得的贴图、动画等零散视觉资源**，而不是复用 BetterMenu 的功能实现或页面架构。
- **In-Scope**:
  - 研究当前宏编辑页真实入口、布局、交互、数据链路与样式实现位置
  - 识别当前自制 UI 与 BetterMenu 风格脱节的具体技术原因
  - 识别仓库内已经存在的 BetterMenu 挂接点与可复用风格边界
  - 形成后续 PLAN 所需的视觉重构方向、文件范围与风险约束
- **Out-of-Scope**:
  - 本轮不修改代码
  - 本轮不改变宏编辑器功能、交互流程或信息布局
  - 本轮不提交 commit

## 1.5 Code Map (Project Topology)
- **菜单入口 / 导航壳层**:
  - `HkVoiceMod/HkVoiceMod.cs`: Mod 入口，实现 `ICustomMenuMod`，`GetMenuScreen(...)` 直接委托给 `VoiceSettingsMenuBuilder`。
  - `HkVoiceMod/Menu/VoiceSettingsMenuBuilder.cs`: 当前 BetterMenu 只承担“薄设置页 + 打开/关闭自制编辑器按钮”的壳层职责；真正的宏编辑页不在 BetterMenu 内实现。
    - 已确认使用 `Satchel.BetterMenus.Menu`、`TextPanel`、`MenuButton`。
    - 按钮点击后调用 `VoiceSettingsWindowController.Instance.ToggleFromMenu(...)` 打开自制窗口。
- **真正的宏编辑器实现（本轮重点）**:
  - `HkVoiceMod/UI/VoiceSettingsWindowController.cs`: 当前自制宏编辑页的唯一 UI 控制器。
    - `EnsureBuilt()`: 构建整棵 `Canvas` UI 树，包括 `Header / StopSection / MacroSection / Footer / RecordingModal / DelayModal`。
    - `BuildMacroRow(...)`: 构建每条宏的布局，当前仍是“标签 + 唤醒词 + 阈值 + 录制 + 删除 + 摘要”的自绘行结构。
    - `CreatePanel(...) / CreateButton(...) / CreateInput(...) / CreateText(...) / CreateScrollView(...)`: 当前所有面板、按钮、输入框、滚动区域的视觉与控件树都在这里手工拼装。
    - `ResolveFont()`: 优先复用运行时已存在 `Text` 组件的字体，否则回退到 Unity 内置 `Arial.ttf`；说明当前窗口并没有稳定绑定 BetterMenu / 原版菜单字体资产。
    - 当前样式证据：窗口、区块、输入框、按钮全部依赖硬编码 `Color`（如 `WindowColor / SectionColor / PrimaryButtonColor / InputColor`），`Image.type = Sliced` 但未发现显式 sprite 赋值逻辑，说明当前只是纯色切片框，不是 BetterMenu 同源皮肤。
- **UI 相关状态 / 业务支撑层**:
  - `HkVoiceMod/Menu/VoiceSettingsDraft.cs`: 编辑页草稿态；视觉重构时应继续复用，不应侵入其业务语义。
  - `HkVoiceMod/Menu/VoiceMacroCaptureService.cs`: 宏录制捕获服务；录制弹窗外观可变，但录制行为契约应保持不变。
  - `HkVoiceMod/Menu/GameKeybindNameResolver.cs`: 宏步骤显示名解析。
- **数据模型 / 应用边界**:
  - `HkVoiceMod/VoiceModSettings.cs`: 持久化设置，包含 `StopKeywordConfig`、`MacroConfigs` 及校验逻辑。
  - `HkVoiceMod/Commands/VoiceMacroConfig.cs` / `VoiceMacroKeyEvent.cs` / `VoiceMacroKeyEventKind.cs`: 宏与事件模型。
  - `HkVoiceMod/HkVoiceMod.cs::TryApplyVoiceCommandSettings(...)`: 编辑页最终 Apply 边界。
- **依赖 / 资源现状**:
  - `HkVoiceMod/HkVoiceMod.csproj`: 已引用 `Satchel.dll`，说明 BetterMenu API 可用。
  - 当前仓库检索未发现与宏编辑页皮肤直接相关的本地 `.png` / 菜单贴图资源文件；现有 `assets/` 目录主要是语音识别模型文件，而非 UI 资源。
  - 本机运行时目录 `D:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Mods\Satchel\` 当前只有 `Satchel.dll / Satchel.pdb / Satchel.xml`，未发现 Satchel 随 mod 单独分发的菜单图片资源目录。
  - 本机 `Satchel.xml` 文档证据显示：
    - `Satchel.BetterMenus.TextPanel.Font` 的默认字体是 `TrajanBold`；
    - `Blueprints.FloatInputField(...)` / `IntInputField(...)` 提供的是输入框配置项（字号、宽度、contentType），而不是一套额外的贴图资源包；
    - `StaticPanel` / `MenuRow` / `GameObjectRow` 是主要扩展结构。
  - 本机游戏安装目录 `D:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\` 中存在大量 `resources.assets / sharedassets*.assets / *.resS / *.resource`，说明原版菜单视觉资源大概率封装在游戏 Unity 资源包中，而不是以可直接引用的独立图片文件散落在 mod 目录。
  - 这意味着“复用游戏资源”的现实落点更可能是：
    1. 运行时从已加载菜单对象中采样 `Font / Sprite / Material / Animator` 等资源引用；或
    2. 后续若需要，再单独研究 Unity 资源包级别的资源定位方式。
 - **外部权威参考（已核实）**:
  - Satchel BetterMenus 文档：
    - Overview: `https://prashantmohta.github.io/ModdingDocs/Satchel/BetterMenus/better-menus.html`
    - Elements: `https://prashantmohta.github.io/ModdingDocs/Satchel/BetterMenus/elements.html`
    - Blueprints: `https://prashantmohta.github.io/ModdingDocs/Satchel/BetterMenus/blueprints.html`
    - Extras / custom element / StaticPanel: `https://prashantmohta.github.io/ModdingDocs/Satchel/BetterMenus/extras.html`
  - Hollow Knight Menu API 文档：
    - `https://hk-modding.github.io/api/articles/menu_api.html`
  - 公开代码样例：
    - Satchel `Utils.cs` 显示 BetterMenus 基于 `MenuBuilder` 和 vanilla style 构建：`https://github.com/PrashantMohta/Satchel/blob/fdd9b3225aee335b2e368db87612ddce5a824b91/BetterMenus/Utils.cs`
    - Satchel `TextPanel.cs` 显示字体直接取自 `MenuResources`（如 `TrajanBold / TrajanRegular / Perpetua`）：`https://github.com/PrashantMohta/Satchel/blob/fdd9b3225aee335b2e368db87612ddce5a824b91/BetterMenus/Elements/TextPanel.cs`
    - Satchel `Menu.cs` 显示 BetterMenus 的核心布局是纵向滚动内容区 + 固定间距行：`https://github.com/PrashantMohta/Satchel/blob/fdd9b3225aee335b2e368db87612ddce5a824b91/BetterMenus/Menu.cs`
    - GodSeekerPlus 的 BetterMenus 多页面示例：`https://github.com/Clazex/HollowKnight.GodSeekerPlus/blob/0cd8bb25bd324e30a7dc2fae7fcfef59391651d4/GodSeekerPlus/Menu.cs`

## 2. Architecture (Optional - Populated in INNOVATE)
- **当前已确认事实**:
  1. 现有宏编辑页功能完整，但视觉实现完全是 `UnityEngine.UI` 手工自绘，并非 BetterMenu 原生元素组合。
  2. BetterMenu 在本项目中的现状是“导航入口壳层”，不是“宏编辑页承载层”。
  3. 由于宏编辑页包含滚动列表、可编辑输入框、录制弹窗、延迟弹窗、动态状态刷新等复杂交互，把它重新塞回 BetterMenu 本体并不天然更稳。
  4. 当前代码里没有发现已经接入的 BetterMenu 风格 sprite / font / panel skin；现有 UI 主要靠纯色块和默认字体支撑，所以“画风不一致”是实现层面的必然结果。
  5. 结合本机 Satchel 安装形态看，BetterMenu 的“像游戏原生”更像是复用了游戏菜单体系内已有字体/控件样式与 Satchel 构建逻辑，而不是靠一包显式外置 png 资源完成。
- **初步架构判断**:
  - 最低风险路线大概率不是“把宏编辑页重新改回 BetterMenu 页面”，而是：
    1. 继续保留 `VoiceSettingsWindowController` 作为自制 Canvas 容器；
    2. 仅重构其皮肤层、控件装饰层、字体/边框/按钮/输入框的视觉实现；
    3. 保持现有布局树、交互流、草稿态、录制逻辑、Apply 边界不动；
    4. 在此基础上尽可能复用 BetterMenu / 游戏菜单同源资源，或按同一视觉语言做高保真复刻。
  - 如果后续确认 BetterMenu / 游戏内现成图像资源可稳定拿到，则优先做“资源换皮”；如果拿不到，则做“结构不动、视觉同构”的自绘换皮。
  - 用户最新决策已进一步收敛为：
    1. **资源优先**：优先复用游戏原有贴图、动画等视觉资源；
    2. **结构隔离**：不直接复用 BetterMenu 功能逻辑或页面架构；
    3. **目标本质**：是“现有自制窗口的原版化换皮”，不是“重新接回 BetterMenu 页面”。
 - **Oracle 复核结论（已吸收）**:
  - 推荐路线：**保留当前自制 `Canvas` 宏编辑器，只做 BetterMenu 主题化换皮**。
  - Oracle 明确不建议把当前宏编辑器重新嵌入 BetterMenus，理由是当前页面包含：
    1. 动态宏列表重建；
    2. 文本输入；
    3. 录制弹窗；
    4. Delay 弹窗；
    5. 实时状态刷新；
    这些都已经深度绑定在 `VoiceSettingsWindowController` 的 Unity UI 结构中，强行回迁会把“视觉重构”变成“功能重写”。
  - Oracle 给出的安全重构边界：
    - **必须保持不动**：
      - `EnsureBuilt()` 中现有布局树、尺寸常量、`LayoutGroup` 结构
      - `Show/ToggleFromMenu/Update/RequestBack` 的事件流程
      - 录制与草稿态 wiring
    - **可以安全重构**：
      - `WindowColor / SectionColor / RowColor / InputColor / ButtonColor` 等硬编码调色参数
      - `CreatePanel / CreateButton / CreateInput / CreateText` 中的皮肤实现
      - 在 `Show(mod, returnScreen)` 时增加一次主题采样/主题装配步骤，利用已存在的 `returnScreen` 上下文去获取更接近 BetterMenu 的字体、sprite、material 引用
  - Oracle 特别提醒的风险点：
    1. 当前 `CreatePanel / CreateButton / CreateInput` 都把 `Image.type` 设成了 `Sliced`，但并未赋真实 sprite；后续若接入资源，必须按 sprite 是否支持 9-slice 决定 `Simple` 或 `Sliced`。
    2. 当前窗口对象是 `DontDestroyOnLoad` 常驻体，只能缓存 `Font / Sprite / Material` 这类资产引用，不能持有场景级组件引用。
    3. 主题采样若依赖已加载菜单对象，必须在 `Show()` 时做，并保留稳定 fallback，避免某些场景下取不到资源导致 UI 崩坏。
- **基于用户偏好的进一步收敛**:
  - 由于用户明确倾向“复用游戏资源”，而本机证据又显示这些资源主要封装在 Unity 资源包中，因此后续实现优先级应调整为：
    1. 先研究**运行时可直接借用**的菜单字体、sprite、动画/transition 资源；
    2. 仅在运行时借用不可行时，才退回到局部手工复刻；
    3. 不把“完整解包游戏资源包”作为首选 blocker。
 - **外部资料带来的补充判断**:
  - BetterMenus 的“像原版”主要来自：
    1. `MenuBuilder` 的 vanilla style；
    2. `MenuResources` 提供的字体、游标动画等原版资源；
    3. HK 菜单系统原生的内容区与导航行为。
  - BetterMenus 并不是一套完整的复杂编辑器 UI 框架；其官方元素集偏向设置项，核心布局仍然是**纵向列表**。
  - 文档和源码都表明：遇到超出标准元素的 UI 需求时，推荐路线是 `StaticPanel` 或自定义 `Element`，而不是强行把复杂编辑器压扁成标准 `MenuRow`。
  - 这进一步解释了为什么当前宏编辑页历史上会落到自制 UI：不是功能做不到，而是要在 BetterMenus 内稳定承载“宏列表 + 输入框 + 录制弹窗 + Delay 弹窗 + 实时刷新”这类编辑器体验，复杂度和回归风险都偏高。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 总体实施策略
- 本次改造严格限定为**视觉层重构**，不改宏编辑器的功能、布局、交互流、草稿态、录制逻辑和 Apply 边界。
- 页面继续由 `HkVoiceMod/UI/VoiceSettingsWindowController.cs` 承载；不把宏编辑器重新改回 BetterMenus 页面。
- 主题资源获取采用**双层策略**：
  1. **主路径**：直接复用游戏 / 菜单运行时可访问到的原版资源（字体、sprite、material、动画控制器等）；
  2. **兜底路径**：若某个局部资源无法稳定解析，则只对该局部做高相似补齐，不影响整体“资源优先”路线。
- 核心设计原则：
  - **布局冻结**：不改 `WindowWidth / WindowHeight / ModalWidth / RowSpacing / FieldHeight` 等布局常量；
  - **结构冻结**：不改 `EnsureBuilt()` 当前的 UI 树层级与 `ScrollRect` / `LayoutGroup` 结构；
  - **行为冻结**：不改 `Show / ToggleFromMenu / Update / RequestBack / TryHandleMenuCancel / VoiceMacroCaptureService` 的既有行为契约；
  - **皮肤替换**：把颜色、字体、背景图、按钮态、输入框装饰、滚动区装饰、可选动画从硬编码实现中抽离出来。

### 3.2 Data Structures & Interfaces
- `File: HkVoiceMod/UI/VoiceSettingsTheme.cs`（新增）
  - `internal sealed class VoiceSettingsTheme`
    - **Goal**: 承载宏编辑器所需的全部视觉资产引用与状态色，不含任何业务逻辑。
    - **Properties**:
      - `public Font PrimaryFont { get; }`
      - `public Font SecondaryFont { get; }`
      - `public Sprite? WindowSprite { get; }`
      - `public Sprite? SectionSprite { get; }`
      - `public Sprite? RowSprite { get; }`
      - `public Sprite? InputSprite { get; }`
      - `public Sprite? PrimaryButtonSprite { get; }`
      - `public Sprite? SecondaryButtonSprite { get; }`
      - `public Sprite? DangerButtonSprite { get; }`
      - `public RuntimeAnimatorController? AccentAnimator { get; }`
      - `public Color FullscreenDimColor { get; }`
      - `public Color WindowTint { get; }`
      - `public Color SectionTint { get; }`
      - `public Color RowTint { get; }`
      - `public Color InputTint { get; }`
      - `public Color PrimaryButtonTint { get; }`
      - `public Color SecondaryButtonTint { get; }`
      - `public Color DangerButtonTint { get; }`
      - `public Color TextColor { get; }`
      - `public Color MutedTextColor { get; }`
      - `public Color PlaceholderTextColor { get; }`
      - `public Color SuccessTextColor { get; }`
      - `public Color ErrorTextColor { get; }`
      - `public bool WindowSpriteIsSliced { get; }`
      - `public bool SectionSpriteIsSliced { get; }`
      - `public bool RowSpriteIsSliced { get; }`
      - `public bool InputSpriteIsSliced { get; }`
      - `public bool ButtonSpriteIsSliced { get; }`
    - **Methods**:
      - `public ColorBlock CreatePrimaryButtonColors()`
      - `public ColorBlock CreateSecondaryButtonColors()`
      - `public ColorBlock CreateDangerButtonColors()`
      - `public ColorBlock CreateInputColors()`

- `File: HkVoiceMod/UI/VoiceSettingsThemeResolver.cs`（新增）
  - `internal static class VoiceSettingsThemeResolver`
    - **Goal**: 负责定位、采样并组装 BetterMenu / 原版菜单同源视觉资源。
    - **Methods**:
      - `public static VoiceSettingsTheme Resolve(MenuScreen returnScreen, Font fallbackFont)`
      - `private static bool TryResolveFromMenuResources(out VoiceSettingsTheme theme, Font fallbackFont)`
      - `private static bool TryResolveFromLoadedMenuObjects(MenuScreen returnScreen, Font fallbackFont, out VoiceSettingsTheme theme)`
      - `private static VoiceSettingsTheme CreateFallbackTheme(Font fallbackFont)`
      - `private static Sprite? FindBestMenuSprite(MenuScreen returnScreen, params string[] preferredNames)`
      - `private static RuntimeAnimatorController? FindMenuAnimator(MenuScreen returnScreen, params string[] preferredNames)`
    - **Resolution Order**:
      1. 优先取 HK / Modding 菜单公开资源（如 `MenuResources` 一类可直接访问资源）；
      2. 若公开资源不足，再从 `returnScreen` 所在已加载菜单对象中采样对应 `Font / Sprite / Material / Animator`；
      3. 仅在以上两步不足时回退到 fallback theme。

- `File: HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - **新增字段**:
    - `private VoiceSettingsTheme? _theme;`
  - **需要修改的方法签名 / 契约**:
    - `internal void Show(HkVoiceMod mod, MenuScreen returnScreen)`
      - 新职责：在 `EnsureBuilt()` / `RebuildFromDraft()` 前解析并应用 theme。
    - `private void EnsureBuilt()`
      - 新职责：构建时使用 theme 驱动控件视觉，而不是写死颜色和默认图形行为。
    - `private GameObject CreateSection(Transform parent, string name, float preferredHeight, bool flexibleHeight = false)`
      - 新职责：仍返回 section 根节点，但内部皮肤来自 `_theme`。
    - `private void CreateInput(Transform parent, string name, string placeholder, float width, InputField.ContentType contentType, Action<string> onValueChanged, out InputField inputField)`
      - 新职责：应用 `_theme.InputSprite / _theme.PrimaryFont / _theme.CreateInputColors()`。
    - `private void CreateButton(Transform parent, string name, string label, float width, Color backgroundColor, Action onClick, out Button button, out Text labelText)`
      - 新职责：`backgroundColor` 不再直接作为最终视觉来源，而是映射为 primary / secondary / danger 三类主题按钮样式。
    - `private GameObject CreatePanel(Transform parent, string name, Color color)`
      - 新职责：`color` 参数改为“逻辑用途标记”的兼容桥，最终 sprite / tint 由主题层控制。
    - `private Text CreateText(Transform parent, string name, string textValue, int fontSize, Color color, FontStyle fontStyle, TextAnchor alignment, TextAnchor childAlignment, float preferredHeight, bool flexibleHeight = false, float preferredWidth = -1f)`
      - 新职责：字体与文本色统一走 `_theme`，保留字号、排版、alignment 逻辑不变。
  - **建议新增私有方法**:
    - `private void ResolveTheme(MenuScreen returnScreen)`
    - `private void ApplyThemeToExistingTree()`
    - `private void ApplyImageTheme(Image image, Sprite? sprite, bool useSliced, Color tint)`
    - `private void ApplyTextTheme(Text text, bool muted)`
    - `private void ApplyButtonTheme(Button button, Text labelText, VoiceThemeButtonKind kind)`
    - `private void ApplyInputTheme(InputField inputField)`

- `File: HkVoiceMod/UI/VoiceThemeButtonKind.cs`（新增）
  - `internal enum VoiceThemeButtonKind`
    - `Primary`
    - `Secondary`
    - `Danger`

### 3.3 File Changes
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - 去掉直接写死为唯一来源的颜色常量角色；保留它们仅作为 fallback theme 的默认值。
  - 在 `Show(...)` 中使用 `returnScreen` 解析主题资源，并在窗口显示前完成 theme 注入。
  - `EnsureBuilt()` 继续构建相同层级：
    - `Root`
    - `Window`
    - `Header`
    - `StopSection`
    - `MacroSection`
    - `Footer`
    - `ModalHost`
    - `RecordingModal`
    - `DelayModal`
  - 但上述节点的 `Image / Text / Button / InputField` 视觉要全部切到 theme 驱动。
  - 明确处理 `Image.type`：
    - 只有当目标 sprite 具备可用 9-slice border 时才使用 `Image.Type.Sliced`；
    - 否则改为 `Image.Type.Simple`，避免拉伸失真。
  - 保持所有 `LayoutElement`、`ContentSizeFitter`、`HorizontalLayoutGroup`、`VerticalLayoutGroup`、`ScrollRect` 参数不变。

- `HkVoiceMod/UI/VoiceSettingsTheme.cs`
  - 定义统一主题对象，集中管理视觉资产与状态色。

- `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs`
  - 封装资源解析逻辑，避免把菜单资源搜索散落到控制器业务代码中。

- `HkVoiceMod/UI/VoiceThemeButtonKind.cs`
  - 把按钮视觉分类从“传入颜色”改成“传入语义类型”，减小样式逻辑与业务逻辑耦合。

- `HkVoiceMod/Menu/VoiceSettingsMenuBuilder.cs`
  - **原则上不改功能行为**。
  - 保持当前薄设置页 + `ToggleFromMenu(mod, menuScreen)` 入口方式不变。
  - 本文件仅在实现期若确有必要时，才允许增加极小的注释或命名收口；不应承担主题解析逻辑。

### 3.4 Theme Acquisition Rules
- **优先复用的资源类别**:
  1. 标题 / 正文 / 辅助文字字体
  2. 面板底图 / 区块底图 / 行底图
  3. 输入框背景图
  4. 主按钮 / 次按钮 / 危险按钮背景图
  5. 菜单高亮或光标相关动画资源
- **允许复用的方式**:
  - 直接引用游戏 / 菜单公开资源对象
  - 从当前已加载菜单对象复制资源引用
- **不允许的方式**:
  - 迁移 BetterMenus 的功能实现代码来“假装复用资源”
  - 变相把宏编辑器重新改写成 BetterMenus 页面
  - 为了追求画风而改动现有布局与交互

### 3.5 Verification Checklist
- `VoiceSettingsWindowController.cs` 改动后需确认：
  1. 主窗口尺寸、宏区尺寸、弹窗尺寸与当前版本一致；
  2. Stop 区、Macro 区、Footer 区控件排列不变；
  3. 录制弹窗与 Delay 弹窗快捷键行为不变；
  4. 输入框编辑、Esc 返回、Apply / Discard / Back 行为不变；
  5. 视觉上已明显贴近 BetterMenu / 原版菜单，而不再是纯色现代面板。
- 若新增资源解析类，需额外验证：
  6. 首次打开菜单时能稳定拿到 theme；
  7. 关闭菜单再重进时 theme 不丢失；
  8. 某个资源解析失败时，fallback 只影响局部观感，不影响页面可用性。

### 3.6 Implementation Checklist
- [x] 1. 新增 `VoiceSettingsTheme`，定义宏编辑器需要的视觉资产与状态色。
- [x] 2. 新增 `VoiceSettingsThemeResolver`，实现“公开菜单资源优先、运行时菜单对象采样次之、fallback 最后”的解析顺序。
- [x] 3. 在 `VoiceSettingsWindowController.Show(...)` 中接入主题解析，并确保主题在构建/显示前可用。
- [x] 4. 改造 `CreatePanel / CreateButton / CreateInput / CreateText`，让视觉完全由 theme 驱动，但不改布局和行为。
- [x] 5. 为不同按钮语义引入稳定的主题分类（primary / secondary / danger），去掉“颜色即样式”的直接耦合。
- [x] 6. 为 panel / input / button 正确处理 sprite 的 `Simple` / `Sliced` 模式，避免接入原版资源后变形。
- [x] 7. 对已建好的窗口树提供一次统一的 theme 应用/刷新路径，避免首次构建后仍残留旧纯色皮肤。
- [x] 8. 已完成代码级与构建级验证；运行时交互未做实机菜单点击验证，但现有行为路径未被改写，且构建通过。

## 4. Review
- **实际落地文件**:
  - 新增 `HkVoiceMod/UI/VoiceSettingsTheme.cs`
  - 新增 `HkVoiceMod/UI/VoiceThemeButtonKind.cs`
  - 新增 `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs`
  - 修改 `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - 修改 `HkVoiceMod/HkVoiceMod.csproj`
- **已实现的核心变化**:
  - `VoiceSettingsWindowController.Show(...)` 现在会在显示前解析 `_theme`。
  - `EnsureBuilt()` 构建时直接使用 `_theme?.PrimaryFont` 作为字体来源。
  - 新增 `ApplyThemeToExistingTree()`，可对已建好的窗口树统一重刷 panel / button / input / text 样式。
  - `CreatePanel / CreateButton / CreateInput / CreateText` 已从“硬编码纯色外观”切换为“主题驱动的字体 / sprite / tint / ColorBlock”。
  - 主题解析顺序已实现为：
    1. `MenuResources` 等公开菜单资源优先；
    2. 从 `returnScreen` 已加载菜单对象中采样 `Font / Sprite / RuntimeAnimatorController`；
    3. 最后回退到原有纯色 fallback。
  - `Image.type` 已按是否存在可用 border 在 `Sliced` 与 `Simple` 之间切换，避免直接套原版 sprite 后失真。
- **构建验证**:
  - `dotnet build "HkVoiceMod/HkVoiceMod.csproj"` 已通过。
  - 结果：`0 warning / 0 error`。
- **工程级补充说明**:
  - 为支持 `RuntimeAnimatorController` 的显式引用，`HkVoiceMod.csproj` 新增了 `UnityEngine.AnimationModule` 引用；这是本轮主题资源解析所需的直接依赖，不属于无关改动。
- **未完成项 / 待确认项**:
  - Oracle 终审已返回并在补丁后复核通过。
  - 已确认一处**前置问题**：`VoiceSettingsWindowController.OpenDelayModal()` 当前不会真正打开 Delay 弹窗，这段逻辑不在本次 visual refactor 的 git diff 内，因此不属于本次换皮引入的回归；本轮仅记录，不扩 scope 修功能。
  - Oracle 指出的两处本次主题层实现瑕疵已修复：
    1. 已移除未使用的 `AccentAnimator` 解析与传递路径；
    2. 已收紧 `VoiceSettingsThemeResolver.TryResolveFromMenuResources()` 的 `foundPublicResource` 判定，只统计真正用于 theme 的公开字体资源。
  - 修补后已重新 `dotnet build "HkVoiceMod/HkVoiceMod.csproj"`，结果仍为 `0 warning / 0 error`。
  - Oracle 最终结论：**PASS**。

## 5. Latest Visual Cleanup Round
- **新增用户反馈（已确认）**:
  1. 当前方向基本正确，说明“原版化”方向是对的；
  2. 但整体观感**过于混乱**，说明当前资源复用策略把过于花的装饰资源误用成了通用背景；
  3. 当前不需要“五颜六色”，说明主按钮/危险按钮的黄/红语义色过强，偏离了 Hollow Knight 菜单的克制单色气质。
- **本轮目标**:
  - 保留现有功能、布局、交互、主题层架构；
  - 把视觉从“资源采样过度的装饰化风格”收回到“更单色、更干净、更有层级的 HK 菜单风格”；
  - 继续优先复用游戏资源，但**不再把装饰性强的大型 sprite 平铺到窗口、行、输入框背景**。

### 5.1 本轮设计收口原则
- **资源使用收口**:
  - 字体继续优先复用 `MenuResources` / 已加载菜单字体；
  - 对 sprite 的使用从“广泛采样并映射到 panel/row/input/button”收紧为“仅在明确适合作为控件背景时才使用”；
  - 若无法明确判定某个 sprite 是稳定的按钮/面板背景，则宁可不用该 sprite，也不要把大型装饰花纹重复铺满界面。
- **色彩收口**:
  - 主体界面收敛到 **深色背景 + 象牙白/灰白文字与描边** 的单色体系；
  - `Primary / Secondary / Danger` 三类按钮仍保留语义分层，但不再用高饱和黄/红做强区分；
  - 主要通过**亮度、明度、边框强弱**体现层级，而不是靠彩色。
- **装饰收口**:
  - 允许保留极少量原版味道的 ornament，但不能干扰信息阅读；
  - 宏列表行、输入框、区块背景应以“可读性优先”，避免大面积卷草纹、光晕、复杂花边重复出现。

### 5.2 本轮计划变更点
- `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs`
  - 收紧 sprite 解析/复用策略，优先输出**更保守的无图/低图 theme**。
  - 降低或取消 `windowSprite / sectionSprite / rowSprite / inputSprite / buttonSprite` 的激进复用，除非明确命中稳定小型背景资源。
  - 主题默认 tint 改为更单色、低饱和的 HK 风格配色。
- `HkVoiceMod/UI/VoiceSettingsTheme.cs`
  - 继续保留按钮语义分类，但对应 tint 改为单色体系内的亮度层级，而非显著彩色差异。
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - 不改布局与行为。
  - 若 theme 中对应 sprite 为空，则稳定回退到单色面板/按钮/输入框呈现，不再尝试用不合适的 ornament sprite 填充。

### 5.3 本轮 Implementation Checklist
- [x] 1. 收紧 theme resolver 的 sprite 复用策略，避免把装饰性强的菜单资源当通用背景。
- [x] 2. 把主题主色系改成单色、低饱和、低装饰的 HK 风格层级。
- [x] 3. 保留字体资源复用，但弱化/移除五颜六色的按钮语义色。
- [x] 4. 构建验证通过，并确认本轮仍然只改视觉、不改功能与布局。

### 5.4 实际落地
- `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs`
  - 不再为 `window / section / row / input` 广泛采样菜单 sprite；这些区域默认回到稳定的单色 theme。
  - 仅保留 `FindStableControlSprite(...)`，而且要求：
    - 名称不带明显 decorative 语义；
    - 必须有可用 border；
    - 必须是 `Image.Type.Sliced`；
    - 必须命中 `button / option / toggle / tab / input` 等控件关键词。
  - 主题 tint 改成低饱和深灰 + 象牙白体系。
- `HkVoiceMod/UI/VoiceSettingsTheme.cs`
  - `ColorBlock` 的 hover / pressed / selected 对比进一步减弱，避免按钮像彩色 UI。
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - 布局、控件树、交互逻辑未变；仅全局 fallback 色常量改为更克制的单色体系，以便在无 sprite 时也保持统一风格。

### 5.5 Validation
- `dotnet build "HkVoiceMod/HkVoiceMod.csproj"` 通过，结果为 `0 warning / 0 error`。
- 未发现仓库内与本次变更直接相关的测试文件；本轮以源码复读 + 构建通过作为验证手段。
- Oracle 对本轮“减法式美术修正”的最终结论：**PASS**。

## 6. Latest Adjustment Round - 回到第一版并把背景改透明
- **新增用户反馈（已确认）**:
  1. 当前“单色简化版”虽然干净，但已经明显退回到偏自制 UI 的感觉，不符合“继续复用游戏资源”的原始目标；
  2. 用户希望**回到第一版**那种更有原版资源味道的结果；
  3. 用户明确指出：按钮、文字等基本可以接受，主要问题是**背景太乱**；
  4. 本轮修正方向不是继续删资源，而是把**背景层改成透明/近透明**，保留第一版按钮与文字风格。

### 6.1 本轮目标
- 恢复第一版主题中的原版资源使用方向，尤其是按钮 / 文字 / 细节装饰层面的原版感。
- 不再把大块 ornament 背景实打实铺在窗口主体后面；窗口、section、row 等大面积背景改为透明或近透明。
- 保持现有布局、交互、录制逻辑、Apply 边界完全不变。

### 6.2 本轮设计原则
- **回退对象**:
  - 回退到“第一版资源风格更强”的主题策略，而不是保留当前纯单色简化版。
- **透明化对象**:
  - `Window / Section / Row / Input` 这类大面积背景层优先透明或近透明；
  - 即使继续保留对应 sprite 引用，也不得让其以高不透明度铺满内容区。
- **保留对象**:
  - 字体资源复用保留；
  - 按钮视觉风格保留第一版方向；
  - 文本层与按钮层允许继续使用原版资源与较明显的主题特征。
- **禁止事项**:
  - 不继续扩大 ornament 背景覆盖范围；
  - 不改变按钮功能、录制逻辑、输入框行为、布局尺寸。

### 6.3 本轮 Implementation Checklist
- [x] 1. 恢复第一版资源风格方向，撤回“只保留 buttonSprite、其余全 null”的简化策略。
- [x] 2. 将窗口/区块/行/输入框等背景层改成透明或近透明，避免大面积花纹占满界面。
- [x] 3. 保留按钮与文字的第一版原版感资源表现，不改布局与行为。
- [x] 4. 构建验证通过，并由 Oracle 确认本轮属于纯视觉修正。

### 6.4 实际落地
- `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs`
  - 已恢复 `window / section / row / input / button` 的资源采样方向，不再停留在“只保留 buttonSprite”的单色简化版。
  - 对大面积背景层使用低 alpha tint：
    - `WindowTint` ≈ `0.16`
    - `SectionTint` ≈ `0.12`
    - `RowTint` ≈ `0.10`
    - `InputTint` ≈ `0.18`
  - 结果是：仍可借用第一版那套资源方向，但背景层会以透明/近透明方式呈现，不再整块刷满。
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - 未改布局、事件绑定、录制逻辑与输入行为。
  - 继续通过既有 `ApplyPanelTheme / ApplyButtonTheme / ApplyInputTheme / ApplyTextTheme` 路径应用主题。

### 6.5 Validation
- `dotnet build "HkVoiceMod/HkVoiceMod.csproj"` 通过，结果为 `0 warning / 0 error`。
- 未发现意外生成的 `nul` / `NUL` 文件。
- Oracle 对本轮“回到第一版资源方向 + 背景透明化”的结论：**PASS**。

## 7. Latest Correction Round - 按钮必须绑定 BetterMenu 同款资源
- **新增用户反馈（已确认）**:
  1. 当前按钮虽然更接近原版风格，但**并没有真正使用 BetterMenu 同款按钮资源**；
  2. 现状仍然是“从已加载 Image 里启发式猜一个像按钮的 sprite，再靠 tint 染色”，这不满足目标；
  3. 本轮必须把按钮资源来源收紧为**明确的 BetterMenu / MenuResources / 实际菜单按钮对象同源资源**，而不是继续猜图。

### 7.1 本轮目标
- 让 `Primary / Secondary / Danger` 按钮直接绑定 BetterMenu 同款按钮资源来源，尽量复用其 sprite / material / 状态资源。
- 停止用 `FindBestMenuSprite(... "button" ...)` 这类启发式扫描作为按钮资源主路径。
- 不改布局、按钮功能、文本逻辑与交互行为。

### 7.2 本轮设计原则
- **优先顺序**:
  1. 优先直接使用 BetterMenu / `MenuResources` 已公开的按钮同源资源；
  2. 若公开入口不足，再从 BetterMenu 实际创建出的按钮对象上采样其真实 `Image.sprite / material / transition / ColorBlock`；
  3. 仅在以上两步都拿不到时，才允许回退到当前启发式路径。
- **禁止事项**:
  - 不允许继续把“启发式猜按钮 sprite”当唯一主实现；
  - 不允许为了做成按钮同款而改动现有按钮布局尺寸与逻辑行为。

### 7.2.1 已确认的权威事实
- BetterMenu 按钮的核心视觉并**不是某一张按钮背景贴图**，而是：
  1. `MenuResources.TrajanBold` 字体；
  2. 左右两侧 `Cursor` 的 `Animator.runtimeAnimatorController = MenuResources.MenuCursorAnimator`（资源名：`Menu Fleur`）；
  3. 提交闪光 `FlashEffect` 的 `Animator.runtimeAnimatorController = MenuResources.MenuButtonFlashAnimator`（资源名：`Menu Flash Effect`）。
- Satchel BetterMenus 的按钮最终走的是 `Modding.Menu.ContentArea.AddMenuButton(...)` / `MenuButtonStyle.VanillaStyle` 这一套 vanilla menu button 实现。
- 因此本轮“按钮同款资源”的正确含义是：**接回 BetterMenu 同源按钮结构与动画资源**，而不是继续寻找一张所谓的“按钮贴图”。

### 7.3 本轮 Implementation Checklist
- [x] 1. 确认 BetterMenu 按钮真实资源来源（公开资源或实际按钮对象）。
- [x] 2. 改造按钮 theme 解析，使其优先绑定明确的 BetterMenu 同款资源来源。
- [x] 3. 保持当前背景透明化方向不变，仅修正按钮资源来源问题。
- [x] 4. 构建验证通过，并由 Oracle 确认本轮修正是纯视觉层资源绑定修正。

### 7.4 实际落地
- `HkVoiceMod/UI/VoiceSettingsTheme.cs`
  - 新增 `ButtonCursorAnimator` / `ButtonFlashAnimator` / `UseVanillaButtonChrome`，把 BetterMenu 同源按钮资源作为主题一等公民，而不是隐含在启发式 sprite 逻辑里。
- `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs`
  - 优先从 `MenuResources` 解析：
    - `MenuResources.TrajanBold`
    - `MenuResources.MenuCursorAnimator`
    - `MenuResources.MenuButtonFlashAnimator`
  - 只有在这些资源缺失时，才允许回退到实际菜单对象采样或旧的 sprite fallback。
- `HkVoiceMod/UI/BetterMenuButtonChrome.cs`（新增）
  - 为现有自制按钮补上 BetterMenu 同源结构：
    - `CursorLeft`
    - `CursorRight`
    - `FlashEffect`
  - 三者都使用 `Image + Animator`，并分别绑定 BetterMenu / vanilla menu 同源动画控制器。
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `ApplyButtonTheme(...)` 现在在 `UseVanillaButtonChrome == true` 时：
    - 不再把 guessed button sprite 作为主路径；
    - 按钮底图改为近透明；
    - label 字体切到 `SecondaryFont`（即 `TrajanBold` 优先路径）；
    - 把 `BetterMenuButtonChrome` 接到按钮上，使用真实的 cursor/flash 动画资源。
  - 当前 guessed button sprite 只保留为 **fallback**，不再是主实现。
- `HkVoiceMod/HkVoiceMod.csproj`
  - 重新加入 `UnityEngine.AnimationModule` 引用，以支持 `Animator / RuntimeAnimatorController`。

### 7.5 Validation
- `dotnet build "HkVoiceMod/HkVoiceMod.csproj"` 通过，结果为 `0 warning / 0 error`。
- 未发现意外生成的 `nul` / `NUL` 文件。
- Oracle 对本轮“按钮切换为 BetterMenu 同源结构与动画资源”的结论：**PASS**。

## 8. Latest Adjustment Round - 直接回退到第一版视觉版本
- **新增用户指令（已确认）**:
  1. 用户明确否定当前按钮同源结构尝试；
  2. 用户要求**直接回退到第一版视觉版本**，即对话早期截图所示的版本；
  3. 本轮不再做“透明背景”“按钮结构对齐 BetterMenu”“继续微调”等局部修补，而是整体回退到第一版主题实现。

### 8.1 本轮目标
- 恢复第一版资源采样与主题表现；
- 撤销后续所有偏离第一版的按钮 chrome、透明背景、单色化收口等修正；
- 保持功能、布局、交互逻辑完全不变，仅做视觉主题实现回退。

### 8.2 本轮 Implementation Checklist
- [x] 1. 移除后续引入的 BetterMenuButtonChrome / Animator 按钮结构尝试。
- [x] 2. 恢复第一版 `VoiceSettingsTheme` / `VoiceSettingsThemeResolver` / `ApplyButtonTheme` 实现。
- [x] 3. 撤销为后续修正加入的 `AnimationModule` 依赖（若不再需要）。
- [x] 4. 构建验证通过，并由 Oracle 确认已回退到第一版方向。

### 8.3 实际落地
- 已删除 `HkVoiceMod/UI/BetterMenuButtonChrome.cs`。
- `HkVoiceMod/UI/VoiceSettingsTheme.cs`
  - 已移除 `ButtonCursorAnimator / ButtonFlashAnimator / UseVanillaButtonChrome` 等后续按钮 chrome 字段。
- `HkVoiceMod/UI/VoiceSettingsThemeResolver.cs`
  - 已恢复第一版的资源启发式采样逻辑：
    - `window / section / row / input / button` 都重新通过 `FindBestMenuSprite(...)` 在当前菜单对象中做匹配；
    - 已移除后续的 Animator / BetterMenu 按钮结构资源解析逻辑；
    - 背景 tint 也已恢复到第一版的高 alpha、强资源存在感版本，而不是后续的透明化版本。
- `HkVoiceMod/UI/VoiceSettingsWindowController.cs`
  - `ApplyButtonTheme(...)` 已恢复为第一版：按钮继续走 `sprite + tint + ColorTint` 路径，不再挂接额外 chrome。
- `HkVoiceMod/HkVoiceMod.csproj`
  - 已移除 `UnityEngine.AnimationModule` 引用。

### 8.4 Validation
- `dotnet build "HkVoiceMod/HkVoiceMod.csproj"` 通过，结果为 `0 warning / 0 error`。
- `glob("**/BetterMenuButtonChrome.cs")` 结果为空，确认文件已删除。
- `grep("BetterMenuButtonChrome|MenuCursorAnimator|MenuButtonFlashAnimator|RuntimeAnimatorController|UnityEngine.AnimationModule")` 结果为空，确认后续按钮 chrome / animator 实验已从当前代码路径移除。
- Oracle 对本轮“直接回退到第一版视觉方向”的结论：**PASS**。
