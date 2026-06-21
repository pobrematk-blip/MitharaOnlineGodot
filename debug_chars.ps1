$tsvLine = Get-Content 'items_data.tsv' | Select-Object -Index 44
$cols = $tsvLine -split "	"
$tipo = $cols[3]
Write-Host ("TSV Tipo: >" + $tipo + "<")
for ($i = 0; $i -lt $tipo.Length; $i++) {
    Write-Host ("  [{0}]={1} (U+{2:X4})" -f $i, $tipo[$i], [int]$tipo[$i])
}
# Check what raw config key looks like
. .\generate_items.ps1 | Out-Null
$normalized = Normalize-Tipo $tipo
Write-Host ("Normalized: >" + $normalized + "<")
for ($i = 0; $i -lt $normalized.Length; $i++) {
    Write-Host ("  [{0}]={1} (U+{2:X4})" -f $i, $normalized[$i], [int]$normalized[$i])
}
Write-Host ("Keys in tipoConfig: " + ($tipoConfig.Keys -join ", "))
Write-Host ("Found: " + ($tipoConfig.ContainsKey($normalized)))
