@echo off
set buildDll=%~dp0build\bin\Debug\netcoreapp2.2\build.dll
echo startup time > %buildDll%.startup
set exitCodeOutOfDate=2

if exist %buildDll% (
    dotnet %buildDll% %*
    if errorlevel 2 (
       call :rebuild
    )
) else (
    call :rebuild
)
goto :eof

:Rebuild
    echo Building %buildDll%
    dotnet run --force -vd --project %~dp0build -- --ignore-clean %*
    goto :eof