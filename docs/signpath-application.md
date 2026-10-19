# SignPath Foundation 申请表（已被好）

申请表地址：<https://signpath.org/apply>（HubSpot 嵌入表单，需要浏览器打开）

提交前请确认：

- [ ] 仓库里已经有 `CODE_SIGNING.md` 和 `SECURITY.md`，并且已 push
- [ ] GitHub 账号已开启 2FA
- [ ] Release `v1.0.0` 已发布，包含 `GameLog.exe` 与 SHA-256
- [ ] 已准备好维护者姓名（表单会填）

---

## 字段填写参考

| 字段（以表单实际为准） | 建议填写 |
| --- | --- |
| Project name | `GameLog` |
| Project website / repository | `https://github.com/420337636/GameLog` |
| License | `MIT` |
| Primary language / platform | `C# / .NET Framework 4.8 (WPF), Windows` |
| Maintainer | `@420337636` |
| Contact email | `420337636@qq.com` |
| Country / time zone | `China / UTC+8` |
| Release URL | `https://github.com/420337636/GameLog/releases/tag/v1.0.0` |
| Artifacts to be signed | `GameLog.exe` (single-file WPF app, ~200 KB) |
| Build platform | `GitHub Actions, windows-latest, in-box csc.exe (no SDK)` |
| Already signed releases? | `No — this is the reason for applying` |
| Code signing policy | `https://github.com/420337636/GameLog/blob/main/CODE_SIGNING.md` |
| MFA enabled? | `Yes`（没开就先开，评审会看） |
| Commercial use | `No, no ads, no paid features, no revenue` |
| Telemetry / data collection | `None; all data stays in %APPDATA%\GameLog` |

## 项目简介（短）

```text
GameLog is a small open-source Windows utility (MIT). Right-click any .exe
(or drop it on the window) to add a game; it looks up the title and cover on
public Baidu / Gamersky pages and records play time by monitoring the process.
Single-file C#/WPF app built with the compiler that ships with Windows.
No telemetry, no account system, no commercial activity.
```

## 项目简介（长，表单允许时用）

```text
GameLog is a local game play-time tracker for Windows, written in C# (WPF,
.NET Framework 4.8) and released as a single ~200 KB executable. The user
right-clicks a game's .exe in Explorer (or drags it onto the window); GameLog
identifies the title and cover image from public Baidu / Baidu Baike / Gamersky
pages, then monitors the process to accumulate total, daily and per-session
play time. The library and settings are stored locally in %APPDATA%\GameLog
(portable mode available).

The project has no telemetry, no account system and no commercial activity.
It is built entirely from public source by a GitHub Actions workflow using the
C# compiler that ships with Windows, so the released binary is reproducible
without any downloaded dependency.
```

## Reputation（重点字段）

```text
GameLog is a new, fully open-source Windows utility (MIT) at
https://github.com/420337636/GameLog. It is a local game play-time tracker:
the user right-clicks any .exe (or drops it on the window), the app looks up
the game title and cover on public Baidu / Gamersky pages, and records play
time by monitoring the process. There is no telemetry, no account system and
no commercial activity.

Verifiable evidence available today:
- Public repository with complete source (21 C# files, ~250 KB) and a public
  GitHub Actions workflow that rebuilds the binary from source using the C#
  compiler that ships with Windows.
- Release v1.0.0 (2026-10-19) publishes GameLog.exe together with its SHA-256
  and release notes:
  https://github.com/420337636/GameLog/releases/tag/v1.0.0
- Code signing policy (CODE_SIGNING.md) and security policy (SECURITY.md) are
  part of the repository; MFA is enabled on GitHub and will be enabled on
  SignPath.
- Maintainer: @420337636, who develops the tool and uses it daily.

The project is young, so download and star counts are still small. We are
applying because Windows SmartScreen blocks the unsigned single-file binary
for every new user, even though it is fully reproducible from public source
and never sends user data anywhere.
```

短版（字段有字数限制时）：

```text
New MIT project: https://github.com/420337636/GameLog — public source,
CI-built release v1.0.0 with SHA-256, code signing and security policies in the
repo, maintainer @420337636, no telemetry or commercial use. Published
2026-10-19, so user numbers are still small; applying because SmartScreen
blocks the unsigned binary for every new user.
```

## 提交后

- 记下申请编号 / 提交时间，填到下面；
- 如果没有回音，2 周后可以用 `support@signpath.io` 礼貌询问；
- 被拒的常见理由是 reputation 不足，可以补用户量后重申，或转 Certum OSS。

## 提交记录

- 提交时间：2026-10-19（下午，UTC+8）
- 申请编号：_未提供（SignPath 回执邮件未带编号）_
- 状态：**已收到确认邮件，等待审核结果**（"Our team will get back to you within the next few business days"）
- 回执时间：2026-10-19
- 结果：_待填_

## 审核期间可以做的事

1. 把 `PRIVACY.md`、`CODE_SIGNING.md`、`SECURITY.md`、`CHANGELOG.md`、`docs/` 全部 push，确保申请里填的链接都能打开；
2. 在 Release 页面加上 SignPath 声明：
   `Free code signing provided by SignPath.io, certificate by SignPath Foundation.`
3. 让真实用户下载/试用、提 issue，积累 `download_count` 和社区讨论（Reputation 字段会看）；
4. 可选：提交 winget-pkgs 收录；
5. 保持每周有提交，审核期被抽查时仓库是活跃的。

## 如果被拒

- 常见原因是 "reputation"（项目太新、使用量太小）；
- 补 2–4 周的真实用户和发布记录后再申请；
- 或者改走 Certum Open Source Code Signing（免费 1 年，标准不同）。

