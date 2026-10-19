# GitHub 访问问题排查

> 结论先说：**改 hosts 只对「DNS 被污染」有效**。本机实测 DNS 解析到的是 GitHub 真实 IP（`20.205.243.x` / `185.199.108-111.x`），TCP 443、HTTPS、git smart HTTP 都返回 200，所以现在改 hosts 不会带来改善，反而可能因为 IP 轮换把原本正常的访问弄坏。

---

## 1. 先判断是哪一类问题

在 PowerShell 里执行：

```powershell
Resolve-DnsName github.com -Type A | Select-Object -ExpandProperty IPAddress
Test-NetConnection github.com -Port 443
curl.exe -I https://github.com
```

对照结果：

| 现象 | 原因 | 有效办法 |
| --- | --- | --- |
| DNS 返回 `0.0.0.0`、`127.0.0.1` 或奇怪 IP | **DNS 污染** | 改 hosts（本目录脚本）/ 换 DNS / DoH |
| DNS 正常，但 TCP 443 连不上 | 线路/防火墙/IP 被封 | 代理、SSH over 443、镜像 |
| TCP 通，`curl -I` 报 `SSL_ERROR_SYSCALL` / 连接重置 | **SNI 阻断** | 代理；hosts 无效 |
| `curl` 正常，只有浏览器打不开 | 浏览器代理 / 安全 DNS / 扩展 | 查浏览器设置，换 Edge 或无痕窗口 |

本机当前实测：DNS 正常、TCP 443 通、HTTPS 200 —— 属于「能访问」，不需要改 hosts。

---

## 2. 确实需要改 hosts 时

脚本已经准备好（需要管理员权限）：

- **应用**：右键 `tools\fix-github-hosts.cmd` →「以管理员身份运行」，或直接双击（会自动请求提权）；
- **撤销**：`tools\fix-github-hosts.cmd -Undo`。

脚本行为：

1. 先测 `https://github.com`，如果能返回 200 就直接退出，**不动 hosts**；
2. 否则从社区维护的镜像 `raw.hellogithub.com/hosts`（GitHub520 项目）拉取最新 IP；
3. 拉不到时退回 Cloudflare DoH 查询；
4. 备份原 hosts 到 `hosts.bak-时间戳`，用标记块写入，`ipconfig /flushdns`；
5. 撤销时只删除标记块，其他 hosts 内容不动。

手动效果类似：

```
# ==== GameLog GitHub hosts BEGIN ====
20.205.243.166  github.com
20.205.243.168  api.github.com
...
# ==== GameLog GitHub hosts END ====
```

> hosts 的局限：GitHub 的 IP 会变，写死后过一段时间可能失效；写错 IP 还可能出现证书错误。所以脚本默认「能通就不改」。

---

## 3. 比 hosts 更可靠的办法

### 3.1 有代理时（最推荐）

```bat
git config --global http.proxy  http://127.0.0.1:7890
git config --global https.proxy http://127.0.0.1:7890
```

取消：

```bat
git config --global --unset http.proxy
git config --global --unset https.proxy
```

### 3.2 SSH over 443（HTTPS 不稳时）

GitHub 的 SSH 也监听 443：

```
# %USERPROFILE%\.ssh\config
Host github.com
  HostName ssh.github.com
  Port 443
  User git
```

然后：

```bat
ssh -T git@github.com
git remote set-url origin git@github.com:你的用户名/GameLog.git
```

需要先把本机公钥加到 GitHub → Settings → SSH and GPG keys。

### 3.3 换 DNS / 开启 DoH

Windows 11：设置 → 网络和 Internet → 以太网/Wi-Fi → DNS 服务器分配 → 手动 → 首选 `1.1.1.1`、备用 `8.8.8.8`，并开启「通过 HTTPS 的 DNS」。

### 3.4 国内镜像（只用于拉取，不能 push）

```bat
git clone https://gitee.com/mirrors/xxx.git
```

或者把仓库同时镜像到 Gitee：

```bat
git remote add gitee https://gitee.com/你的用户名/GameLog.git
git push gitee main
```

> 注意：SignPath 要求 GitHub / GitLab / Bitbucket；Gitee 镜像只能作为备份，不能用于申请。

---

## 4. 什么时候可以放心用脚本

- `Resolve-DnsName github.com` 返回明显不对的 IP；
- 浏览器提示「无法访问此网站 / DNS_PROBE_FINISHED_NXDOMAIN」；
- 换网络（公司/校园网）后突然打不开。

脚本失败时不会破坏原 hosts（会先备份），撤销命令也一直可用。
