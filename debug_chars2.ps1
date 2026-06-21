# Find first Adaga Secundaria line in TSV
$lines = Get-Content 'items_data.tsv'
foreach ($line in $lines) {
    if ($line -match 'Adaga Secund') {
        $cols = $line -split "	"
        $tipo = $cols[3]
        Write-Host ("FOUND: >" + $tipo + "<")
        for ($i = 0; $i -lt $tipo.Length; $i++) {
            Write-Host ("  [{0}]={1} (U+{2:X4})" -f $i, $tipo[$i], [int]$tipo[$i])
        }
        break
    }
}
# Now check the key in rawConfig
. .\generate_items.ps1 | Out-Null
Write-Host ("RawConfig keys with 'Secund':")
foreach ($key in $rawConfig.Keys) {
    if ($key -match 'Secund') {
        Write-Host ("Key: >" + $key + "<")
        for ($i = 0; $i -lt $key.Length; $i++) {
            Write-Host ("  [{0}]={1} (U+{2:X4})" -f $i, $key[$i], [int]$key[$i])
        }
        $normKey = Normalize-Tipo $key
        Write-Host ("Normalized: >" + $normKey + "<")
        for ($i = 0; $i -lt $normKey.Length; $i++) {
            Write-Host ("  [{0}]={1} (U+{2:X4})" -f $i, $normKey[$i], [int]$normKey[$i])
        }
        $normTsv = Normalize-Tipo $tipo
        Write-Host ("TSV Normalized: >" + $normTsv + "<")
        Write-Host ("Match: " + ($normKey -eq $normTsv))
    }
}
