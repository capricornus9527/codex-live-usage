# Rebuild the companion's original vector design as a 512px PNG using Windows GDI+.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object Drawing.Bitmap 512,512
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$path = New-Object Drawing.Drawing2D.GraphicsPath
$path.AddArc(8,8,224,224,180,90)
$path.AddArc(280,8,224,224,270,90)
$path.AddArc(280,280,224,224,0,90)
$path.AddArc(8,280,224,224,90,90)
$path.CloseFigure()
$background = New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.Point]::new(8,8)),([Drawing.Point]::new(504,504)),([Drawing.ColorTranslator]::FromHtml('#172d4d')),([Drawing.ColorTranslator]::FromHtml('#0b1527'))
$muted = New-Object Drawing.Pen ([Drawing.ColorTranslator]::FromHtml('#29465b')),28
$accent = New-Object Drawing.Pen ([Drawing.ColorTranslator]::FromHtml('#5ee4c5')),28
$pulse = New-Object Drawing.Pen ([Drawing.ColorTranslator]::FromHtml('#f0f9ff')),23
$brush = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#5ee4c5'))
try {
    foreach ($pen in @($muted,$accent,$pulse)) {
        $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    }
    $graphics.FillPath($background,$path)
    $graphics.DrawArc($muted,102,90,308,308,143,254)
    $graphics.DrawArc($accent,102,90,308,308,143,146)
    $points = [Drawing.PointF[]]@([Drawing.PointF]::new(111,277),[Drawing.PointF]::new(187,277),[Drawing.PointF]::new(219,218),[Drawing.PointF]::new(264,332),[Drawing.PointF]::new(306,252),[Drawing.PointF]::new(401,252))
    $graphics.DrawLines($pulse,$points)
    $graphics.FillEllipse($brush,385,236,32,32)
    $graphics.DrawLine($accent,202,405,310,405)
    $bitmap.Save((Join-Path $PSScriptRoot '..\assets\icon.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {
    foreach ($resource in @($brush,$pulse,$accent,$muted,$background,$path,$graphics,$bitmap)) { $resource.Dispose() }
}
