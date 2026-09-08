#Requires -Version 5.1
<# Rebuild the project-owned vector app mark as a Windows icon. No external artwork. #>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$assetDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\source\iDock\Assets'))
$null = New-Item -ItemType Directory -Path $assetDirectory -Force
$visual = New-Object System.Windows.Media.DrawingVisual
$drawing = $visual.RenderOpen()
try {
    $gradient = New-Object System.Windows.Media.LinearGradientBrush
    $gradient.StartPoint = New-Object System.Windows.Point(0, 0)
    $gradient.EndPoint = New-Object System.Windows.Point(1, 1)
    $gradient.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.ColorConverter]::ConvertFromString('#48A5FF'), 0)))
    $gradient.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.ColorConverter]::ConvertFromString('#0867E9'), 1)))
    $drawing.DrawRoundedRectangle($gradient, $null, (New-Object System.Windows.Rect(8, 8, 240, 240)), 58, 58)
    $drawing.PushTransform((New-Object System.Windows.Media.TranslateTransform(39, 30)))
    $drawing.PushTransform((New-Object System.Windows.Media.ScaleTransform(6.3, 6.3)))
    $whitePen = New-Object System.Windows.Media.Pen([System.Windows.Media.Brushes]::White, 1.7)
    $whitePen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
    $display = [System.Windows.Media.Geometry]::Parse('M4,5 L19,5 Q21,5 21,7 L21,20 M4,5 Q2,5 2,7 L2,20 Q2,22 4,22 L12,22 M7,25 L15,25 M11,22 L11,25')
    $drawing.DrawGeometry($null, $whitePen, $display)
    $phoneBrush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString('#1778EE'))
    $drawing.DrawRoundedRectangle($phoneBrush, $whitePen, (New-Object System.Windows.Rect(16, 10, 10, 17)), 2.5, 2.5)
    $drawing.DrawLine($whitePen, (New-Object System.Windows.Point(19, 24)), (New-Object System.Windows.Point(23, 24)))
    $drawing.Pop(); $drawing.Pop()
}
finally { $drawing.Close() }
$bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(256, 256, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($visual)
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$memory = New-Object IO.MemoryStream
try {
    $encoder.Save($memory)
    $png = $memory.ToArray()
    $path = Join-Path $assetDirectory 'iDock.ico'
    $file = [IO.File]::Create($path)
    $writer = New-Object IO.BinaryWriter($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22)
        $writer.Write($png)
    }
    finally { $writer.Dispose(); $file.Dispose() }
    Write-Output "Generated project-owned app icon: $path"
}
finally { $memory.Dispose() }
