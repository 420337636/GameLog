# SignPath 接入步骤（批准后照做，约 30 分钟）

申请已于 2026-10-19 提交，等待审核。收到通过邮件后再执行本文。

仓库里已经准备好：

- `signpath/artifact-configuration.xml` — 只签 `GameLog.exe`
- `.github/workflows/signpath-release.yml` — 打 tag 时自动构建 → 提交签名 → 校验 → 发 Release
- `CODE_SIGNING.md` — 签名策略（审核会看）

---

## 1. 在 SignPath 里创建项目

登录 <https://app.signpath.io/>，按邮件指引加入组织后：

1. **Trusted Build Systems → GitHub.com**：加入组织，并链接到本项目；
2. 安装 [SignPath GitHub App](https://github.com/apps/signpath)，授权访问 `420337636/GameLog`；
3. **Projects → Create project**
   - Name：`GameLog`
   - Slug：`gamelog`（后面变量用这个）
4. **Artifact configurations → New**
   - Name：`windows-exe`
   - 把 `signpath/artifact-configuration.xml` 的内容粘进去（或上传一个 zip 让它自动生成）
5. **Signing policies → New**
   - Name：`release-signing`
   - 勾选“每次签名需要人工批准”（manual approval）
   - 允许的 artifact：上面那个 configuration

## 2. 创建 API Token

`Users → API tokens → Create`：

- 关联到你的用户
- 权限：submitter / 允许提交签名请求
- 复制 token（只显示一次）

## 3. 配置 GitHub 仓库的 Secrets 和 Variables

**Settings → Secrets and variables → Actions**

Secrets：

| 名称 | 值 |
| --- | --- |
| `SIGNPATH_API_TOKEN` | 第 2 步的 token |
| `SIGNPATH_ORGANIZATION_ID` | SignPath 组织 ID（组织设置页可复制） |

Variables（不是 secret）：

| 名称 | 值 |
| --- | --- |
| `SIGNPATH_ENABLED` | `true` |
| `SIGNPATH_PROJECT_SLUG` | `gamelog` |
| `SIGNPATH_SIGNING_POLICY_SLUG` | `release-signing` |
| `SIGNPATH_ARTIFACT_CONFIG_SLUG` | `windows-exe` |

> 只有 `SIGNPATH_ENABLED=true` 时 `signpath-release.yml` 才会执行；在此之前它不会影响现有构建。

## 4. 试签一次

```bat
git tag v1.0.1
git push origin v1.0.1
```

工作流会：构建 → 上传未签名 artifact → 提交签名请求 →（你在 SignPath 里点批准）→ 下载已签名文件 → `Get-AuthenticodeSignature` 校验 → 创建 Release。

第一次建议手动触发（Actions → signpath-release → Run workflow），确认流程后再打 tag。

## 5. 验证

```powershell
signtool verify /pa /v GameLog.exe
Get-AuthenticodeSignature .\GameLog.exe | Format-List Status, SignerCertificate
```

期望：`Status = Valid`，发布者为 **SignPath Foundation**。

## 6. 之后每次发版

1. 改代码，本地 `build.cmd` + `--selftest`；
2. `git tag vX.Y.Z` 并 push；
3. 在 SignPath 里批准这次签名请求；
4. 工作流自动发 Release。

## 常见问题

**工作流报 “artifact not found”**
`upload-artifact` 的 name 必须和提交时的 `github-artifact-id` 对应；本仓库已用 `${{ steps.upload.outputs.artifact-id }}` 自动传递。

**签名请求一直 pending**
SignPath 的 signing policy 要求人工批准，去 SignPath 网页点 Approve。

**签名后 SmartScreen 仍提示**
新证书需要一点下载量积累声誉（OV 级），一般几天到几周；EV 才会即时。可以先用「更多信息 → 仍要运行」过渡。

**想改签名文件范围**
改 `signpath/artifact-configuration.xml`，然后在 SignPath 里更新对应的 artifact configuration。
