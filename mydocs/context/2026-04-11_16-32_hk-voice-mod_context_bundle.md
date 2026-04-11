# Context Bundle: hk-voice-mod

## 1. Source Index
- Requirement Source：用户口述需求与本轮补充说明
- Design Decision Source：同一轮中已确认的“Vosk 先验证、DS-CNN 后正式化”讨论结论
- Workspace Fact Source：空仓库扫描结果

## 2. Requirement Snapshot
- 游戏：`Hollow Knight`
- 目标：做一个声控 mod
- 命令词：`上 / 下 / 左 / 右 / 劈 / 跳 / 冲 / 吼 / 砸 / 波`
- 控制词：`停`
- mod 职责：只负责“识别出什么字 -> 发什么 HeroActions 键”，不做朝向、魂量、技能解锁等状态判断

## 3. Confirmed Constraints
- 必须跨说话人
- 无真实语料，最多使用高多样性 TTS 合成数据
- 优先体积小、延迟低、离线
- 适合 C# / Unity 集成

## 4. Confirmed Architecture Decisions
- 不推荐 DTW 作为主路线
- 推荐主路线：`DS-CNN / Tiny CNN KWS`
- 零训练验证兜底：`Vosk`
- 实施路径：`先 Vosk 验证 -> 再 DS-CNN 正式版`
- TTS 训练数据必须高多样性，否则模型容易只学到 TTS 引擎口音

## 5. Confirmed Mapping

### 5.1 方向键
- `上` -> `up`，按住 `0.5s` 后松开
- `下` -> `down`，按住 `0.5s` 后松开
- `左` -> `left`，持续长按，直到收到 `停`
- `右` -> `right`，持续长按，直到收到 `停`

### 5.2 动作键
- `劈` -> `attack`，短按一次
- `跳` -> `jump`，按住 `0.5s` 后松开
- `冲` -> `dash`，短按一次
- `吼` -> `up + cast`，短按一次
- `砸` -> `down + cast`，短按一次
- `波` -> `cast`，短按一次

### 5.3 控制词
- `停` -> 取消所有持续按键，仅针对 `左/右` 长按保持态

## 6. Inferred Execution Rules
- 收到 `左` 时，若当前 `右` 在持续按住，应先松开 `右` 再保持 `左`
- 收到 `右` 时，若当前 `左` 在持续按住，应先松开 `左` 再保持 `右`
- `上/下/跳` 的 0.5 秒动作可以与 `左/右` 的持续按住叠加
- `吼/砸` 属于同帧组合短按，不做额外状态检查

## 7. Acceptance Snapshot
- 识别线程与主线程完全隔离
- 主线程只通过队列消费识别结果
- 指令执行完全走查表架构
- 输入注入目标是 `HeroActions`
- 识别后端可替换，Vosk 只是验证版实现

## 8. Open Questions
- `短按一次` 的默认时长尚未由用户显式给出，建议默认 `80ms`
- Vosk 中文模型是否随 mod 分发，还是通过外部目录挂载
- 首版验证是否只面向 Windows 麦克风采集链路

## 9. Bootstrap Prompt Snapshot
```text
Hollow Knight 声控 mod，10 个中文单字命令词，上/下/左/右/劈/跳/冲/吼/砸/波。
mod 只负责喊什么字 -> 发什么键，不做状态检查。
当前已确认路线：先 Vosk 验证，后续可替换为 DS-CNN。
线程模型：录音/识别在子线程，结果进主线程队列消费。
输入注入：直接操作 HeroActions。
查表架构：VoiceCommand -> KeyActionProfile -> InputInjector。
最终映射：上/下 0.5 秒，左/右 持续直到“停”，跳 0.5 秒，劈/冲/吼/砸/波 为短按组合键，停只取消持续按键。
请先输出 SDD-RIPER SPEC，待确认后再写代码。
```
