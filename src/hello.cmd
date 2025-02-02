@echo off
set buildDll=%~dp0build\bin\Debug\netcoreapp2.2\build.dll
set exitCodeOutOfDate=2

echo startup time > %buildDll%.startup
if exist %buildDll% (
    dotnet %buildDll% %*
) else (
    call :build
)
goto :eof

:build
    echo Building %buildDll%
    dotnet run --force -vd --project %~dp0build -- --ignore-clean %*
    goto :eof