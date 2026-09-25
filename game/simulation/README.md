# DiscreteTD.Simulation

已实现 A 规则程序集；World、NavigationField 和 SpatialIndex 承担 A.5 的宽正面分队、动态双导航场、碰撞推挤、双向挤压及最小交战闭环，后续模块按需拆分。仅依赖 .NET 基础库，不得引用 Godot。权威状态、命令验证、资源账目、伤害、生命期与技能排程都在这里。

模块为 World、Progression、Spatial、Construction、Navigation、Legions、Anomalies、Energy、Combat、Rituals、Pollution、Environment、Lifecycle、Diagnostics；按切片出现真实实现时创建子目录，不预建全部空模块。

阅读[架构总览](../../docs/architecture/架构总览.md)、[状态契约](../../docs/architecture/状态与契约.md)、[时序](../../docs/architecture/时序与结算.md)。实体、占格、渲染构件分离；T/M/P/V/A/C/Q 独立；伤害/特殊移除与生命周期原因明确。
