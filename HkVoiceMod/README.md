# HkVoiceMod

离线中文语音控制 Hollow Knight 的验证版 mod。当前版本固定为 `Vosk + Windows + NAudio`，目标是先跑通“识别 -> 主线程队列 -> HeroActions 注入”整链路。

## 构建

当前仓库支持两种构建模式：

1. 编译验证模式：未提供 `GameDir` 时启用 stub，适合在当前 Linux/CI 环境跑 `dotnet build`。
2. 真实 HK 构建模式：提供 `GameDir` 后链接 Hollow Knight / Modding API 程序集。

```bash
dotnet build HkVoiceMod/HkVoiceMod.csproj
dotnet build HkVoiceMod/HkVoiceMod.csproj -p:GameDir="C:\\Program Files (x86)\\Steam\\steamapps\\common\\Hollow Knight"
```

## 运行时依赖

- Windows 麦克风采集：`NAudio`
- 识别：`Vosk`
- 模型目录：默认按程序集相对路径解析 `assets/vosk-model-cn`

如果需要改成绝对路径，可在 global settings 中设置 `VoskModelPath`。

## 打包产物

每次 `dotnet build` 后会生成：

```text
HkVoiceMod/artifacts/package/HkVoiceMod/
├── HkVoiceMod.dll
├── HkVoiceMod.pdb
├── README.md
└── assets/vosk-model-cn/README.md
```

Vosk 中文模型本体不进仓库，也不进入默认打包目录。

## 模型放置

将下载好的 Vosk 中文模型目录内容放到：

```text
HkVoiceMod/artifacts/package/HkVoiceMod/assets/vosk-model-cn/
```

或者放到实际 mod 程序集同级的：

```text
assets/vosk-model-cn/
```

## 命令映射

- `上` -> `up`，保持 `0.5s`
- `下` -> `down`，保持 `0.5s`
- `左` -> `left`，持续直到 `停`
- `右` -> `right`，持续直到 `停`
- `劈` -> `attack`，短按 `80ms`
- `跳` -> `jump`，保持 `0.5s`
- `冲` -> `dash`，短按 `80ms`
- `吼` -> `up + cast`，短按 `80ms`
- `砸` -> `down + cast`，短按 `80ms`
- `波` -> `cast`，短按 `80ms`
- `停` -> 释放 `left/right` 的持续按住态
