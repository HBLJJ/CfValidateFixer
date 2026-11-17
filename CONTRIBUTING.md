# 贡献指南

感谢关注 Cloudflare 修复工坊（CfValidateFixer）。

## 许可

本项目以 **GPLv3 + §7 署名保护附加条款** 发布，提交贡献即表示同意以相同许可授权，
并保留原作者「火爆辣椒鸡」的署名（见 NOTICE）。

## 开发

```powershell
dotnet build tools\CfValidateFixer\CfValidateFixer.csproj -c Release
dotnet publish tools\CfValidateFixer\CfValidateFixer.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=false `
  -p:EnableCompressionInSingleFile=true -o dist
```

- 字体：`fonts/zpix.ttf`（Zpix，个人项目免费，仓库不随附，请自行下载）
- 签名：`.\sign.ps1 -SelfSigned`（开发测试）或 `.\sign.ps1 -PfxPath cert.pfx -PfxPassword ***`（正式）

## 提交

- 新增检测/修复项请同时更新 README 的说明
- 不要移除界面、关于窗口、报告中的作者与产品名
- 提交前确认 `dotnet build` 0 警告 0 错误
