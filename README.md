# Discrete Tower Defense · 离散塔防

二维俯视、方格建造、连续位置战斗的阵地生存塔防。工业与神秘共享设施风险，通过生产、空间、供能、维护、仪式与敌群组织形成持续运营。

**当前交付：可运行 Windows DEMO A.5。** 已实现 32×20 战场、两个宽出生面、分队推进、建筑绕行与拆墙突破，以及核心、机炮、三夜交战与重开。Godot 4.7.2 .NET＋纯 C# Simulation，.NET SDK 10.0.401。游玩、构建、范围与验证见 [DEMO A](DEMO-A.md)。

## 阅读入口

- [文档导航与依据优先级](docs/README.md)
- [设计基线 v1.0 原文与页码索引](docs/baseline/v1.0/README.md)
- [设计摘要](docs/design/设计基线.md)与[内容库登记](docs/design/内容库.md)
- [架构总览](docs/architecture/架构总览.md)、[状态与契约](docs/architecture/状态与契约.md)、[时序与结算](docs/architecture/时序与结算.md)
- [制作切片与验收](docs/plan/制作切片与验收.md)
- [冲突迁移与待定项](docs/decisions/0002-设计基线迁移.md)
- [贡献约定](CONTRIBUTING.md)、[代理工作约定](AGENTS.md)

核心体验：前期决定把有限资源变成什么，后期决定怎样让已经建立的体系持续工作。

## 仓库边界

| 目录 | 职责 |
| --- | --- |
| docs/baseline | 用户提供的设计原件、校验值、检索副本 |
| docs/design、architecture、plan | 当前设计摘要、工程契约、制作与验证安排 |
| docs/archive | 已被取代的旧设计；不作为当前实现依据 |
| game/simulation | 无 Godot 依赖的权威规则核心，已实现 A 的固定步规则 |
| game/content | 内容加载和语义校验边界，已实现 DEMO JSON 加载与校验 |
| game/client | Godot 输入、视图、声音、平台适配边界 |
| content、art、tests、tools | 内容规范、资产规范、验证目录与开发工具 |

工具链与 A.5 空间重构已完成；先试玩验证阵地感，再进入 B 工业运营。首个对外实机至少达到切片 C 的身份展示要求；A 的内部交战原型不算完整产品展示。设计库不等于首个版本全部实现。

本项目独立实现，不依赖参考游戏、社群资源包或逆向结果。许可尚未选定。源 PDF 纳入版本记录不构成对外资产许可声明。
