Set-Location "$PSScriptRoot\CourseInventory.Web"

$env:DOTNET_CLI_HOME = "$PSScriptRoot\.dotnet"
$env:NUGET_PACKAGES = "$PSScriptRoot\.nuget\packages"
$env:APPDATA = "$PSScriptRoot\.appdata"
$env:USERPROFILE = "$PSScriptRoot"

# PowerShell needs the whole connection string assigned to an environment variable.
# Replace YOUR_LOCAL_PASSWORD with the password you configured in PostgreSQL.
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=course_inventory;Username=course_inventory;Password=YOUR_LOCAL_PASSWORD"

# Optional: seed an admin account on first run.
# $env:SeedAdmin__Email = "admin@example.com"
# $env:SeedAdmin__Password = "ChangeThisPassword123!"

dotnet run
