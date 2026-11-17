# 常见问题（FAQ）

适用于 **Cloudflare 修复工坊 v1.0.0**（作者：火爆辣椒鸡）。更多说明见 [README.md](README.md)、[SECURITY.md](SECURITY.md) 与 [PRIVACY.md](PRIVACY.md)。

## 1. 被杀毒软件 / Windows SmartScreen 拦截了怎么办？

这是**误报**，通常因为程序是新的、未购买正式代码签名证书，且会读取浏览器 Cookie 数据库、修改 hosts（虽然只读计数、改动前备份）。处理顺序：

1. **SmartScreen**：点「更多信息」→「仍要运行」。
2. **杀毒软件弹窗**：选择「允许 / 信任 / 加入白名单」，不要直接删除文件。
3. **核对哈希**：用 `Get-FileHash .\CfValidateFixer.exe -Algorithm SHA256` 或 `certutil -hashfile CfValidateFixer.exe SHA256` 与发布包 `SHA256SUMS.txt` 比对，确认文件未被篡改。
4. **先零修改验证**：用 `CfValidateFixer.exe --safe` 只诊断、不改系统，确认行为符合预期后再用完整功能。
5. **提交误报申诉**：申诉文本与入口见 [docs/误报申诉模板.md](docs/误报申诉模板.md)。
6. **根本解决**：作者后续购买 OV/EV 代码签名证书后，SmartScreen 与杀软信誉会显著改善。

## 2. 为什么修复前要关闭浏览器？

浏览器 Cookie 本质是 SQLite 数据库。浏览器运行时会占用该文件并缓存 Cookie，直接删除可能失败，或被浏览器在退出时写回，导致修复不生效。程序会先提示你关闭浏览器再执行清理；使用 `--autofix` 时默认**不**自动关闭浏览器，需要显式加 `--close-browser` 才会自动关闭。清理前会先复制一份 Cookie 数据库到备份目录。

## 3. 为什么需要管理员权限？没有管理员权限会怎样？

以下修复需要管理员权限：

- 修改 `C:\Windows\System32\drivers\etc\hosts`
- 执行 `w32tm /resync` 校准系统时间

没有管理员权限时，这些项目会**自动跳过并提示**，不会强行修改；清理 Cookie、刷新 DNS 等通常仍可执行。建议右键 exe →「以管理员身份运行」，或在弹窗中选择「是」。

## 4. 验证失败一直过不去怎么办？

按以下顺序排查（程序报告与诊断日志里会标出对应项）：

1. **扩展**：在 AdGuard、uBlock 等扩展里对当前站点「暂停过滤 / 加白名单」，或临时禁用后刷新。
2. **第三方 Cookie / 隐私设置**：允许第三方 Cookie，跟踪防护调至「平衡」，临时关闭增强安全模式。
3. **残留 Cookie**：执行「一键修复」清理 Cloudflare 验证 Cookie，或换无痕窗口对比。
4. **系统时间 / hosts / 代理**：按诊断结果处理；代理或 VPN 可尝试关闭直连、换节点。
5. **确认不是站点侧问题**：打开 <https://www.cloudflarestatus.com/> 看 Cloudflare 是否故障；换设备、换网络对比。
6. **仍不行**：可能是网站 sitekey 配置错误或账号风控，需联系网站客服。可把 `diagnostic_*.txt` 通过程序内「问题反馈」发给作者。

## 5. 如何切换语言？

主界面底部点「**EN / 中文**」按钮，确认后程序会自动重启并应用新语言。也可直接编辑程序目录下的 `settings.ini`：

```ini
language=zh
```

`zh` 为中文，`en` 为英文；保存后重启程序生效。

## 6. 如何替换收款码和赞助文案？

把以下文件放到 **exe 同目录**（同名覆盖即可，程序优先读取外部文件）：

| 文件 | 作用 |
|---|---|
| `sponsor_wechat.png` | 微信收款码 |
| `sponsor_alipay.png` | 支付宝收款码 |
| `sponsor.txt` | 中文赞助文案（UTF-8） |
| `sponsor_en.txt` | 英文赞助文案（UTF-8，英文界面使用） |

图片建议使用正方形 PNG；文案用 UTF-8 编码保存，修改后重启程序生效。发布包内已附 `sponsor.txt` / `sponsor_en.txt` 可直接编辑。

## 7. 如何配置反馈邮箱？

编辑 exe 同目录的 `feedback.config`（UTF-8），填写：

```ini
email=你的QQ邮箱@qq.com
smtp_host=smtp.qq.com
smtp_port=587
smtp_auth=SMTP授权码（16位，不是QQ密码）
```

- 在 QQ 邮箱「设置 → 账户 → POP3/SMTP 服务」开启服务并生成授权码。
- 保存后重启程序，反馈即可直发到 `email` 指定的收件邮箱。
- 未配置或发送失败时，反馈会保存为本地 `feedback_*.txt` 并复制到剪贴板，可手动发送。
- `smtp_auth` 属于敏感凭据，请勿公开或提交到仓库；发布包已预填作者收件邮箱，普通用户无需修改。

## 8. 安全模式是什么？

安全模式（`--safe`）**只诊断、不修改任何系统设置**：不清理 Cookie、不改 hosts、不刷新 DNS、不校时、不动代理。适合：

- 首次使用、想先确认程序行为；
- 杀毒软件敏感或公司规定不允许改动系统的环境；
- 只想拿到诊断结论再决定是否手动处理。

诊断日志仍会生成在程序目录。用法：

```text
CfValidateFixer.exe --safe
```

## 9. 备份在哪里？如何恢复？

**位置**（按程序权限依次尝试，报告里会写明实际路径）：

1. 程序目录 `backup\<时间戳>\`
2. `%TEMP%\CfValidateFixer\backup\<时间戳>\`
3. 桌面 `CfValidateFixer_backup\<时间戳>\`

备份内容包括：`hosts` 原文件、各浏览器配置目录下的 `Cookies` 数据库副本。

**恢复步骤**：

1. 关闭所有浏览器。
2. 打开对应时间戳的备份目录。
3. Cookie：备份文件名就是浏览器的 `Cookies` 数据库，复制回原浏览器配置目录对应位置（例如 Edge 为 `%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Network\Cookies`，其他浏览器以实际配置目录为准），覆盖前先备份当前文件。
4. hosts：把备份的 `hosts` 复制回 `C:\Windows\System32\drivers\etc\hosts`（需管理员权限）。
5. 重启浏览器（hosts 改动建议再执行一次 `ipconfig /flushdns`）。

## 10. 诊断日志和修复报告保存在哪？

- 诊断日志：程序目录（无写权限时为临时目录）的 `diagnostic_时间.txt`，每次检测后自动保存。
- 修复报告：`修复报告_时间.txt`（验证失败模式）或 `加载慢诊断报告_时间.txt`（加载过久模式）。
- 通过程序内「问题反馈」发送时，会自动附带当次诊断日志（不含账号与 Cookie 值）。

## 11. 命令行参数有哪些？

| 参数 | 作用 |
|---|---|
| `--mode slow` | 启动即进入「加载时间过长（转圈）」模式 |
| `--safe` | 安全模式：只诊断，不修改任何设置 |
| `--autofix` | 启动后自动检测并修复（跳过关闭浏览器确认） |
| `--close-browser` | 自动修复时允许自动关闭浏览器 |
| `--no-splash` | 跳过启动画面 |

## 12. 程序会上传我的数据吗？

不会自动上传。诊断只访问 `challenges.cloudflare.com` 与 `cloudflarestatus.com`；只有你主动点「发送反馈」且配置了 SMTP 时，反馈内容才会发到作者邮箱。详细范围见 [PRIVACY.md](PRIVACY.md)。
