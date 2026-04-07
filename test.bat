@echo off
dotnet run --project src\Test.Automated\Test.Automated.csproj -c Debug -f net10.0
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%
dotnet test src\Test.XUnit\Test.XUnit.csproj -c Debug -f net10.0
