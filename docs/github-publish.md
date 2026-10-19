# 发布到 GitHub 并申请免费签名

从「本地代码」到「公开仓库 + 免费代码签名」的完整步骤。仓库已经整理好，照着做即可。

---

## 第 1 步：在 GitHub 建一个空仓库

1. 打开 <https://github.com/new>；
2. **Repository name**：`GameLog`（大小写随意）；
3. 选 **Public**（免费签名要求公开）；
4. **不要**勾选 "Add a README file"、"Add .gitignore"、"Choose a license"——本地已经有这些文件；
5. 点 **Create repository**，复制页面上的 HTTPS 地址，形如：

   ```
   https://github.com/你的用户名/GameLog.git
   ```

> 如果不想暴露 QQ 邮箱：GitHub → Settings → Emails → 勾选 "Keep my email addresses private"，
> 它会给你一个 `数字+用户名@users.noreply.github.com` 地址，填到下面的 `publish.cmd` 里即可。

---

## 第 2 步：安装 Git

本机目前没有 git。任选其一：

- **Git for Windows**（推荐）：<https://git-scm.com/download/win>，一路默认安装即可；
- 或 **GitHub Desktop**：<https://desktop.github.com/>，登录 GitHub 后 "Add local repository" 选择 `D:\gameLog`，再点 "Publish repository"。

---

## 第 3 步：执行发布脚本

双击仓库根目录的 **`publish.cmd`**（这个文件是我给你准备好的），按提示：

1. 输入 GitHub 邮箱（直接回车用 `420337636@qq.com`）；
2. 输入显示名（回车用 `GameLog`）；
3. 粘贴第 1 步复制的仓库 URL；
4. 首次 `git push` 会弹出浏览器登录 GitHub，授权后自动完成。

脚本会执行：`git init` → `git add -A` → `git commit` → `git push -u origin main`。
`.gitignore` 已排除 `dist/`、`GameLogData/`、日志和证书，你的游戏记录不会上传。

也可以手动执行：

```bat
cd /d D:\gameLog
git init
git config user.email "420337636@qq.com"
git config user.name "GameLog"
git add -A
git commit -m "GameLog 1.0.0: Windows game playtime tracker"
git branch -M main
git remote add origin https://github.com/你的用户名/GameLog.git
git push -u origin main
```

---

## 第 4 步：检查仓库

打开 `https://github.com/你的用户名/GameLog`，应当看到：

```
.github/workflows/build.yml   src/   docs/   assets/
LICENSE   README.md   CONTRIBUTING.md   build.cmd   publish.cmd
```

并确认 **没有** `dist/`、`GameLogData/`、`*.pfx`。
仓库页面上方的 **Actions** 标签会自动跑一次构建（Windows 自带 `csc.exe`），几十秒后出现绿色对勾。

---

## 第 5 步：建 Release 作为“官方下载”

1. 仓库右侧 **Releases → Create a new release**；
2. Tag：`v1.0.0`，标题：`GameLog 1.0.0`；
3. 上传 `D:\gameLog\dist\GameLog.exe` 和 `D:\gameLog\dist\icon.ico`；
4. 说明里写清楚：

   > 未签名版本，首次运行如遇 SmartScreen 请点「更多信息 → 仍要运行」。

5. 发布。

> 免费签名申请通常要求“有公开可下载的发布版本”，这一步很关键。

---

## 第 6 步：申请免费开源签名

### 方案 A：SignPath Foundation（推荐，私钥不离开签名服务）

1. 打开 <https://signpath.org/> → **Apply for free signing**；
2. 填写：项目名 `GameLog`、仓库地址、许可证 MIT、维护者邮箱、项目用途；
3. 说明这是个人开源工具、无商业收入；
4. 通过后会收到 SignPath 账号，在 GitHub 安装 **SignPath GitHub App**，按邮件指引配置签名策略；
5. 以后在 CI 里调用 SignPath 的 Action 签名（私钥一直留在 SignPath）。

### 方案 B：Certum Open Source Code Signing（免费 1 年，给 .pfx）

1. 打开 <https://certum.eu/>，找 **Code Signing → Open Source Code Signing**；
2. 提交：公开仓库、MIT 许可证、项目英文简介、维护者身份；
3. 审核通过后签发证书（文件形式），导出 `.pfx`；
4. 把 `.pfx` 配置到 GitHub Secrets（见第 7 步）。

### 方案 C：Microsoft Store 个人开发者（免费账号）

1. <https://partner.microsoft.com/dashboard> 注册个人开发者（现已免费）；
2. 用 MSIX Packaging Tool 打包（需要把 exe 包成 MSIX）；
3. 上架审核通过后由商店签名分发，用户安装不弹 SmartScreen。

### 申请信模板（英文，可直接改）

```text
Subject: Open source code signing request - GameLog

Hello,

I maintain GameLog, a small open-source Windows utility (MIT license):
https://github.com/<your-name>/GameLog

GameLog adds a right-click "Add to GameLog" verb for .exe files, identifies
the game via Baidu / Baidu Baike / Gamersky, downloads a cover, and tracks
per-game play time by monitoring running processes. It is written in C#
(WPF, .NET Framework 4.8) and builds with the compiler that ships with
Windows (no SDK, no third-party runtime).

The project has no commercial activity and no revenue. The unsigned binary
currently triggers SmartScreen for every new user, which is the reason I am
applying for a free open-source code signing certificate.

Repository: https://github.com/<your-name>/GameLog
License: MIT
Releases: https://github.com/<your-name>/GameLog/releases
Maintainer: <your name>, <your email>

Thank you for supporting open source.

Best regards,
<your name>
```

---

## 第 7 步：把签名接进 CI（拿到证书后）

仓库里的 `.github/workflows/build.yml` 已经写好签名步骤，只需配置两个 Secret：

```
GitHub 仓库 → Settings → Secrets and variables → Actions → New repository secret

SIGNING_PFX_BASE64     = base64 编码的 .pfx
SIGNING_PFX_PASSWORD   = .pfx 密码
```

本机生成 base64：

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("D:\certs\gamelog.pfx")) | Set-Clipboard
```

之后每次 push / 打 tag，CI 会自动编译、签名、校验并上传产物。
用 SignPath 的话，则改为调用 SignPath 的 GitHub Action，PFX 不需要放进仓库。

---

## 第 8 步：之后的发版流程

```bat
rem 改代码
build.cmd
rem 本地自测
dist\GameLog.exe --selftest "D:\Games\SomeGame\game.exe" --data .selftest

git add -A
git commit -m "feat: ..."
git tag v1.0.1
git push
git push --tags
```

在 GitHub 上基于新 tag 建 Release，附上 `dist\GameLog.exe`。
有证书后 CI 产出的就是已签名版本。

---

## 常见问题

**push 时提示权限被拒？**
首次 push 会弹 GitHub 登录窗口；若没有弹，检查是否装了 Git Credential Manager（Git for Windows 默认带）。

**想改协议？**
把 `LICENSE` 换成 Apache-2.0 / GPL 等，并同步改 README 里的 badge 和 `docs/open-source.md`。

**仓库里不小心提交了 dist/ 或数据？**
```bat
git rm -r --cached dist GameLogData
git commit -m "chore: stop tracking build output"
git push
```
如果已经推到公开仓库，还需要清理历史（`git filter-repo`），或者删库重建。
