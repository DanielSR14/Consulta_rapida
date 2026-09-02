<#
.SYNOPSIS
    Gera os assets de ícone do "Consulta Rápida" (Assets\app.ico e Assets\icon.png) a partir
    da mesma marca vetorial usada como logo padrão (badge com lupa + linhas de velocidade).

.DESCRIPTION
    Script descartável — rode só quando quiser regenerar o ícone. Desenha a geometria com WPF
    (RenderTargetBitmap), exporta PNGs em vários tamanhos e monta um .ico com frames PNG
    (suportado pelo Windows Vista+). Não adiciona dependência nenhuma ao app.

    Uso:  powershell -ExecutionPolicy Bypass -File tools\generate-branding-assets.ps1
#>

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetsDir = Join-Path $repoRoot "src\ClienteConsulta.App\Assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

# XAML da marca (badge 48x48). Mesma forma que BrandingProvider.BuildDefaultLogo desenha em
# código, mas com a cor fixa (#2563EB) porque o ícone do .exe não muda com o tema.
$xaml = @"
<DrawingImage xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
  <DrawingImage.Drawing>
    <DrawingGroup>
      <GeometryDrawing Brush='#2563EB'>
        <GeometryDrawing.Geometry><RectangleGeometry Rect='0,0,48,48' RadiusX='12' RadiusY='12'/></GeometryDrawing.Geometry>
      </GeometryDrawing>
      <GeometryDrawing>
        <GeometryDrawing.Pen><Pen Brush='#66FFFFFF' Thickness='3' StartLineCap='Round' EndLineCap='Round'/></GeometryDrawing.Pen>
        <GeometryDrawing.Geometry>
          <GeometryGroup>
            <LineGeometry StartPoint='7,17' EndPoint='17,17'/>
            <LineGeometry StartPoint='5,24' EndPoint='15,24'/>
            <LineGeometry StartPoint='7,31' EndPoint='17,31'/>
          </GeometryGroup>
        </GeometryDrawing.Geometry>
      </GeometryDrawing>
      <GeometryDrawing>
        <GeometryDrawing.Pen><Pen Brush='White' Thickness='4.5' StartLineCap='Round' EndLineCap='Round'/></GeometryDrawing.Pen>
        <GeometryDrawing.Geometry>
          <GeometryGroup>
            <EllipseGeometry Center='26,22' RadiusX='9' RadiusY='9'/>
            <LineGeometry StartPoint='32.5,28.5' EndPoint='40,36'/>
          </GeometryGroup>
        </GeometryDrawing.Geometry>
      </GeometryDrawing>
    </DrawingGroup>
  </DrawingImage.Drawing>
</DrawingImage>
"@

$drawingImage = [Windows.Markup.XamlReader]::Parse($xaml)

function Render-Png([int]$size) {
    $visual = New-Object Windows.Media.DrawingVisual
    $ctx = $visual.RenderOpen()
    $ctx.DrawImage($drawingImage, (New-Object Windows.Rect(0, 0, $size, $size)))
    $ctx.Close()

    $rtb = New-Object Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($visual)

    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    [void]$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $ms = New-Object IO.MemoryStream
    [void]$encoder.Save($ms)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    Write-Output -NoEnumerate $bytes
}

# icon.png (usado como Resource no app, 256px)
[IO.File]::WriteAllBytes((Join-Path $assetsDir "icon.png"), (Render-Png 256))

# app.ico multi-size com frames PNG
$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = $sizes | ForEach-Object { , (Render-Png $_) }

$ico = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter($ico)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $bytes = $frames[$i]
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$bytes.Length)
    $w.Write([UInt32]$offset)
    $offset += $bytes.Length
}
foreach ($bytes in $frames) { $w.Write($bytes) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $assetsDir "app.ico"), $ico.ToArray())

Write-Host "Gerado: $assetsDir\app.ico  e  $assetsDir\icon.png" -ForegroundColor Green
