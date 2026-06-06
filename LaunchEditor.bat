@echo off
title Mithara - Editor de Jogo
echo ============================================
echo   Mithara - Editor de Jogo
echo ============================================
echo.
echo Iniciando Godot Engine com o Editor...
echo.

set "GODOT=C:\Users\Wesley\Documents\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe"
set "PROJECT=%~dp0"
set "SCENE=res://EditorOnly.tscn"

if not exist "%GODOT%" (
    echo [ERRO] Godot nao encontrado em:
    echo   %GODOT%
    echo.
    echo Edite este arquivo .bat e ajuste o caminho do Godot na linha 12.
    echo.
    pause
    exit /b 1
)

echo Godot: %GODOT%
echo Projeto: %PROJECT%
echo Cena: %SCENE%
echo.
echo Abrindo editor... (pode levar alguns segundos)
echo.

"%GODOT%" --path "%PROJECT%" --scene "%SCENE%"

echo.
echo Editor fechado.
pause
