# PowerShell script to copy the icon file
$sourceIcon = "to-do-list\resources\post-it-orange.ico"
$targetDir = "EasyToDo\resources"
$targetIcon = "$targetDir\post-it-orange.ico"

Write-Host "Copying icon file..."

# Create the resources directory if it doesn't exist
if (-Not (Test-Path -Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force
    Write-Host "Created resources directory: $targetDir"
}

# Copy the icon file
if (Test-Path -Path $sourceIcon) {
    Copy-Item -Path $sourceIcon -Destination $targetIcon -Force
    Write-Host "Successfully copied icon: $sourceIcon -> $targetIcon"
} else {
    Write-Host "Source icon not found: $sourceIcon"
    Write-Host "Please manually copy the icon file from to-do-list\resources\ to EasyToDo\resources\"
}

Write-Host "Icon copy operation completed."