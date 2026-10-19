# 贡献指南

感谢你愿意改进 GameLog。项目很小，规则也很简单。

## 开发环境

- Windows 10/11，自带 .NET Framework 4.8；
- **不需要** Visual Studio / .NET SDK / Node / Python；
- 编译：`build.cmd`，产物在 `dist\GameLog.exe`。

```bat
git clone https://github.com/<owner>/GameLog.git
cd GameLog
build.cmd
```

## 代码结构

| 文件 | 作用 |
| --- | --- |
| `src/Program.cs` | 入口、命令行参数、自检/渲染模式 |
| `src/App.cs` | 生命周期、托盘、单实例、数据目录 |
| `src/MainWindow.cs` | 主界面（卡片墙、拖拽、搜索、菜单条） |
| `src/GameCard.cs` | 单个游戏卡片 |
| `src/IdentifyDialog.cs` | 识别确认窗口 |
| `src/SettingsWindow.cs` | 设置窗口 |
| `src/Identifier.cs` | 关键词提取与多来源识别打分 |
| `src/Baidu.cs` / `src/Gamersky.cs` / `src/WebSource.cs` | 各来源解析 |
| `src/Net.cs` | HTTP 封装（TLS1.2、gzip、编码探测） |
| `src/Store.cs` | 数据持久化（JSON） |
| `src/Monitor.cs` | 进程监控与计时 |
| `src/ShellMenu.cs` | 右键菜单、桌面快捷方式、解除锁定 |
| `src/Ui.cs` | 深色主题与控件工厂 |

## 提交前自检

```bat
build.cmd
dist\GameLog.exe --selftest "D:\Games\SomeGame\game.exe" --data .selftest
type .selftest\logs\selftest.txt
```

`--selftest` 会真实联网，把识别过程与候选写入日志，方便对比改动效果。

## 识别规则的改动

`Identifier.cs` 里的打分直接影响准确率，改完请至少在 `--selftest` 里覆盖：

- 英文 exe 名（`eldenring.exe`）
- 中文文件夹（`荒野大镖客2\RDR2.exe`）
- 启动器子目录（`...\Binaries\Win64\xxx-Win64-Shipping.exe`）
- 游民星空未收录的游戏（会走百度百科/网页兜底）
- 非游戏 exe（应当识别失败，不能硬凑）

## 提交规范

- 一个 PR 只做一件事；
- 提交信息用「动词 + 对象」，例如 `fix: 右键菜单分隔线样式导致菜单空白`；
- 不要提交 `dist/`、`GameLogData/`、`logs/`、证书文件；
- UI 改动请附截图（可以用 `GameLog.exe --render out.png --demo` 生成）。

## 协议

提交代码即表示同意以 [MIT License](LICENSE) 授权。
