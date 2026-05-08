# 一键开启 Windows SSH 服务（管理员 PowerShell 运行）
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 -ErrorAction SilentlyContinue
Set-Service -Name sshd -StartupType Automatic
Start-Service sshd
New-NetFirewallRule -DisplayName "OpenSSH-Server-In" -Direction Inbound -LocalPort 22 -Protocol TCP -Action Allow -ErrorAction SilentlyContinue

# 输出连接信息
$ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } | Select-Object -First 1).IPAddress
$username = $env:USERNAME
Write-Host ""
Write-Host "========== 连接信息 ==========" -ForegroundColor Green
Write-Host "IP: $ip"
Write-Host "用户名: $username"
Write-Host "端口: 22"
Write-Host "================================" -ForegroundColor Green
Write-Host "把 IP 和用户名发给我，我就能连上来操作你桌面上的 Unity 了"
Read-Host "按回车退出"
