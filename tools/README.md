# 开发工具

当前仅提供[文档验证脚本](validate_docs.py)，使用 Python 标准库检查本地 Markdown 链接、基线哈希及章节覆盖：

```sh
python tools/validate_docs.py
```

未来按需要增加纯 .NET 场景运行器、内容校验、资产生成和打包；不依赖原游戏编辑器。引擎/SDK 版本待实际兼容验证，尚无游戏构建命令。
