# 内容数据

未来规则采用 UTF-8 JSON、显式 schemaVersion/单位/稳定 ID，由 Content 加载。当前包含 demo-a.json 实验场景和[标准内容库登记](../docs/design/内容库.md)，没有已平衡数值表；旧试跑数值已归档，不作为默认值。

后续按实际切片增加 facilities、abilities、upgrades、recipes、rituals、enemies、legions、anomalies、weather、maps、scenarios。定义/实例/表现映射分离；数值仅一处权威来源。新增行为使用强类型判别，不以名称猜机制。

地图独立保存实际高度、移动/建设/耗物许可和区域；配方输入与输出占格分离；能源能力可组合，三种攻击用能明确标记。全部约束见[状态与契约](../docs/architecture/状态与契约.md)。测试覆盖和 TestSupply 必须显式标记，不导出为真实展示场景。
