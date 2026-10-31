# Generates pixel-block.png and pixel-block.fnt from pixel-block.glyphs.txt, next to this script.
# Windows PowerShell: System.Drawing writes the PNG.
Add-Type -AssemblyName System.Drawing

$GlyphWidth = 5
$GlyphHeight = 7
$Advance = 6
$LineHeight = 8
$CellSize = 8
$Columns = 16

$lines = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'pixel-block.glyphs.txt')
$glyphs = New-Object System.Collections.ArrayList
$current = $null
foreach ($line in $lines) {
    if ($line.StartsWith('= ')) {
        $name = $line.Substring(2)
        $character = if ($name -ceq 'space') { [char]' ' } else { [char]$name[0] }
        $current = @{ Character = $character; Rows = (New-Object System.Collections.ArrayList) }
        [void]$glyphs.Add($current)
    }
    elseif ($null -ne $current) {
        [void]$current.Rows.Add($line)
    }
}

$rows = [int][Math]::Ceiling($glyphs.Count / $Columns)
$bitmap = New-Object System.Drawing.Bitmap ($Columns * $CellSize), ($rows * $CellSize), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

$charLines = New-Object System.Collections.ArrayList
function Add-Char([int]$id, [int]$x, [int]$y) {
    [void]$charLines.Add("char id=$id x=$x y=$y width=$GlyphWidth height=$GlyphHeight xoffset=0 yoffset=0 xadvance=$Advance page=0 chnl=15")
}

for ($index = 0; $index -lt $glyphs.Count; $index++) {
    $glyph = $glyphs[$index]
    if ($glyph.Rows.Count -ne $GlyphHeight) { throw "Glyph '$($glyph.Character)' has $($glyph.Rows.Count) rows." }

    $cellX = ($index % $Columns) * $CellSize
    $cellY = [Math]::Floor($index / $Columns) * $CellSize

    for ($y = 0; $y -lt $GlyphHeight; $y++) {
        $row = $glyph.Rows[$y]
        if ($row.Length -ne $GlyphWidth) { throw "Glyph '$($glyph.Character)', row ${y}: '$row'." }

        for ($x = 0; $x -lt $GlyphWidth; $x++) {
            if ($row[$x] -eq '#') {
                $bitmap.SetPixel($cellX + $x, $cellY + $y, [System.Drawing.Color]::White)
            }
        }
    }

    Add-Char ([int]$glyph.Character) $cellX $cellY
    if ($glyph.Character -ge [char]'A' -and $glyph.Character -le [char]'Z') {
        Add-Char ([int]$glyph.Character + 32) $cellX $cellY
    }
}

$bitmap.Save((Join-Path $PSScriptRoot 'pixel-block.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

$fnt = New-Object System.Collections.ArrayList
[void]$fnt.Add('info face="pixel-block" size=8 bold=0 italic=0 charset="" unicode=1 stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=0,0 outline=0')
[void]$fnt.Add("common lineHeight=$LineHeight base=$GlyphHeight scaleW=$($Columns * $CellSize) scaleH=$($rows * $CellSize) pages=1 packed=0 alphaChnl=0 redChnl=0 greenChnl=0 blueChnl=0")
[void]$fnt.Add('page id=0 file="pixel-block.png"')
[void]$fnt.Add("chars count=$($charLines.Count)")
[void]$fnt.AddRange($charLines)
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'pixel-block.fnt') -Value $fnt -Encoding Ascii
