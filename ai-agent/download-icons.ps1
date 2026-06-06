$icons = @(
    ##"save",
    ##"pencil",
    ##"mouse-pointer",
    ##"move",
    ##"rotate-3d",
    ##"scaling",
    ##"file-plus",
    ##"folder-open",
    ##"save-all",
    ##"x",
    ##"undo-2",
    ##"redo-2",
    ##"scissors",
    ##"copy",
    ##"clipboard-paste",
    ##"copy-plus",
    ##"trash-2",
    ##"list-tree",
    ##"sliders-horizontal",
    ##"layers-3",
    ##"search",
    ##"settings",
    ##"camera",
    ##"lightbulb",
    ##"grid-3x3",
    ##"magnet",
    ##"eye",
    ##"eye-off",
    ##"lock",
    ##"lock-open",
    ##"zoom-in",
    ##"zoom-out",
    ##"hand",
    ##"focus",
    ##"maximize-2",
    ##"play",
    ##"pause",
    ##"square",
    ##"refresh-cw",
    ##"terminal",
    ##"triangle-alert",
    ##"info",
    ##"circle-help",
    ##"folder",
    ##"image",
    ##"box",
    ##"palette",
    ##"clapperboard",
    ##"volume-2",
    ##"file-code-2",
    ##"package"
    "cuboid",
    "globe"
)

$baseDir = Join-Path $PSScriptRoot "CasaEngine.Editor\Content\icons"
$svgDir = Join-Path $baseDir "svg"
$pngBlackDir = Join-Path $baseDir "png-black"
$pngWhiteDir = Join-Path $baseDir "png-white"

# Mappings: icon list name -> actual Lucide filename (for renamed icons)
$lucideAliases = @{
    "layers-3"    = "layers"
    "circle-help" = "circle-question-mark"
    "file-code-2" = "file-code"
}

New-Item -ItemType Directory -Force -Path $svgDir | Out-Null
New-Item -ItemType Directory -Force -Path $pngBlackDir | Out-Null
New-Item -ItemType Directory -Force -Path $pngWhiteDir | Out-Null

# Adapte ce chemin si besoin
$inkscape = "C:\Program Files\WindowsApps\25415Inkscape.Inkscape_1.4.30.0_x64__9waqn51p1ttv2\VFS\ProgramFilesX64\Inkscape\bin\inkscape.exe"

if (!(Test-Path $inkscape)) {
    throw "Inkscape introuvable. Installe Inkscape ou modifie la variable `$inkscape."
}

Add-Type -AssemblyName System.Drawing

foreach ($icon in $icons) {
    $lucideName = if ($lucideAliases.ContainsKey($icon)) { $lucideAliases[$icon] } else { $icon }
    $svgUrl = "https://raw.githubusercontent.com/lucide-icons/lucide/main/icons/$lucideName.svg"
    $svgPath = Join-Path $svgDir "$icon.svg"
    $pngBlackPath = Join-Path $pngBlackDir "$icon.png"
    $pngWhitePath = Join-Path $pngWhiteDir "$icon.png"

    Write-Host "Téléchargement $icon..."
    Invoke-WebRequest -Uri $svgUrl -OutFile $svgPath

    Write-Host "Conversion PNG noir $icon..."
    & $inkscape $svgPath --export-type=png --export-filename=$pngBlackPath | Out-Null

    Write-Host "Création PNG blanc $icon..."
    $bmp = [System.Drawing.Bitmap]::FromFile($pngBlackPath)
    $outBmp = New-Object System.Drawing.Bitmap($bmp.Width, $bmp.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $newColor = [System.Drawing.Color]::FromArgb($c.A, 255, 255, 255)
            $outBmp.SetPixel($x, $y, $newColor)
        }
    }

    $outBmp.Save($pngWhitePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $outBmp.Dispose()
}

Write-Host "Terminé."
Write-Host "SVG  : $svgDir"
Write-Host "Noir : $pngBlackDir"
Write-Host "Blanc: $pngWhiteDir"