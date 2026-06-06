Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Rodando Testes do Mithara Server" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

dotnet test "$PSScriptRoot\Mithara.Server.Tests.csproj" -v normal

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
if ($LASTEXITCODE -eq 0) {
    Write-Host " TODOS OS TESTES PASSARAM!" -ForegroundColor Green
} else {
    Write-Host " ALGUNS TESTES FALHARAM!" -ForegroundColor Red
}
Write-Host "======================================" -ForegroundColor Cyan
Read-Host "Pressione Enter para sair"
