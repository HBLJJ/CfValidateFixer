<#
.SYNOPSIS
    Cloudflare 修复工坊（CfValidateFixer）代码签名脚本

.DESCRIPTION
    给发布版 exe 做 Authenticode 签名。
    - 自签名模式（开发/测试）：生成或复用本机代码签名证书并签名，同时导出 .cer 公钥文件
    - 正式模式：使用 PFX 证书与密码签名（建议从 CA 购买 OV/EV 代码签名证书）

    说明：PowerShell 的 Set-AuthenticodeSignature 不附带时间戳。
    正式发布建议使用 Windows SDK 的 signtool 并加时间戳：
      signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f cert.pfx /p <密码> CfValidateFixer.exe

.EXAMPLE
    .\sign.ps1 -SelfSigned

.EXAMPLE
    .\sign.ps1 -PfxPath .\certs\my.pfx -PfxPassword "******"
#>
param(
    [string]$Exe = "$PSScriptRoot\dist\CfValidateFixer.exe",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [switch]$SelfSigned
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Exe)) { throw "找不到 exe：$Exe" }

if ($SelfSigned) {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -like "*火爆辣椒鸡*" } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type CodeSigningCert `
            -Subject "CN=火爆辣椒鸡, O=Huobaolajiaoji, C=CN" `
            -KeyUsage DigitalSignature `
            -FriendlyName "CfValidateFixer Code Signing (self-signed)" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -NotAfter (Get-Date).AddYears(3)
        Write-Host "已生成自签名代码签名证书：" $cert.Thumbprint
    } else {
        Write-Host "复用已有自签名证书：" $cert.Thumbprint
    }
    $sig = Set-AuthenticodeSignature -FilePath $Exe -Certificate $cert -HashAlgorithm SHA256
    $cer = Join-Path (Split-Path $Exe) "CfValidateFixer.cer"
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null
    Write-Host "已签名，公钥证书已导出：" $cer
} elseif ($PfxPath) {
    $sec = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
    $cert = Get-PfxCertificate -FilePath $PfxPath
    $sig = Set-AuthenticodeSignature -FilePath $Exe -Certificate $cert -HashAlgorithm SHA256
    Write-Host "已使用证书签名：" $cert.Subject
} else {
    throw "请指定 -SelfSigned（自签名）或 -PfxPath/-PfxPassword（正式证书）"
}

Get-AuthenticodeSignature $Exe | Select-Object Status, StatusMessage, SignerCertificate | Format-List
