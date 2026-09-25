# 游戏工程边界

当前是架构目录，无可运行工程。先完成工具链验证，再按[制作切片](../docs/plan/制作切片与验收.md)建立项目。

- [simulation](simulation/README.md)：权威纯 C# 状态与规则。
- [content](content/README.md)：加载、定义解析、语义校验。
- [client](client/README.md)：Godot 宿主、输入、表现与平台适配。

依赖、线程和性能约束见[架构总览](../docs/architecture/架构总览.md)，契约与时序由相邻文档维护。当前不创建空类、假实现或未验证版本的工程文件。
