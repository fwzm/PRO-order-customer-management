@echo off
chcp 65001 >nul
title PRO 一键发布

echo ====================================
echo   PRO 企业管理系统 - 一键发布
echo ====================================
echo.

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\PRO.Desktop\PRO.Desktop.csproj"
set "PUBLISH_DIR=%ROOT%publish"

:: 清理旧发布
echo [1/5] 清理旧发布目录...
if exist "%PUBLISH_DIR%" (
    rmdir /s /q "%PUBLISH_DIR%"
)

:: 构建 Release
echo [2/5] 编译 Release...
dotnet build "%PROJECT%" -c Release --nologo
if %errorlevel% neq 0 (
    echo [失败] 编译错误！请查看上方信息。
    pause
    exit /b 1
)
echo      编译成功

:: 发布为独立部署（无需 .NET 运行时）
echo [3/5] 发布到 publish 目录（独立部署）...
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true -o "%PUBLISH_DIR%" --nologo
if %errorlevel% neq 0 (
    echo [失败] 发布错误！
    pause
    exit /b 1
)
echo      发布成功

:: 移除调试文件以减小体积
echo [4/5] 移除调试符号文件...
if exist "%PUBLISH_DIR%\*.pdb" (
    del /s /q "%PUBLISH_DIR%\*.pdb" >nul 2>&1
)
echo      清理完成

:: 完成
echo [5/5] 完成！
echo.
echo ====================================
echo   发布完成！
echo   路径: %PUBLISH_DIR%\PRO.exe
echo   类型: 独立部署（无需 .NET 运行时）
echo   大小:
for %%f in ("%PUBLISH_DIR%\PRO.exe") do echo       %%~zf 字节
echo ====================================

pause
