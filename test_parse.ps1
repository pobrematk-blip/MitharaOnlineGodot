$content = Get-Content 'generate_items.ps1' -Raw
$errors = $null
$null = [System.Management.Automation.Language.Parser]::ParseInput($content, [ref]$null, [ref]$errors)
if ($errors) {
    Write-Host "ERRORS FOUND:"
    $errors | ForEach-Object { Write-Host ("Line " + $_.Extent.StartLineNumber + ": " + $_.Message) }
} else {
    Write-Host "PARSE OK - no errors"
}
