@echo off
echo Building EasyToDo Release...

rem Clean and build the project
dotnet clean EasyToDo\EasyToDo.csproj
dotnet build EasyToDo\EasyToDo.csproj --configuration Release

rem Publish self-contained executable
dotnet publish EasyToDo\EasyToDo.csproj --configuration Release --output .\release --self-contained true --runtime win-x64

echo Build complete! Check the 'release' folder for EasyToDo.exe.
pause