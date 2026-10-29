## 研发与审查

### 设计与复杂度控制

- **复用优先。** 先查现有实现，明确能力、缺口和最小改动；新增入口或角色不默认另建流程。
- **区分来源。** 分清用户要求、代码事实与模型建议。
- **结构按需。** 新增表、模块或机制须说明必要性及无法复用的原因；依据生命周期、查询、容量和并发选择结构，不为假设扩展过度设计。
- **权威唯一。** 复用规则与数据，避免重复逻辑和多份权威状态；快照及派生数据须明确来源与失效规则。
- **限制范围。** 局部需求不默认改造全局；超出原方案先说明必要性和更小替代方案，不静默扩展，也不对已授权的常规细节反复请示。
- **精简不减保障。** 保留必要的权限、隔离、事务、幂等、并发控制、失败恢复及操作溯源。设计与交付均检查影响范围和维护成本；测试通过或文档已确认不代表架构必要。
- **规模校准。** 本项目是个人项目、面向少量受邀用户：抽象与防御只在当下明确需求或已观测到的失败面前加入，并指名它服务哪一个需求或故障。必需的保障按「精简不减保障」完整保留。
- **插件理念。** 一切皆插件，符合 dsh 的理念，同时尽可能兼容其插件生态，让模块更灵活；深模块、窄接口是被推崇的。

## Agent skills

### Issue tracker

GitHub Issues via the `gh` CLI，本仓库的 issue 与规格说明都存放在 GitHub。See `docs/agents/issue-tracker.md`.

### Triage labels

五个默认标签：`needs-triage`、`needs-info`、`ready-for-agent`、`ready-for-human`、`wontfix`。See `docs/agents/triage-labels.md`.

### Domain docs

单上下文布局：根目录 `GLOSSARY.md` + `docs/adr/`。See `docs/agents/domain.md`.

### Version control

开发节点以 PlasticSCM 历史为准（考核【必须】项），GitHub 为代码镜像并承载 issue。See `docs/agents/plastic-workflow.md`.
