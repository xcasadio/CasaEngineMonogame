# Generates cursor.png from cursor.source.txt, next to this script: the body on the left, the shade on the right.
# Windows PowerShell: System.Drawing writes the PNG.
Add-Type -AssemblyName System.Drawing

$CellSize = 16
$Colors = @{
    'K' = [System.Drawing.Color]::FromArgb(255, 24, 24, 32)
    'Y' = [System.Drawing.Color]::FromArgb(255, 255, 216, 64)
    # A subtractive texel: its color is what it takes away from what is below it.
    's' = [System.Drawing.Color]::FromArgb(255, 112, 112, 112)
}

$lines = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'cursor.source.txt')
$bitmap = New-Object System.Drawing.Bitmap (2 * $CellSize), $CellSize, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

$cell = -1
$y = 0
foreach ($line in $lines) {
    if ($line.StartsWith('= ')) {
        $cell++
        $y = 0
        continue
    }

    if ($line.Length -ne $CellSize) { throw "Row $y of cell $cell has $($line.Length) columns." }

    for ($x = 0; $x -lt $CellSize; $x++) {
        $texel = [string]$line[$x]
        # Everything else stays transparent black: a subtractive image ignores alpha, so it must have no color either.
        if ($texel -cne '.') {
            $bitmap.SetPixel($cell * $CellSize + $x, $y, $Colors[$texel])
        }
    }

    $y++
}

$bitmap.Save((Join-Path $PSScriptRoot 'cursor.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
