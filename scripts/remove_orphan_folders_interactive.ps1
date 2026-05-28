Param(
    [string]$CsvPath = "$env:USERPROFILE\Desktop\orphan_folders_report.csv",
    [switch]$OnlyOrphanLikely = $true,
    [switch]$UseRecycleBin = $true,
    [string]$LogPath = "$env:USERPROFILE\Desktop\orphan_delete_log.csv"
)

Add-Type -AssemblyName Microsoft.VisualBasic

if (-not (Test-Path $CsvPath)) {
    Write-Error "CSV não encontrado em: $CsvPath"
    exit 1
}

$rows = Import-Csv -Path $CsvPath -ErrorAction Stop
if ($OnlyOrphanLikely) { $rows = $rows | Where-Object { $_.Confidence -eq 'Orphan-Likely' } }
if (-not $rows) { Write-Output "Nenhuma entrada para processar."; exit 0 }

$log = @()

foreach ($r in $rows | Sort-Object {[double]$_.SizeMB} -Descending) {
    $path = $r.Path
    $size = $r.SizeMB
    $last = $r.LastWrite
    $exeCount = $r.ExeCount

    if (-not (Test-Path $path)) {
        $log += [PSCustomObject]@{ Path=$path; SizeMB=$size; Action='Missing'; Result='Path not found'; Time=(Get-Date) }
        continue
    }

    Write-Output "\nPath: $path"
    Write-Output "SizeMB: $size   LastWrite: $last   ExeCount: $exeCount"

    while ($true) {
        $choice = Read-Host "Escolher ação: (Y) Lixeira, (D) Excluir permanentemente, (O) Abrir pasta, (S) Pular"
        switch ($choice.ToUpper()) {
            'Y' {
                try {
                    [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($path, [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs, [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
                    $log += [PSCustomObject]@{ Path=$path; SizeMB=$size; Action='DeleteToRecycleBin'; Result='OK'; Time=(Get-Date) }
                } catch {
                    $log += [PSCustomObject]@{ Path=$path; SizeMB=$size; Action='DeleteToRecycleBin'; Result=$_.Exception.Message; Time=(Get-Date) }
                }
                break
            }
            'D' {
                try {
                    Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Stop
                    $log += [PSCustomObject]@{ Path=$path; SizeMB=$size; Action='DeletePermanent'; Result='OK'; Time=(Get-Date) }
                } catch {
                    $log += [PSCustomObject]@{ Path=$path; SizeMB=$size; Action='DeletePermanent'; Result=$_.Exception.Message; Time=(Get-Date) }
                }
                break
            }
            'O' {
                Start-Process explorer.exe -ArgumentList ($path)
            }
            'S' {
                $log += [PSCustomObject]@{ Path=$path; SizeMB=$size; Action='Skip'; Result='User skipped'; Time=(Get-Date) }
                break
            }
            default { Write-Output "Entrada inválida. Use Y, D, O ou S." }
        }
        if ($choice.ToUpper() -in @('Y','D','S')) { break }
    }
}

# Export log
try {
    $log | Export-Csv -Path $LogPath -NoTypeInformation -Encoding UTF8
    Write-Output "Log salvo em $LogPath"
} catch {
    Write-Error "Falha ao salvar log: $_"
}
