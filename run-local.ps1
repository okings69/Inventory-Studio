Set-Location "$PSScriptRoot\CourseInventory.Web"

$env:DOTNET_CLI_HOME = "$PSScriptRoot\.dotnet"
$env:NUGET_PACKAGES = "$PSScriptRoot\.nuget\packages"
$env:APPDATA = "$PSScriptRoot\.appdata"
$env:USERPROFILE = "$PSScriptRoot"

dotnet run
