#Requires -Version 5.1
<#
  在桌面创建“刷新率切换”快捷方式，指向 RefreshSwitch.exe。
  注意：这里故意不设置 lnk 的快捷键 —— 全局快捷键由程序自己用 RegisterHotKey 注册，
        lnk 再占一份会导致程序注册失败（“被其他程序占用”）。
  不想要了：删掉桌面快捷方式即可。
#>
param(
    [string]$LinkName = '刷新率切换'
)

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe  = Join-Path $here 'RefreshSwitch.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "找不到 $exe，先运行 build.ps1" }

$desktop = [Environment]::GetFolderPath('Desktop')

# 清掉早期版本的快捷方式（它通过 lnk 占用快捷键，会和程序冲突）
$old = Join-Path $desktop '切换刷新率.lnk'
if (Test-Path -LiteralPath $old) {
    Remove-Item -LiteralPath $old -Force
    Write-Host "已移除旧快捷方式：$old" -ForegroundColor DarkGray
}

$lnkPath = Join-Path $desktop ($LinkName + '.lnk')
$wsh = New-Object -ComObject WScript.Shell
$lnk = $wsh.CreateShortcut($lnkPath)
$lnk.TargetPath       = $exe
$lnk.WorkingDirectory = $here
$lnk.Description      = '一键切换显示器刷新率（120Hz / 60Hz）'
$lnk.IconLocation     = "$exe,0"
$lnk.Hotkey           = ''
$lnk.WindowStyle      = 1
$lnk.Save()

Write-Host ''
Write-Host ('  已创建快捷方式：' + $lnkPath) -ForegroundColor Green
Write-Host '  快捷键在程序里设置（托盘图标右键 → 设置…）' -ForegroundColor DarkGray
Write-Host ''
