# 贡献约定

先读[文档导航](docs/README.md)和[代理约定](AGENTS.md)。当前设计以用户提供的 v1.0 基线为准，旧规则已归档；目录骨架不表示实现完成。

围绕明确任务建短期分支，通过 PR 说明具体结果、范围与实际验证。保留他人修改，只暂存本次文件。规则变更同步当前章、架构、内容定义与验收；不可默默编辑原始 PDF/DOCX 伪造来源，新修订另行登记版本与理由。

实际规则测试、引擎运行与 Windows 导出命令见 [DEMO A](DEMO-A.md)。文档运行 `python tools/validate_docs.py` 和 `git diff --check`。29 项 A 检查不代表 B–F 的规划夹具已通过。

文本 UTF-8、LF、末尾换行，遵循 .editorconfig/.gitattributes。内容稳定 ID 和显示名分开；资产保存来源及适用许可。缓存、输出、凭据、参考游戏和下载资源包不进入构建前置。
