# SDD Spec: HK Voice Mod Vosk 验证版

## 0. Open Questions
- [x] `短按一次` 默认时长采用 `80ms`
- [x] Vosk 中文模型以外部目录方式提供，不直接打进 mod 包
- [x] 首版麦克风采集先以 `Windows + NAudio` 为目标环境验证

## 1. Requirements (Context)
- **Goal**: 在 Hollow Knight 中实现一个离线中文单字声控 mod，首版采用 Vosk 完成可运行验证，同时保留后续替换为 DS-CNN 的架构边界。
- **In-Scope**:
  - 11 个语音词条（10 个动作词 + `停`）
  - 子线程录音与识别
  - 主线程队列消费
  - `VoiceCommand -> KeyActionProfile -> InputInjector` 查表执行
  - 直接驱动 `HeroActions`
  - 新建独立项目目录 `HkVoiceMod/`
  - 构建期允许使用条件 stub 完成非 Windows / 无 `GameDir` 环境下的编译验证
- **Out-of-Scope**:
  - DS-CNN 训练与推理实现
  - 朝向/魂量/技能解锁/地形等游戏状态判断
  - 真实语料采集
  - 复杂 UI 菜单与可视化配置页

## 1.1 Context Sources
- Requirement Source: `用户原话 + 本轮确认映射`
- Design Refs: `mydocs/context/2026-04-11_16-32_hk-voice-mod_context_bundle.md`
- Chat/Business Refs: `本轮会话中已确认的推荐方案与约束`
- Extra Context: `hk-api` 技能中的 `HeroActions / InputHandler / InControl` 源码事实

## 1.5 Codemap Used (Feature/Project Index)
- Codemap Mode: `project`
- Codemap File: `mydocs/codemap/2026-04-11_16-32_hk-voice-mod项目总图.md`
- Key Index:
  - Entry Points / Architecture Layers: `Mod入口 -> RuntimeController -> RecognitionBackend -> Queue -> InputInjector`
  - Core Logic / Cross-Module Flows: `识别结果只经 ConcurrentQueue 进入主线程`
  - Dependencies / External Systems: `HK Modding API / HeroActions / InControl / Vosk / 麦克风采集库`

## 1.6 Context Bundle Snapshot (Standard)
- Bundle Level: `Standard`
- Bundle File: `mydocs/context/2026-04-11_16-32_hk-voice-mod_context_bundle.md`
- Key Facts:
  - 路线固定为 `先 Vosk 验证 -> 后 DS-CNN 正式化`
  - `停` 只取消持续按键
  - `左/右` 是持续保持；`上/下/跳` 是 `0.5s` 定时保持；其余为短按
- Resolved Defaults:
  - `短按` 时长固定为 `80ms`
  - 模型目录使用相对路径 `assets/vosk-model-cn`
  - 首版采集平台固定为 `Windows + NAudio`

## 2. Research Findings
- `InputHandler.Awake()` 会创建 `HeroActions`，因此运行时只需在主线程读取 `InputHandler.Instance?.inputActions`
- `HeroController` 同时读取：
  - `inputActions.left/right/up/down/attack/jump/dash/cast`
  - `inputActions.moveVector.Vector.x/y`
- 因此“只改单键不改轴向量”会造成方向状态不一致，注入器必须同步单键与 `moveVector`
- `PlayerAction.CommitWithState(...)` 可用于按 tick 写入动作态；识别线程不能直接操作 Unity/HK 对象
- Vosk 验证版可用命令白名单降低误触发，但模型体积不能满足“几十 KB”最终目标，因此必须把验证版与正式版目标拆开
- 当前仓库为空，最稳妥方案是从零建立一个最小 HK mod 项目，并将识别后端与注入层解耦

## 2.1 Next Actions
- 下一步动作 1：确认本 SPEC 中的默认值与文件清单
- 下一步动作 2：收到 `Plan Approved` 后进入 Execute，先搭骨架，再实现注入与识别

## 3. Innovate (Optional: Options & Decision)
### Option A
- Pros: `Vosk` 零训练即可快速验证整链路，跨说话人能力更稳
- Cons: 模型较大，不满足最终嵌入式级体积目标

### Option B
- Pros: 直接做 `DS-CNN` 更接近最终产品形态
- Cons: 当前无真实语料，只靠 TTS 时实现风险更高，且验证周期更长

### Decision
- Selected: `Option A`
- Why: 当前目标是先把“识别 -> 队列 -> HeroActions 注入”整链路跑通，Vosk 验证版风险最低；同时保留识别后端抽象，后续替换为 DS-CNN 时不重写注入层与主线程调度层。

### Skip (for small/simple tasks)
- Skipped: `false`
- Reason: 已保留选项对比，但路线已由前置讨论基本确定

## 4. Plan (Contract)
### 4.1 File Changes
- `HkVoiceMod/HkVoiceMod.csproj`: 创建 net472 SDK 风格 HK mod 项目，声明 HK/Unity/Modding/Vosk/采集依赖与构建输出策略
- `HkVoiceMod/HkVoiceMod.cs`: Mod 入口，初始化运行时控制器与设置
- `HkVoiceMod/VoiceModSettings.cs`: 全局设置，包含开关、模型路径、短按时长、定时长按时长、日志开关
- `HkVoiceMod/Runtime/VoiceRuntimeController.cs`: 主线程协调器，负责启动/停止识别后端、消费队列、驱动注入器
- `HkVoiceMod/Recognition/IVoiceRecognitionBackend.cs`: 识别后端抽象
- `HkVoiceMod/Recognition/RecognizedCommandEvent.cs`: 识别结果 DTO
- `HkVoiceMod/Recognition/Vosk/VoskVoiceRecognitionBackend.cs`: Vosk 验证版录音+识别后端
- `HkVoiceMod/Commands/VoiceCommand.cs`: 命令枚举
- `HkVoiceMod/Commands/HeroActionKey.cs`: HeroActions 键枚举
- `HkVoiceMod/Commands/KeyPressMode.cs`: `Tap / TimedHold / ContinuousHold / ReleaseContinuous`
- `HkVoiceMod/Commands/KeyActionProfile.cs`: 查表数据结构
- `HkVoiceMod/Commands/VoiceCommandMap.cs`: 固定映射表
- `HkVoiceMod/Input/IInputInjector.cs`: 输入注入接口
- `HkVoiceMod/Input/HeroActionInputInjector.cs`: HeroActions 注入实现，处理左右持续按住、定时动作、组合短按、`停`
- `HkVoiceMod/Compatibility/Stubs/*.cs`: 无 `GameDir` 时用于编译验证的最小兼容桩，不参与真实 HK 运行时
- `HkVoiceMod/README.md`: 构建、依赖、模型目录、安装说明
- `HkVoiceMod/assets/vosk-model-cn/README.md`: 模型放置占位说明，不直接提交大模型本体
- `.gitignore`: 忽略 `bin/obj/artifacts`、本地模型内容与工具状态文件

### 4.2 Class Design

#### `HkVoiceMod : Mod, IGlobalSettings<VoiceModSettings>`
- 责任：
  - mod 生命周期入口
  - settings 读写
  - 创建/销毁 `VoiceRuntimeController`

#### `VoiceRuntimeController : MonoBehaviour`
- 责任：
  - 持有 `ConcurrentQueue<RecognizedCommandEvent>`
  - 在主线程消费识别结果
  - 调用 `VoiceCommandMap` 与 `IInputInjector`
  - 管理识别后端的启动、停止、异常日志

#### `VoskVoiceRecognitionBackend : IVoiceRecognitionBackend`
- 责任：
  - 创建麦克风采集与 Vosk recognizer
  - 在子线程循环读取 PCM 数据
  - 只输出白名单中的最终命令词
  - 将结果写入线程安全队列

#### `HeroActionInputInjector : IInputInjector`
- 责任：
  - 将 `VoiceCommand` 转为 HeroActions 状态变化
  - 管理持续按住集（`left/right`）
  - 管理定时动作集（`up/down/jump`）
  - 管理短按窗口（`attack/dash/cast/up+cast/down+cast`）
  - 同步 `moveVector`

#### `VoiceCommandMap`
- 责任：
  - 维护 `VoiceCommand -> KeyActionProfile` 的唯一映射表
  - 不包含任何游戏状态判断

### 4.3 Key Interfaces
- `public interface IVoiceRecognitionBackend : IDisposable`
  - `void Start(ConcurrentQueue<RecognizedCommandEvent> outputQueue);`
  - `void Stop();`
  - `bool IsRunning { get; }`

- `public interface IInputInjector`
  - `void Dispatch(VoiceCommand command, float realtimeSinceStartup);`
  - `void Tick(float unscaledDeltaTime, float realtimeSinceStartup);`
  - `void ReleaseContinuousInputs();`

- `public sealed class RecognizedCommandEvent`
  - `public VoiceCommand Command { get; }`
  - `public string RawText { get; }`
  - `public float Timestamp { get; }`

- `public sealed class KeyActionProfile`
  - `public VoiceCommand Command { get; }`
  - `public KeyPressMode Mode { get; }`
  - `public IReadOnlyList<HeroActionKey> Keys { get; }`
  - `public float DurationSeconds { get; }`
  - `public bool ReleaseOppositeHorizontalHold { get; }`

### 4.4 Runtime Rules
- `左/右`：
  - 收到后进入持续按住态
  - 自动释放相反方向的持续按住态
  - 仅在收到 `停` 或新的相反方向指令时松开
- `上/下/跳`：
  - 建立一个 `0.5s` 定时动作
  - 到时自动松开
- `劈/冲/波`：
  - 使用统一短按窗口，默认 `80ms`
- `吼/砸`：
  - 以同一短按窗口同时按下两键：`up + cast` 或 `down + cast`
- `停`：
  - 只释放 `left/right` 的持续按住态
  - 不取消已进入倒计时的 `up/down/jump` 动作
- 模型路径：
  - 默认相对 mod 程序集目录解析 `assets/vosk-model-cn`
  - 若 settings 提供绝对路径，则优先使用绝对路径
- 打包策略：
  - 构建后输出到 `HkVoiceMod/artifacts/package/HkVoiceMod/`
  - 包含 `HkVoiceMod.dll`、`HkVoiceMod.pdb`、`README.md`、`assets/vosk-model-cn/README.md`
  - 不包含 Vosk 模型本体，由用户自行放入相对模型目录
- 构建验证策略：
  - 有 `GameDir` 时链接真实 HK/Unity/Modding 程序集
  - 无 `GameDir` 时启用兼容 stub，保证当前仓库可执行编译验证

### 4.5 Implementation Checklist
- [x] 1. 初始化 `HkVoiceMod/` 项目骨架与依赖声明，确保后端与注入层目录边界固定
- [x] 2. 实现命令模型、枚举、映射表、配置默认值
- [x] 3. 实现主线程 `HeroActionInputInjector`，包含定时器、持续按住态、组合短按与 `moveVector` 同步
- [x] 4. 实现 `VoiceRuntimeController`，完成队列消费与 mod 生命周期衔接
- [x] 5. 实现 `VoskVoiceRecognitionBackend`，完成子线程录音识别与白名单命令输出
- [x] 6. 补齐 README、模型目录说明与日志策略
- [x] 7. 补齐条件构建、打包输出与 `.gitignore`

### 4.6 Spec Review Notes
- Requirement clarity & acceptance: `PASS`
  - 证据：命令映射、短按时长、模型路径、首版平台与打包边界均已固定
- Plan executability: `PASS`
  - 证据：文件路径、职责边界、接口签名、checklist 已原子化
- Risk / rollback readiness: `PASS`
  - 证据：已将 Vosk 验证版与 DS-CNN 正式版边界拆开，并识别出 `moveVector` 同步风险
- Readiness Verdict: `GO`
- Risks & Suggestions:
  - Vosk 模型保持外部目录挂载，避免仓库与发布包膨胀
  - 若采集库在执行期与平台冲突，必须先回写 Spec 再改实现策略
- User Decision: `Plan Approved`

## 5. Execute Log
- [x] 已收到 `Plan Approved`，进入 Execute
- [x] 已确认默认值：`短按=80ms`、`Windows + NAudio`、模型相对路径 `assets/vosk-model-cn`
- [x] 已确认打包与忽略策略：模型不入包，构建产物进入 `artifacts/package`
- [x] 已创建 `net472` SDK 风格项目，依赖 `NAudio 2.3.0`、`Vosk 0.3.38`、`Newtonsoft.Json 13.0.4`
- [x] 已实现 `HeroActionInputInjector`，使用主线程 tick 同步 `HeroActions` 与 `moveVector`
- [x] 已实现 `VoiceRuntimeController`，负责 settings 应用、后台启动/关闭、主线程队列消费
- [x] 已实现 `VoskVoiceRecognitionBackend`，采用 `WaveInEvent -> BlockingCollection<byte[]> -> VoskRecognizer`
- [x] 已添加 `HKVOICE_STUBS` 条件构建，以便无 `GameDir` 环境执行编译验证
- [x] 构建验证通过：`dotnet build HkVoiceMod/HkVoiceMod.csproj`
- [x] 打包验证通过：生成 `HkVoiceMod/artifacts/package/HkVoiceMod/`，包含 dll / pdb / README / 模型占位 README

## 6. Review Verdict
- Pending

## 7. Plan-Execution Diff
- N/A
