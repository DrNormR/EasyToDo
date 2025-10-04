# PowerShell script to copy the entire project structure to the new EasyToDo folder
Write-Host "Copying EasyToDo project files..."

# Copy all files from to-do-list to EasyToDo, preserving directory structure
Copy-Item -Path "to-do-list\*" -Destination "EasyToDo\" -Recurse -Force

# Rename the project file
Move-Item -Path "EasyToDo\to-do-list.csproj" -Destination "EasyToDo\EasyToDo.csproj" -Force

Write-Host "Project structure copied successfully!"
Write-Host "New structure:"
Write-Host "  EasyToDo\"
Write-Host "    EasyToDo.csproj"
Write-Host "    Views\"
Write-Host "    Models\"
Write-Host "    Services\"
Write-Host "    Converters\"
Write-Host "    resources\"

Write-Host "`nYou can now build using: dotnet build EasyToDo\EasyToDo.csproj"