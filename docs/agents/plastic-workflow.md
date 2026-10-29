# PlasticSCM 工作流

本仓库的**开发节点以 PlasticSCM 历史为准**（考核【必须】项），GitHub 是代码镜像并承载 issue 跟踪。决策依据见 `docs/adr/0001-plasticscm-authoritative-history.md`。

## 环境

| 组件     | 位置                                          |
| -------- | --------------------------------------------- |
| CLI      | `C:\Program Files\PlasticSCM5\client\cm.exe`  |
| 服务端   | `C:\Program Files\PlasticSCM5\server\plasticd.exe` |
| 客户端配置 | `%LOCALAPPDATA%\plastic4\client.conf`       |
| 仓库     | `lingjing-2@localhost:8084`                  |
| 工作区   | `lingjing2` → 仓库根目录（本仓库目录）      |

`cm` 未加入 PATH，调用时用全路径，或把 `C:\Program Files\PlasticSCM5\client` 加进 PATH。

### 服务端

服务端以进程方式运行（本机，无需云账号）：

```powershell
# 需要管理员权限：服务端要往 Program Files 下的 cloudconfig 写目录
plasticd --console --port=8087 --dbpath="$env:LOCALAPPDATA\plastic4\server"
```

**端口说明**：`--port=8087` 指定的是公开端口，服务端实际监听的是**私有端口 8084**（= 公开端口 - 3）。客户端要连的是 8084：

```powershell
cm configure --language=en --workingmode=UPWorkingMode --server=localhost --port=8084 --user=eafen405 --password=<本地口令>
cm checkconnection      # 期望输出：Test connection executed successfully
```

**为什么用 UPWorkingMode**：服务端是 `NameWorkingMode`，只认用户名、不校验口令。若客户端也用 NameWorkingMode，提交身份会取自 Windows 账号（本机是 `user`），changeset 的 owner 就会变成 `user`。改用 UPWorkingMode 后客户端发送 `eafen405`，服务端照单接受并自动建号，changeset 归属就对了。本地口令只存在 `%LOCALAPPDATA%\plastic4\client.conf`，不对外暴露。

服务端不随会话结束而停止。要长期运行可改用服务方式：`plasticd --installservice` / `--start`。

### 换成 Plastic Cloud（可选）

若后续拿到 plasticscm.com 账号与 Cloud 组织，仓库标识变成 `<repo>@<org>@cloud`，其余命令不变：

```powershell
cm configure --server=<org>@cloud --workingmode=SSOWorkingMode --user=<user>
cm repository create <repo>
```

## 日常命令

| 目的     | 命令                                        |
| -------- | ------------------------------------------- |
| 看改动   | `cm status`                                 |
| 加文件   | `cm add "<路径>"`                           |
| 提交     | `cm checkin "<路径>" -c="<提交说明>"`        |
| 看历史   | `cm log --dateformat=yyyy-MM-dd`            |
| 看某文件历史 | `cm history "<路径>"`                    |
| 更新     | `cm update`                                 |
| 撤销改动 | `cm undo "<路径>"`                          |
| 差异     | `cm diff`                                   |
| 分支     | `cm branch list` / `cm switch "<分支>"`     |
| 工作区   | `cm workspace list`                         |

**已知坑 1**：`cm add <目录>` 在本版本**不会递归添加子项**，目录会变成 Added 而子文件仍是 Private。目录加完后再逐个 add 子文件，或对每个叶子文件单独 add。

**已知坑 2**：`cm changeset delete <n>` 会把该 changeset 新增的文件**从工作区一并删除**（回滚到该节点之前的状态）。删之前先确认另一套版本控制里有副本，否则用 `git restore --worktree .` 找回。

**已知坑 3**：`ignore.conf` 必须存在于工作区根目录才生效；文件缺失时 `.git/` 之类会重新出现在待添加列表里。

## 忽略规则

忽略文件是工作区根目录下的 **`ignore.conf`**，不是 `.plasticignore`。语法与 `.gitignore` 有三处不同：

- 不支持 `[a-z]` 字符集区间
- 末尾斜杠只忽略目录**下的文件**，不忽略目录本身
- 以点开头的目录要同时写 `.name` 和 `.name/` 才能整个忽略

本仓库的 `ignore.conf` 排除了 Unity 生成物（`Library/`、`Temp/`、`obj/`…）、IDE 生成文件、构建产物，以及 **`.git/`**（Git 镜像的对象库不能进 Plastic）。

## 提交约定（开发节点）

考核要求「保留正常开发提交记录及开发节点」。约定：

- **一个 changeset = 一个可验收的功能点**，不把多个不相干的改动塞进一次提交。
- 提交说明写明节点语义，前缀 `节点 N：` 加一句话描述，例如 `节点 3：飞盘数量道具生效，拾取后飞盘 +1`。
- 说明末尾带关联的 GitHub issue 号（`Issue #12`），编号空间与 changeset 号分离。
- 提交顺序固定：**先 `cm checkin`，再 `git commit` + `git push`**。顺序反了会出现 Git 领先于 Plastic 的节点缺失。
- 合并与发版用 `cm label` 打标签，节点记录以 `cm log` 的输出为准。

## 交付导出

考核交付物之一是「PlasticSCM 项目及开发节点记录」。导出方式：

- **节点记录**：`cm log --xml --encoding=utf-8 > docs/delivery/plastic-changesets.xml`（人读版直接用 `cm log`）。
- **项目本体**：仓库数据在服务端 `dbpath` 下，工作区在本仓库目录；两者一起打包提交，或用 `cm fast-export` 导出为 fast-export 格式。

## 与 GitHub 镜像的关系

GitHub 仓 `eafen405/lingjing-2` 承载镜像代码与 GitHub Issues（技能 tracker，见 `docs/agents/issue-tracker.md`）。**Git 提交不作为开发节点记录**。两套历史需要人工保持一致，代价与理由记在 ADR-0001。
