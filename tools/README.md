# 开发工具

提供 [DEMO 构建脚本](build-demo.ps1)（用法见 [DEMO A](../DEMO-A.md)）及[文档验证脚本](validate_docs.py)，使用 Python 标准库检查本地 Markdown 链接、基线哈希及章节覆盖：

```sh
python tools/validate_docs.py
```

未来按需要增加纯 .NET 场景运行器、内容校验、资产生成和打包；不依赖原游戏编辑器。工具链已实测；导出需安装与编辑器匹配的 Windows 模板。
