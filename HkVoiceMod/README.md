# HkVoiceMod

离线中文语音控制 Hollow Knight 的验证版 mod。当前版本固定为 `Sherpa-ONNX KeywordSpotter + Windows + NAudio`，目标是先跑通“识别 -> 后台线程队列 -> 主线程 HeroActions 注入”整链路。

## 构建

当前仓库现在只支持**真实 Hollow Knight 安装构建**。默认游戏目录已经指向本机：

```text
D:\SteamLibrary\steamapps\common\Hollow Knight
```

如果你的游戏装在别处，构建时覆盖 `GameDir` 即可。

```bash
dotnet build HkVoiceMod/HkVoiceMod.csproj
dotnet build HkVoiceMod/HkVoiceMod.csproj -p:GameDir="C:\\Program Files (x86)\\Steam\\steamapps\\common\\Hollow Knight"
```

构建前会强校验真实游戏 DLL 是否存在；找不到 `Assembly-CSharp.dll` / `Assembly-CSharp-firstpass.dll` 时会直接报错，而不会再回退到 stub。

## 运行时依赖

- Windows 麦克风采集：`NAudio`
- 识别：`SherpaOnnx.KeywordSpotter`
- 模型目录：默认按程序集相对路径解析 `assets/sherpa-kws-cn`

如果需要改成绝对路径，可在 global settings 中设置 `SherpaModelPath`。为防止长音重复触发，同一命令默认还会应用 `300ms` 的简单冷却去重。

当前仓库已经内置一套可直接运行的中文 Sherpa KWS 资源，来源于官方 `sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01` 模型，并整理为运行时固定文件名。

## 打包产物

每次 `dotnet build` 后会先生成：

```text
HkVoiceMod/artifacts/package/HkVoiceMod/
├── HkVoiceMod.dll
├── HkVoiceMod.pdb
├── sherpa-onnx.dll
├── NAudio*.dll
├── README.md
├── native/
│   ├── win-x64/
│   │   ├── onnxruntime.dll
│   │   └── sherpa-onnx-c-api.dll
│   └── win-x86/
│       ├── onnxruntime.dll
│       └── sherpa-onnx-c-api.dll
└── assets/sherpa-kws-cn/
    ├── README.md
    ├── keywords_raw.txt
    ├── keywords.txt
    ├── encoder.onnx
    ├── decoder.onnx
    ├── joiner.onnx
    ├── tokens.txt
    └── ...
```

当前工作区中的 Sherpa 关键词模型目录会随打包目录一起复制；托管依赖会放在 mod 根目录，Windows 原生依赖会放在 `native/win-x64` 和 `native/win-x86` 中，由运行时加载器按进程位数预加载 `onnxruntime.dll` 与 `sherpa-onnx-c-api.dll`。如果本地缺少模型文件，打包目录里也只会出现实际存在的内容。

构建完成后，整个打包目录还会自动同步到游戏安装目录：

```text
D:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Mods\HkVoiceMod\
```

这样构建完成后就可以直接打开游戏验证，无需再手动复制 mod 文件。

## 模型放置

如果本地还没有模型文件，将下载好的 Sherpa 中文关键词模型目录内容放到：

```text
HkVoiceMod/artifacts/package/HkVoiceMod/assets/sherpa-kws-cn/
```

或者放到实际 mod 程序集同级的：

```text
assets/sherpa-kws-cn/
```

目录内至少需要包含：`encoder.onnx`、`decoder.onnx`、`joiner.onnx`、`tokens.txt`、`keywords.txt`。仓库自带的 `keywords.txt` 默认列出了固定命令词：`往上 往下 往左 往右 攻击 跳跃 冲刺 上吼 下砸 放波 停止`。

## 命令映射

- `往上` -> `up`，保持 `0.5s`
- `往下` -> `down`，保持 `0.5s`
- `往左` -> `left`，持续直到 `停`
- `往右` -> `right`，持续直到 `停`
- `攻击` -> `attack`，短按 `80ms`
- `跳跃` -> `jump`，保持 `0.5s`
- `冲刺` -> `dash`，短按 `80ms`
- `上吼` -> `up + cast`，短按 `80ms`
- `下砸` -> `down + cast`，短按 `80ms`
- `放波` -> `cast`，短按 `80ms`
- `停止` -> 释放 `left/right` 的持续按住态
