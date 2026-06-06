@echo off
cd /d "%~dp0.."
echo ======================================
echo  Rodando Testes do Mithara Server
echo ======================================
echo.
dotnet test tests\Mithara.Server.Tests.csproj -v normal
echo.
echo ======================================
if %ERRORLEVEL% equ 0 (
    echo  TODOS OS TESTES PASSARAM!
) else (
    echo  ALGUNS TESTES FALHARAM!
)
echo ======================================
pause
