# PlasticSCM 工作流

本仓库的**开发节点以 PlasticSCM 历史为准**（考核【必须】项），GitHub 是代码镜像并承载 issue 跟踪。远程仓库挂在团结云开发（Unity 云开发），决策依据见 `docs/adr/0001-plasticscm-authoritative-history.md`。

## 环境

| 组件       | 位置 / 标识                                        |
| ---------- | -------------------------------------------------- |
| CLI        | `C:\Program Files\PlasticSCM5\client\cm.exe`       |
| 远程仓库   | `lingjing_2@tj_eafen405@cloud.cn`                  |
| 工作区     | `lingjing2` → 仓库根目录（本仓库目录）             |
| 客户端配置 | `%LOCALAPPDATA%\plastic4\client.conf`              |
| 云开发 App | devops.unity.cn/download，或从 Unity Hub 启动      |

`cm` 未加入 PATH，调用时用全路径，或把 `C:\Program Files\PlasticSCM5\client` 加进 PATH。

### 账号与额度

- 仓库归属 Unity ID `2027874080@qq.com`，changeset 的 owner 就是它。
- 工作区连上云仓库后 `cm` 直接可用，**不需要再配 server / 端口 / 工作模式**；登录态由云开发 App 维护。
- 免费版：20GB 存储、8 核构建机、3000 分钟；完成新手任务后 2TB、16 核、5000 分钟/月。个人项目够用。

### 换机器或协作

另一台机器装云开发 App，登录同一 Unity ID，在项目列表里选一个新目录作为工作区，`cm update` 拉全量历史即可。

### 本地旧仓库（历史备份）

本机还留着连接云端之前的本地仓库 `lingjing-2@localhost:8084`（本地 `plasticd` 服务 + 节点 0–3 的文档提交）。云端仓库是新建的，从**节点 0 重新起基线**，两套编号不连续，旧仓库只作备份、不再写入：

```powershell
Get-Service 'Plastic Server 6'   # 本地服务状态
plasticd --stop                  # 需要管理员权限
```

## 日常命令

| 目的     | 命令                                        |
| -------- | ------------------------------------------- |
| 看改动   | `cm status`                                 |
| 加文件   | `cm add -R "<路径>"`                        |
| 提交     | `cm checkin "<路径>" -c="<提交说明>"`        |
| 看历史   | `cm log --dateformat=yyyy-MM-dd`            |
| 看某文件历史 | `cm history "<路径>"`                    |
| 更新     | `cm update`                                 |
| 撤销改动 | `cm undo "<路径>"`                          |
| 差异     | `cm diff`                                   |
| 分支     | `cm branch list` / `cm switch "<分支>"`     |
| 工作区   | `cm workspace list`                         |

提交直接进云仓库，工作区不必再连本地服务端。

**已知坑 1**：`cm add <目录>` 默认**不递归**，目录会变成 Added 而子文件仍是 Private。加 `-R`（`cm add -R .`），或对每个叶子文件单独 add。

**已知坑 2**：`cm changeset delete <n>` 会把该 changeset 新增的文件**从工作区一并删除**（回滚到该节点之前的状态）。删之前先确认另一套版本控制里有副本，否则用 `git restore --worktree .` 找回。

**已知坑 3**：`ignore.conf` 必须存在于工作区根目录才生效；文件缺失时 `.git/` 之类会重新出现在待添加列表里。

## 忽略规则

忽略文件是工作区根目录下的 **`ignore.conf`**，不是 `.plasticignore`。语法与 `.gitignore` 有四处不同：

- 不支持 `[a-z]` 字符集区间
- 末尾斜杠只忽略目录**下的文件**，不忽略目录本身
- 以点开头的目录要同时写 `.name` 和 `.name/` 才能整个忽略
- **前导 `/` 锚定到项目根目录**；不加则匹配任意层级（`delivery/` 会连带忽略 `docs/delivery/`）

本仓库的 `ignore.conf` 排除了 Unity 生成物（`Library/`、`Temp/`、`obj/`…）、IDE 生成文件、构建产物，以及 **`.git/`**（Git 镜像的对象库不能进 Plastic）。根目录 `delivery/`（交付包）用 `/delivery` 锚定，`docs/delivery/`（交付文档）保持可跟踪。

## 提交约定（开发节点）

考核要求「保留正常开发提交记录及开发节点」。约定：

- **一个 changeset = 一个可验收的功能点**，不把多个不相干的改动塞进一次提交。
- 提交说明写明节点语义，前缀 `节点 N：` 加一句话描述，例如 `节点 3：飞盘数量道具生效，拾取后飞盘 +1`。
- 说明末尾带关联的 GitHub issue 号（`Issue #12`），编号空间与 changeset 号分离。
- 提交顺序固定：**先 `cm checkin`，再 `git commit` + `git push`**。顺序反了会出现 Git 领先于 Plastic 的节点缺失。
- 合并与发版用 `cm label` 打标签，节点记录以 `cm log` 的输出为准。

## 交付导出

考核交付物之一是「PlasticSCM 项目及开发节点记录」：

- **节点记录**：`cm log --xml --encoding=utf-8 > docs/delivery/plastic-changesets.xml`（人读版直接用 `cm log`）。
- **项目本体**：云仓库本身就是交付对象（验收方可直接在团结云组织里看历史）；需要离线副本时用 `cm fast-export lingjing_2@tj_eafen405@cloud.cn <file>` 导出 fast-export 格式。

## 与 GitHub 镜像的关系

GitHub 仓 `eafen405/lingjing-2` 承载镜像代码与 GitHub Issues（技能 tracker，见 `docs/agents/issue-tracker.md`）。**Git 提交不作为开发节点记录**。两套历史需要人工保持一致，代价与理由记在 ADR-0001。
