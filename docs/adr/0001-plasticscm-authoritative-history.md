# 以 PlasticSCM 作为开发节点的权威记录，GitHub 作为公开镜像

考核要求「保留正常开发提交记录及开发节点」为必须项，而 GitHub 仓库同时承载 issue 与对外镜像。我们决定：**开发节点以 PlasticSCM（Unity 云开发托管）历史为准，GitHub 只作代码镜像并承载 issue 跟踪**；提交顺序固定为「先 `cm checkin`，再 `git commit` + `git push`」。玩法设定不放在本 ADR，见 `docs/design/game-design.md`。

## Considered Options

- **纯 Git**：工具链最省，但考核的节点记录是 PlasticSCM 项，且 Git 看不到 Unity 资产的工作区状态。
- **纯 PlasticSCM**：节点记录完整，但失掉 GitHub 的 issue 流程与公开镜像。
- **双轨（选定）**：PlasticSCM 记节点，Git 镜像代码并承载 issue。代价是两套历史要人工保持一致。

## Consequences

- Git 提交**不作为**开发节点记录；节点一律以 `cm log` 为准。
- 每完成一个可验收的功能点：先 `cm checkin`，再 `git commit` + `git push`。顺序反了会出现 Git 领先于 Plastic 的节点缺失。
- 交付要导出节点记录：`cm log --xml --encoding=utf-8 > docs/delivery/plastic-changesets.xml`。
- 仓库中存在两套历史，合并、发版以 `cm label` 为准，Git 标签只作镜像。
