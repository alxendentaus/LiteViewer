@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

echo ============================================
echo   LiteViewer 源码上传助手
echo   目标: https://github.com/alxendentaus/LiteViewer
echo ============================================
echo.

cd /d "%~dp0"

REM ---- 定位 git.exe（本机的 git 不在系统 PATH，需自动查找）----
set "GIT_EXE="
where git >nul 2>&1 && set "GIT_EXE=git"
if not defined GIT_EXE (
    if exist "%ProgramFiles%\WorkBuddy\resources\vendor\PortableGit\mingw64\bin\git.exe" (
        set "GIT_EXE=%ProgramFiles%\WorkBuddy\resources\vendor\PortableGit\mingw64\bin\git.exe"
    )
)
if not defined GIT_EXE (
    if exist "%LOCALAPPDATA%\Programs\Git\cmd\git.exe" set "GIT_EXE=%LOCALAPPDATA%\Programs\Git\cmd\git.exe"
)
if not defined GIT_EXE (
    if exist "%ProgramFiles%\Git\cmd\git.exe" set "GIT_EXE=%ProgramFiles%\Git\cmd\git.exe"
)
if not defined GIT_EXE (
    if exist "%ProgramFiles(x86)%\Git\cmd\git.exe" set "GIT_EXE=%ProgramFiles(x86)%\Git\cmd\git.exe"
)
if not defined GIT_EXE (
    echo [错误] 未找到 git.exe。请安装 Git for Windows 后重试。
    pause & exit /b 1
)
echo 使用 git: %GIT_EXE%
echo.

"%GIT_EXE%" rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo [错误] 当前目录不是 git 仓库。
    pause & exit /b 1
)

echo [1/3] 本地仓库就绪，提交数:
"%GIT_EXE%" rev-list --count HEAD
echo.

REM ---- 设置 remote ----
"%GIT_EXE%" remote get-url origin >nul 2>&1
if errorlevel 1 (
    "%GIT_EXE%" remote add origin https://github.com/alxendentaus/LiteViewer.git
    echo [2/3] 已添加远程仓库 origin。
) else (
    echo [2/3] origin 已存在:
    "%GIT_EXE%" remote get-url origin
)

REM ---- 提示登录 ----
echo.
echo [3/3] 检测远程仓库是否存在...
echo  注意：接下来可能弹出 GitHub 登录窗口，请先登录（勾选记住凭据）。
echo.

REM ---- 检测远程仓库是否已创建 ----
"%GIT_EXE%" ls-remote origin >nul 2>&1
if not errorlevel 1 (
    echo  远程仓库已存在，直接推送。
    goto PUSH
)

REM ---- 仓库不存在或登录未完成：打开预填好的创建页面 ----
echo.
echo  检测失败（仓库不存在，或尚未登录成功）。
echo  即将打开预填好的创建页面：
echo   仓库名: LiteViewer
echo   描述:   Lightweight log and code viewer (WinForms)
echo.
echo  请在网页上：
echo   1. 确认 Owner = alxendentaus、Repository name = LiteViewer
echo   2. 选 Public
echo   3. 不要勾选 README / .gitignore / license（保持空仓库）
echo   4. 点 Create repository
echo   5. 回到本窗口按任意键继续
echo.
start "" "https://github.com/new?name=LiteViewer&description=Lightweight%20log%20and%20code%20viewer%20built%20with%20WinForms"
pause

:PUSH
echo.
echo 正在推送（如弹出 GitHub 登录，请登录并勾选记住凭据）...
"%GIT_EXE%" push -u origin main
if errorlevel 1 (
    echo.
    echo [失败] 推送未成功，可能原因：
    echo   - 认证失败：安装 GitHub Desktop 登录后重试
    echo   - 仓库名不符：确认网页建的是 alxendentaus/LiteViewer 空仓库
    echo   - 网络问题：确认能访问 github.com
    echo.
    echo 也可手动重试（完整路径）：
    echo   "%GIT_EXE%" push -u origin main
) else (
    echo.
    echo ============================================
    echo  上传成功！
    echo  仓库地址: https://github.com/alxendentaus/LiteViewer
    echo ============================================
)

echo.
pause
