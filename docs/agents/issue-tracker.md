# Issue tracker: GitHub

本仓库的 issue 与规格说明存放在 GitHub Issues，所有操作通过 `gh` CLI 完成。

## 前置条件

- 仓库需有 GitHub remote（`git remote -v`），`gh` 据此推断目标仓库。
- `gh auth status` 显示已认证且具备 `repo` scope，issue 才能创建与打标。
- 五个 triage 标签必须已存在于仓库标签集；缺失的用 `gh label create "<name>" --description "<text>" --color "<hex>"` 补齐。

### 当前机器的环境差异

这台机器上有两套环境，`gh` 只在 WSL 侧是登录态：

- **WSL（Ubuntu，用户 eafen405）**：`gh` 已登录账号 eafen405，token scopes 含 `repo`；SSH key 在 `~/.ssh/id_ed25519`，已注册到 GitHub。**所有 `gh` 命令都从这里执行**：
  ```powershell
  wsl.exe -u eafen405 -- gh <子命令> --repo eafen405/lingjing-2
  ```
- **Windows 侧**：`gh` 的 keyring token 已失效（`gh auth status` 报 invalid），直接调用会失败。Git 推拉同样建议走 WSL（`wsl.exe -u eafen405 -- git -C /mnt/c/Users/user/Desktop/lingjing_2 push`），Windows 侧 `git` 只用来读状态与提交。

remote 已配成 SSH 形式：`git@github.com:eafen405/lingjing-2.git`。

## 与版本管理的关系

本仓库双轨版本管理：**PlasticSCM 是开发节点的权威记录**（考核【必须】项），GitHub 是公开镜像并承载 issue 跟踪。详见 `docs/agents/plastic-workflow.md`。

issue 编号只在 GitHub 侧使用。Plastic changeset 号与 GitHub issue 号是两套编号，**不要混用或互相引用为同一个 ID**；关联时写明来源，例如 `Issue #12` 指 GitHub issue，`changeset 123` 指 Plastic changeset。

## 约定

- **创建 issue**：`gh issue create --title "..." --body "..."`。多行正文用 heredoc。
- **读取 issue**：`gh issue view <number> --comments`，用 `jq` 过滤评论，同时取回标签。
- **列出 issue**：`gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'`，按需加 `--label` 与 `--state` 过滤。
- **评论**：`gh issue comment <number> --body "..."`。
- **打/摘标签**：`gh issue edit <number> --add-label "..."` / `--remove-label "..."`。
- **关闭**：`gh issue close <number> --comment "..."`。

在仓库目录内运行时 `gh` 会自动推断仓库，无需显式指定。

## PR 作为 triage 入口

**PRs as a request surface: no.** _(若本仓库把外部 PR 当作需求入口，改为 `yes`；`/triage` 读这个标志。)_

改为 `yes` 时，PR 与 issue 共用同一套标签和状态，用 `gh pr` 对应命令：

- **读取 PR**：`gh pr view <number> --comments`，差异用 `gh pr diff <number>`。
- **列出待 triage 的外部 PR**：`gh pr list --state open --json number,title,body,labels,author,authorAssociation,comments`，只保留 `authorAssociation` 为 `CONTRIBUTOR`、`FIRST_TIME_CONTRIBUTOR` 或 `NONE` 的（丢弃 `OWNER`/`MEMBER`/`COLLABORATOR`）。
- **评论 / 打标 / 关闭**：`gh pr comment`、`gh pr edit --add-label`/`--remove-label`、`gh pr close`。

GitHub 的 issue 与 PR 共用一个编号空间，裸 `#42` 两者皆可能是：先用 `gh pr view 42` 解析，失败再 `gh issue view 42`。

## 技能话术映射

- 当技能说 **"publish to the issue tracker"** → `gh issue create`。
- 当技能说 **"fetch the relevant ticket"** → `gh issue view <number> --comments`。

## Wayfinding 操作

供 `/wayfinder` 使用。**地图**是一个 issue，**子 issue**是票据。

- **地图**：单个打了 `wayfinder:map` 标签的 issue，正文承载 Notes / Decisions-so-far / Fog。`gh issue create --label wayfinder:map`。
- **子票据**：作为 GitHub sub-issue 挂在地图下的 issue（用 `gh api` 调用 sub-issues 端点）。若未启用 sub-issues，则把子项写进地图正文的任务列表，并在子项正文首行标注 `Part of #<map>`。标签：`wayfinder:<type>`（`research`/`prototype`/`grilling`/`task`）。认领后指派给主导开发者。
- **阻塞**：用 GitHub 原生 issue dependencies 作为唯一权威表示。加边用 `gh api --method POST repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`，其中 `<blocker-db-id>` 是阻塞项的数字 **database id**（`gh api repos/<owner>/<repo>/issues/<n> --jq .id`，**不是** `#number` 或 `node_id`）。GitHub 通过 `issue_dependencies_summary.blocked_by` 暴露开放中的阻塞项（实时闸门）。依赖不可用时，退回到子项正文首行写 `Blocked by: #<n>, #<n>`。所有阻塞项关闭后票据解除阻塞。
- **前沿查询**：列出地图的开放子项（`gh issue list --state open`，限定在地图的 sub-issues / 任务列表内），剔除有开放阻塞项（`issue_dependencies_summary.blocked_by > 0`，或 `Blocked by` 行里有开放 issue）或已有 assignee 的；按地图顺序取第一个。
- **认领**：`gh issue edit <n> --add-assignee @me`，这是会话的第一次写操作。
- **解决**：`gh issue comment <n> --body "<answer>"`，然后 `gh issue close <n>`，最后在地图的 Decisions-so-far 追加一条上下文指针。
