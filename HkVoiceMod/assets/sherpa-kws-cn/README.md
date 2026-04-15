# Sherpa Keyword Spotter Assets

当前目录已经按可运行包所需整理好了 Sherpa-ONNX 中文 KWS 资源。

- 默认相对路径：`assets/sherpa-kws-cn`
- 上游模型：`sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01`
- 当前使用的是官方 `int8` 权重，并重命名为运行时代码使用的固定文件名：
  - `encoder.onnx`
  - `decoder.onnx`
  - `joiner.onnx`
  - `tokens.txt`
- `keywords_raw.txt` 是原始命令词表
- `keywords.txt` 是按 Sherpa `ppinyin` 规则整理后的可运行关键词文件
- `upstream-configuration.json` 保留了上游模型元信息

`dotnet build` 的打包步骤会把这个目录中的现有文件一并复制到 mod 产物目录。
