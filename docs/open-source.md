# 开源与代码签名指南

这个项目已经按开源仓库的结构整理好（MIT 协议、`.gitignore`、GitHub Actions 工作流）。下面是从「本地代码」到「别人下载不弹 SmartScreen」的完整路径。

---

## 1. 发布到 GitHub

本机没有安装 git 时，先装 [Git for Windows](https://git-scm.com/download/win) 或使用 GitHub Desktop。

```bat
cd /d D:\Tools\GameLog
git init
git add .
git commit -m "GameLog 1.0.0: 游戏时长记录器"
git branch -M main
git remote add origin https://github.com/<你的账号>/GameLog.git
git push -u origin main
```

注意事项：

- `.gitignore` 已排除 `dist/`、`GameLogData/`、`logs/`、证书等，**不要**把 `dist\GameLogData`（你的游戏记录）提交上去。
- 首次推送前建议扫一眼 `git status`，确认没有 `*.pfx`、`games.json` 之类文件。
- 仓库公开后，在 GitHub 上建一个 Release，附上 `dist\GameLog.exe` 和 `icon.ico`，作为「官方下载」。

---

## 2. 免费的合法签名（只对开源项目开放）

| 方案 | 费用 | 条件 | SmartScreen 表现 |
| --- | --- | --- | --- |
| **Certum Open Source Code Signing** | 免费（1 年，可续） | 公开仓库 + OSI 协议 + 项目可验证 | 正规 OV 证书；新文件仍可能提示，随下载量积累消失 |
| **SignPath Foundation** | 免费 | 开源项目，在 SignPath 平台构建并签名 | 同上；私钥不离开签名服务，更安全 |
| **Microsoft Store 个人开发者** | 免费（微软已取消个人注册费） | 打包 MSIX、过商店审核 | 商店签名，安装不弹 SmartScreen |
| 自签名证书 | 免费 | 无 | **无效**：只在本机受信任时有效，SmartScreen 不认 |
| Azure Trusted Signing | $9.99/月 | 需要 Azure 订阅 + 身份验证 | 微软云签名，效果接近正式证书 |

### Certum 开源证书申请材料清单

1. 公开仓库地址（GitHub/GitLab 均可）；
2. `LICENSE` 文件（本仓库已是 MIT）；
3. 项目英文简介（可直接用下面这段）：

   > GameLog is a small Windows desktop utility that adds a right-click "Add to GameLog" verb for .exe files, identifies the game via Baidu/Baidu Baike/Gamersky, downloads its cover, and tracks per-game play time by monitoring running processes. It is written in C# (WPF, .NET Framework 4.8) and builds with the compiler that ships with Windows.

4. 构建说明（README 的「从源码构建」一节 + `build.cmd`）；
5. 可下载的 Release 页面（证明项目真实存在、可构建）。

申请通过后，把证书导出为 `.pfx`（含私钥），**只保存在本机/CI secret**，不要提交到仓库。

### SignPath Foundation 申请要点

- 项目需使用 OSI 认可的开源协议；
- 在 <https://signpath.org/> 提交申请，说明项目用途、仓库地址、维护者身份；
- 通过后在 GitHub 安装 SignPath GitHub App，并配置签名策略；
- 签名在 SignPath 的服务器上完成，**私钥不会交给你**，因此比在 CI 里放 PFX 更安全。

---

## 3. 在 GitHub Actions 里签名

仓库已带 `.github/workflows/build.yml`：

- 每次 push / PR 自动用 Windows 自带的 `csc.exe` 编译；
- 如果配置了证书 secret，会自动用 `signtool` 签名并校验；
- 未配置时只产出未签名 exe（构建仍然成功）。

配置方法：

```text
仓库 → Settings → Secrets and variables → Actions → New repository secret

SIGNING_PFX_BASE64   = base64 编码后的 .pfx 内容
SIGNING_PFX_PASSWORD = .pfx 的密码
```

本机生成 base64：

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("D:\certs\gamelog.pfx")) | Set-Clipboard
```

> 安全提示：PFX 放在 CI secret 里意味着 CI 能拿到私钥。对开源项目，优先选 SignPath（私钥留在签名服务）或 Azure Trusted Signing（密钥在 Key Vault）。若使用 Certum 证书，至少开启仓库的 branch protection，避免他人改 workflow 窃取 secret。

---

## 4. 本机签名（可选）

### 4.1 本机自签名（只对本机有效）

仓库里带了一套本地自签名脚本，用于在等待 SignPath 审核期间消除本机的 SmartScreen 弹窗：

```bat
tools\sign-local.cmd        :: 给 dist\GameLog.exe 签名
tools\verify-signature.cmd  :: 查看签名状态
tools\remove-local-cert.cmd :: 从本机信任存储移除证书（-All 同时删除私钥）
```

- 证书：`CN=GameLog, O=GameLog`，保存在 `Cert:\CurrentUser\My`，有效期 5 年；
- 信任：写入 `CurrentUser\Root` 与 `CurrentUser\TrustedPublisher`；
- 公钥证书导出在 `dist\GameLog-CodeSigning.cer`，可以发给别人安装到「受信任的根证书颁发机构 + 受信任的发布者」；
- **只在当前用户/当前电脑有效**，换台机器仍然会提示；私钥不要外传。

`build.cmd` 会覆盖 `dist\GameLog.exe`，所以重新编译后要再跑一次 `tools\sign-local.cmd`；也可以：

```bat
set SIGN_LOCAL=1
build.cmd
```

### 4.2 用 CA 证书签名

拿到证书后，本机也可以用 signtool 签：

```bat
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" sign ^
  /fd SHA256 /f D:\certs\gamelog.pfx /p <密码> ^
  /tr http://timestamp.digicert.com /td SHA256 ^
  dist\GameLog.exe
```

验证：

```bat
signtool verify /pa /v dist\GameLog.exe
```

在 `build.cmd` 里自动签名可以加：

```bat
if exist "%SIGN_PFX%" (
  signtool sign /fd SHA256 /f "%SIGN_PFX%" /p "%SIGN_PFX_PASSWORD%" ^
    /tr http://timestamp.digicert.com /td SHA256 dist\GameLog.exe
)
```

---

## 5. 关于 SmartScreen 的预期

- **EV 证书**：签名后立即建立声誉，新文件也不弹；
- **OV / 开源证书**：签名有效，但新文件仍可能被提示，需要一定下载量；
- **未签名**：每次新哈希都可能提示，点「更多信息 → 仍要运行」即可；
- 无论哪种证书，都不要在代码或仓库里放私钥。

---

## 6. 合规提醒

- 只抓取百度、百度百科、游民星空的公开页面用于个人记录，遵守对方 robots/使用条款，不要高频请求；
- 游戏名称、封面版权归原厂商与游民星空所有，本项目不打包、不再分发这些内容；
- 仓库里不要放任何用户数据（游戏列表、路径、日志）。
