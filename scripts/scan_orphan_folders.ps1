Param(
    [int]$MinSizeMB = 50,
    [string]$OutputCsv = "$env:USERPROFILE\Desktop\orphan_folders_report.csv"
)

function Get-FolderSizeMB($path) {
    try {
        $sum = Get-ChildItem -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { -not $_.PSIsContainer } |
            Measure-Object -Property Length -Sum
        if ($sum.Sum) { [math]::Round($sum.Sum / 1MB, 2) } else { 0 }
    } catch { 0 }
}

function Find-RegistryInstall($folderName,$folderPath) {
    $matches = @()
    $keys = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    foreach ($k in $keys) {
        try {
            $items = Get-ItemProperty -Path $k -ErrorAction SilentlyContinue | Where-Object { $_ }
            foreach ($item in $items) {
                $display = $item.DisplayName -as [string]
                $instLoc = $item.InstallLocation -as [string]
                if ($instLoc) {
                    if ($folderPath -and ($instLoc -ne "")) {
                        if ($folderPath -like "$instLoc*") { $matches += @{DisplayName=$display; InstallLocation=$instLoc} }
                    }
                } elseif ($display) {
                    if ($display -like "*$folderName*") { $matches += @{DisplayName=$display; InstallLocation=$instLoc} }
                }
            }
        } catch {}
    }
    return $matches
}

# Collect candidate install folders from common locations
$users = Get-ChildItem C:\Users -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -notin @('Public','Default','Default User','All Users') }
$candidatePaths = @()
$commonPaths = @("C:\Program Files","C:\Program Files (x86)","C:\ProgramData")
foreach ($p in $commonPaths) { if (Test-Path $p) { $candidatePaths += (Get-ChildItem $p -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName) } }
foreach ($u in $users) {
    $p = Join-Path $u.FullName "AppData\Local\Programs"
    if (Test-Path $p) { $candidatePaths += (Get-ChildItem $p -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName) }
}
# Steam common
$steamCommon = "C:\Program Files (x86)\Steam\steamapps\common"
if (Test-Path $steamCommon) { $candidatePaths += (Get-ChildItem $steamCommon -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName) }

$report = @()
foreach ($folder in $candidatePaths | Sort-Object) {
    $sizeMB = Get-FolderSizeMB $folder
    if ($sizeMB -lt $MinSizeMB) { continue }
    $exeCount = (Get-ChildItem -LiteralPath $folder -Recurse -Include *.exe,*.dll -ErrorAction SilentlyContinue | Measure-Object).Count
    $lastWriteItem = Get-ChildItem -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $lastWrite = if ($lastWriteItem) { $lastWriteItem.LastWriteTime } else { $null }
    $folderName = Split-Path $folder -Leaf
    $matches = Find-RegistryInstall -folderName $folderName -folderPath $folder
    $registryMatch = $matches.Count -gt 0
    $displayNames = ($matches | ForEach-Object { $_.DisplayName }) -join "; "
    if ($registryMatch) { $confidence = "Installed" } elseif ($exeCount -gt 0) { $confidence = "Orphan-Likely" } else { $confidence = "Orphan-Possible" }
    $report += [PSCustomObject]@{
        Path = $folder
        SizeMB = $sizeMB
        LastWrite = $lastWrite
        ExeCount = $exeCount
        RegistryMatch = $registryMatch
        DisplayNames = $displayNames
        Confidence = $confidence
    }
}

$report | Sort-Object -Property SizeMB -Descending | Export-Csv -Path $OutputCsv -NoTypeInformation -Encoding UTF8
Write-Output "Relatório gerado em $OutputCsv"
