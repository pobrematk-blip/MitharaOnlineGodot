$outFile = "$env:TEMP\localtunnel_url.txt"
$process = Start-Process -FilePath "npx.cmd" -ArgumentList "localtunnel --port 5000" -NoNewWindow -RedirectStandardOutput $outFile -PassThru
Start-Sleep -Seconds 15
if (Test-Path $outFile) {
    Get-Content $outFile
    Write-Host ""
    Write-Host "URL salva em: $outFile"
}
$process.WaitForExit()
