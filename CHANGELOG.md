# 更新日志

本项目的所有重要变更都会记录在此文件。

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]

暂无。

## [1.0.0] - 2026-11-17

首个公开版本。

### 新增

- **双模式诊断**：「验证失败（卡验证）」与「加载时间过长（一直转圈）」两种问题类型，切换后自动重新诊断。
- **8 项基础检测**：系统时间偏差、DNS 解析、Cloudflare 验证域名连通性、hosts 屏蔽、系统代理、浏览器扩展（广告拦截 / 隐私 / 代理类）、残留 Cloudflare 验证 Cookie、浏览器隐私设置。
- **慢加载附加检测（+3）**：网络延迟与丢包、TCP/DNS 耗时、Cloudflare 服务状态、浏览器版本。
- **结论排序**：检测结果按可能性排序列出「大概率问题」，每条附依据与修复建议；检测耗时约数十秒。
- **一键修复（全部可逆）**：清理 Cloudflare 验证 Cookie（`cf_*`、`cf_clearance`、`__cf_bm` 及 Cloudflare 域 Cookie）、刷新 DNS 缓存、校准系统时间、修复 hosts 屏蔽、关闭失效系统代理。拦截类扩展只提示、不自动禁用。
- **修复报告**：生成 `修复报告_时间.txt`（验证失败）或 `加载慢诊断报告_时间.txt`（加载过久），包含发现的问题、执行的动作与下一步建议。
- **诊断日志**：每次检测后自动保存 `diagnostic_时间.txt`；通过程序内反馈时自动附带（不含账号与 Cookie 内容）。
- **安全模式 `--safe`**：只诊断、不修改任何系统设置，便于先验证程序行为或在不希望改动系统的环境中使用。
- **赞助 / 反馈窗口**：收款码内置且支持外部图片替换，赞助文案可编辑（`sponsor.txt` / `sponsor_en.txt`）；反馈通过 SMTP 直发作者邮箱 `3814830710@qq.com`，未配置 SMTP 时保存到本地并复制到剪贴板。
- **像素风 UI**：星露谷（Stardew Valley）风格，无系统标题栏、自绘像素控件、内置 Zpix 字体。
- **中英双语**：主界面底部「EN / 中文」按钮切换，切换后自动重启生效，语言记录在 `settings.ini`。
- **命令行参数**：`--mode slow`、`--safe`、`--autofix`、`--close-browser`、`--no-splash`。
- **发布包**：`CfValidateFixer_v1.0.0_win64.zip`，含 `CfValidateFixer.exe`（自包含单文件）、`e_sqlite3.dll`、`CfValidateFixer.cer`、`SHA256SUMS.txt`、`sponsor.txt`、`sponsor_en.txt`、`feedback.config`、`SECURITY.md`。
- **代码签名**：支持 Authenticode 签名脚本，当前发布版为自签名证书（`CN=Huobaolajiaoji`），签名脚本见 `sign.ps1`。

### 修复

- 初始版本，暂无历史缺陷修复记录。
- 随首发内置的可逆修复流程：
  - hosts 中 `challenges.cloudflare.com` 等屏蔽条目在注释前先备份原文件；
  - 系统时间偏差超过 5 分钟时执行 `w32tm /resync`；
  - 刷新 DNS 缓存执行 `ipconfig /flushdns`；
  - 仅当系统代理端口不可达时才关闭失效代理，不修改代理软件配置；
  - Cookie 数据库删除前先复制备份，删除失败时不影响原文件。
- 反馈发送失败时自动回退为「保存本地文件 + 复制到剪贴板」，避免内容丢失。

### 安全

- 只读取 Cookie 的数量与名称，不读取、不上传 Cookie 值；不读取账号密码、浏览历史、书签。
- 修改 Cookie 数据库与 hosts 前自动备份到 `backup/时间戳/`，可手动回滚。
- 不修改 DNS 服务器地址、不修改代理软件配置、不改路由与防火墙；不安装驱动、不注入进程、不常驻后台、不混淆、不加壳。
- `--safe` 模式下不修改任何系统设置，仅在程序目录生成诊断日志。
- 联网范围限定：`challenges.cloudflare.com`（检测与延迟探测）、`cloudflarestatus.com`（服务状态）、以及用户配置的 SMTP 服务器（仅发送反馈时）。
- 许可证为 GPLv3 + §7 署名保护附加条款；发布包附 `SHA256SUMS.txt` 供校验完整性。
