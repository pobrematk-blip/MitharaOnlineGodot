$total = (Get-ChildItem $args[0] -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ([math]::Round($total / 1MB, 1)).ToString() "MB"
