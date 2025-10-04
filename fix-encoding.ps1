# PowerShell script to fix encoding issues in EasyToDo project

Write-Host "?? Fixing Encoding Issues in EasyToDo Project..."

# Delete potentially problematic build directories
$buildDirs = @("EasyToDo\bin", "EasyToDo\obj")
foreach ($dir in $buildDirs) {
    if (Test-Path $dir) {
        Remove-Item -Path $dir -Recurse -Force
        Write-Host "? Deleted: $dir"
    }
}

# Check for any hidden files that might have encoding issues
$files = Get-ChildItem -Path "EasyToDo" -Recurse -File -Include "*.xaml", "*.cs", "*.csproj"
foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    if ($content -match '[^\x00-\x7F]') {
        Write-Host "??  Non-ASCII characters found in: $($file.Name)"
    }
}

Write-Host ""
Write-Host "?? Recommended Actions:"
Write-Host "1. Try running: dotnet clean EasyToDo\EasyToDo.csproj"
Write-Host "2. Then run: dotnet build EasyToDo\EasyToDo.csproj"
Write-Host "3. If error persists, there may be copied files with encoding issues"

Write-Host ""
Write-Host "?? Alternative Solution:"
Write-Host "Use the to-do-list project which builds successfully:"
Write-Host "   dotnet build to-do-list\to-do-list.csproj"
Write-Host "   (This already has EasyToDo branding and orange icon)"