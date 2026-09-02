@echo off
chcp 65001 >nul
REM ─────────────────────────────────────────────────────────────
REM  팀 배포용 exe 만들기 (자체 포함 단일 파일)
REM
REM  결과물 : D:\중점추진과제 SW\workspace\배포\JahwaInspector.Light\
REM           JahwaInspector.Light.exe  (파일 1개, 약 185MB)
REM
REM  이 exe 하나만 팀원에게 전달하면 된다.
REM  받는 PC 에 .NET 8 런타임을 설치하지 않아도 실행된다.
REM ─────────────────────────────────────────────────────────────
cd /d "%~dp0"

echo.
echo [1/3] 실행 중인 앱 종료
REM 파일명에 버전이 붙으므로 와일드카드로 종료한다 (JahwaInspector.Light.1.0.0.exe 등)
for /f "tokens=1" %%i in ('tasklist /fi "IMAGENAME eq JahwaInspector.Light.*" /nh 2^>nul ^| findstr /i JahwaInspector') do taskkill /IM "%%i" /F >nul 2>&1

echo [2/3] 게시 중... (2~3분 걸립니다)
dotnet publish JahwaInspector.Light.csproj -c Release -p:PublishProfile=SingleFile
if errorlevel 1 (
    echo.
    echo *** 게시 실패 — 위 오류 메시지를 확인하세요. ***
    pause
    exit /b 1
)

echo [3/3] 완료
echo.
echo 결과물 폴더를 엽니다...
explorer "D:\중점추진과제 SW\workspace\배포\JahwaInspector.Light"
echo.
pause
