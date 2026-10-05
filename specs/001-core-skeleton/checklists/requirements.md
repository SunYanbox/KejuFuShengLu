# Specification Quality Checklist: 解决方案骨架与家族领域模型

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-05
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- 校验结果：全部通过（1 轮，无返工）。
- 「无实现细节」一项说明：规格中出现的工程名、目标框架与单一解决方案文件格式，
  是规格书 §1、§2 与章程原则 I 强加的验收判据，已作为**需求**而非实现选择写入，
  并已在 Assumptions 中显式声明。除此之外规格不含类设计、API 签名、算法或数据结构。
- 「面向非技术干系人」一项说明：本特性是地基型特性，其直接干系人是项目所有者与
  后续阶段的开发者；因此用户故事按「档案是否完整可校验」「能否一条命令验证」
  「结果能否复现」组织，而非按界面流程组织。
- 无 [NEEDS CLARIFICATION] 标记：字段全集、官阶级数、状态集合、依赖方向与验收门禁均由
  规格书与章程给定。首轮校验时功名链也被判为「已给定」——该判断在 2026-10-05 的复审中
  被推翻，见下条。
- **2026-10-05 复审（发现规格缺失三处，均已按 §17 提问并裁决）**：
  ① **功名**必须表达为按时间排序的**变迁历史**。原为单值字段，无法承载 §7.4「降一级功名」
     的惩处——被连坐降级的成员历史末条会仍是「进士」，与实际功名矛盾，历史本身会变成假的。
     → 已回写规格书 §4.1/§6/§7.4/§12.2，spec FR-008，research R-14。
  ② **辈分**需分层（血亲出生即定 / 外来者由家族指定），且需新增**家主**。原模型既无辈分
     规则也无家主，`Family` 无法表达「娶入配偶与买来旁系的代数从何而来」。→ 新规格书 §4.4，
     spec FR-011，research R-15。家主继任**判定**属阶段⑧，001 只承载字段。
  ③ 「按角色的每月收支」不适合挂在 `Person` 上（角色永久归档会使其无界增长；买人口、聘礼、
     罚金、利息等支出根本无法归属到个人）。→ 定为 `GameState` 的家族级流水账，属阶段②，
     spec Out of Scope，research R-16。
  同次还新增规格书 **§9.5「买人口」**——该机制在规格书中**原本完全不存在**，属新设计，
  按章程「规格书是唯一真源」必须回写，不能只活在 data-model 里；并在 §5.4 补支付来源、
  在 §12.3 补统计口径。
- 家主继任细则原本留有**两处待裁决**（长子已亡而孙辈在世时孙辈是否优先于兄弟；「同辈分的
  兄弟」的范围）。二者已于 2026-10-05 由用户裁决并回写规格书 §4.4 与 research R-15：
  孙辈**优先于**家主的兄弟（直系不断则不走旁系），「同辈分的兄弟」= 家主的**亲兄弟**。
  001 仍**不实现**继任判定（依赖死亡推进，属阶段⑧），只承载家主属性与不变量。
- 同批经用户确认的推导项（原由 §17① 推导，现改为「已确认」）：买人口加成每年 1 月重置、
  买人口支出不得转贷款、买人口属性按 §4.2 无父母参照者初始化、买人口不设购买门槛、
  外来者辈分须在尚无子女时落定。
- 另一处待实测项：流水账在 100 年存档下的落盘体量（预计约数万条），阶段④ 落盘时决定是否
  按年分块（research R-16 的 Cost 段）。
- 阶段间边界一项已裁决：规格书 §5.1「儿童 0~12 岁」与 §4.3「男满 12 周岁成年」在
  12 岁男性上重叠，已按 §17 裁决（成年 = 男满 12 周岁 / 女满 14 周岁；未成年一律按
  儿童档）并回写规格书 §5.1「年龄档归属」，规格与规格书保持一致。
- **2026-10-05 跨工件一致性分析（第二轮，`$speckit-analyze`；15 项发现，0 CRITICAL，全部就地修补）**：
  六项**歧义**（A1~A6）、三项**欠定义**（U1~U3）、四项**不一致**（I1~I4）、两项**重复**（D1~D2）。
  修补要点与判据：
  - **A1（G-06 证据层级）**：原 G-06 说「程序集引用」，却要求由输入只有文本的纯函数
    `ArchitectureRules` 实现。已拆为 **G-06（源码级，纯函数承担）** 与 **G-06b（程序集级，
    守卫外壳直接断言）**，`GuardSelfTests` 用例③的期望编号固定为 **G-06**（不写「或」）。
  - **U1/U2（Edge Case 无断言）**：一夫一妻 + 丧偶再婚的断言补进 T026 与 `Family`
    不变量 ②（至多一个当前配偶；再婚先置空再入 `FormerSpouseIds`，子女父母引用不变）；
    四出身 × `HasShiStatus` 八种组合补进 T026。T026 原写的「覆盖全部 Edge Cases」改为
    逐条引用 spec 的三条，不再过度声明。
  - **U3（FR-015/SC-002 无验证物）**：G-07 的扫描范围显式扩到
    `tests/KFL.Tests/Core`、`Infrastructure`、`Fixtures`，`Architecture/` 显式豁免
    （R-13 的收敛由此物化）。
  - **A2（「可变」列被读成 setter）**：`Person` 的无写入通道成员改为**全集清单**（13 个），
    自持可写属性只有 8 个；T020 原来自相矛盾的「`Generation` 不在无写入通道之列」已改正。
  - **A3（FR-012 改档名不可验证）**：该子句在 001 内**无验证对象**（落盘属阶段④），
    已显式标注；T043 原来的「同一实例复用即 `Id` 不变」是同义反复，改为反射断言
    `GameState.Id` 无公开 setter。
  - **A5（两处留白被读成缺失）**：**开局成员辈分 = 0**（§4.4 原文，据此买来的旁系在开局
    家主治下为 1）；`Lifespan` **只有下界、上界属阶段⑧**，两处均写明「有意留白」。
  - **I2（跨故事依赖未登记）**：`US3 → T033` 是全阶段唯一跨故事依赖，已登记；
    US3 的 AS1 仍可独立验收，AS2 不可。
  - **I3（手工演练不可执行）**：本方案允许边集是**全序**，任何反向边都闭合成环；
    `using System.Windows.Media;` 在 `net10.0` 工程也必然编译失败。故反向依赖与平台泄漏
    的验证物**只有合成输入**，quickstart §2/§3 与 T035 据此重写，手工演练改用可编译的 G-02。
  - **D1（token 清单四处维护且已漂移）**：契约一 §2.1 定为**唯一真源**（14 个 token），
    其余三处只写引用、不复制副本。
  - 其余 A4（G-02 路径形式）、A6（UUID/`Guid` 用词）、I1（13→16 项抉择）、I4（类型计数）
    均为措辞与计数修正。
  - 分析报告另指出：章程「数值集中在 `GameConfig.cs` 等常量/配置类」一条与
    `KFL.Core/Config/AttributeLimits.cs` 的归属**不构成冲突**（原文「等」字已覆盖，且经
    所有者确认）。已记入 research R-06 作**遗留建议**，待章程下次实质性修订时顺手补一句，
    **不为此单独发一次版本**。
