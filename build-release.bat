@echo off
echo Building EasyToDo Release...

rem Clean and build the project
dotnet clean to-do-list\to-do-list.csproj
dotnet build to-do-list\to-do-list.csproj --configuration Release

rem Publish self-contained executable
dotnet publish to-do-list\to-do-list.csproj --configuration Release --output .\release --self-contained true --runtime win-x64

echo Build complete! Check the 'release' folder for the executable.
pause