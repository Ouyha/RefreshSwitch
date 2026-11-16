# 编译 RefreshSwitch.exe：直接用 .NET Framework 自带的 csc，不依赖 SDK / NuGet
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = @(
    (Join-Path $root 'src\RefreshSwitch.cs')
)
$out  = Join-Path $root 'RefreshSwitch.exe'
$ico  = Join-Path $root 'src\app.ico'

foreach ($f in $src) { if (-not (Test-Path -LiteralPath $f)) { throw "缺少源文件：$f" } }
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Force }

$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$gac = 'C:\Windows\Microsoft.NET\assembly'

function Find-Gac([string]$name) {
    foreach ($bucket in 'GAC_MSIL', 'GAC_64', 'GAC_32') {
        $dir = Join-Path $gac "$bucket\$name"
        if (Test-Path $dir) {
            $ver = Get-ChildItem $dir -Directory | Sort-Object Name -Descending | Select-Object -First 1
            $dll = Join-Path $ver.FullName "$name.dll"
            if (Test-Path $dll) { return $dll }
        }
    }
    return $null
}

$refs = @(
    (Join-Path $fw 'System.dll'),
    (Join-Path $fw 'System.Core.dll'),
    (Join-Path $fw 'System.Drawing.dll'),
    (Join-Path $fw 'System.Windows.Forms.dll'),
    (Join-Path $fw 'System.Xaml.dll'),
    (Find-Gac 'WindowsBase'),
    (Find-Gac 'PresentationCore'),
    (Find-Gac 'PresentationFramework')
)
foreach ($r in $refs) { if (-not $r -or -not (Test-Path $r)) { throw "找不到引用程序集：$r" } }

# 生成 exe 图标（256x256 PNG 载荷的 ICO，Vista 以上原生支持）
function New-Icon([string]$path) {
    $bmp = New-Object System.Drawing.Bitmap(256, 256)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(24, 27, 33))
    $g.FillEllipse($dark, 8, 8, 240, 240)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0x4A, 0xDE, 0x80)), 14
    $pen.StartCap = 'Round'
    $pen.EndCap = 'Round'
    $g.DrawRectangle($pen, 62, 78, 132, 88)
    $g.DrawLine($pen, 100, 206, 156, 206)
    $g.DrawLine($pen, 128, 166, 128, 206)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $png = $ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()

    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$png.Length); $bw.Write([uint32]22)
    $bw.Write($png)
    $bw.Flush(); $bw.Close(); $fs.Close()
}

New-Icon $ico

$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }

$cscArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/warn:0',
             "/out:$out", "/win32icon:$ico")
foreach ($r in $refs) { $cscArgs += "/r:$r" }
$cscArgs += $src

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc 编译失败（退出码 $LASTEXITCODE）" }
if (-not (Test-Path -LiteralPath $out)) { throw '编译没有产出 exe' }

$f = Get-Item -LiteralPath $out
"编译成功：$($f.FullName)"
"大小：{0:N1} KB" -f ($f.Length / 1KB)
