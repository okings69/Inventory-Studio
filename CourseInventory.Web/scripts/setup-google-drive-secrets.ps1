param(
    [string]$FolderId
)

$ErrorActionPreference = "Stop"

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Fail {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
$credentialPath = Join-Path $projectRoot ".secrets\google-service-account.json"

Write-Info "Inventory Studio Google Drive setup"
Write-Info "Expected credential file: $credentialPath"

if (-not (Test-Path -LiteralPath $credentialPath -PathType Leaf)) {
    Fail "Missing .secrets\google-service-account.json. Download a Google service account JSON key, rename it, and place it there."
}

try {
    $rawJson = Get-Content -LiteralPath $credentialPath -Raw
    $credential = $rawJson | ConvertFrom-Json -ErrorAction Stop
    Write-Success "Credential JSON parsed successfully."
}
catch {
    Fail "Google Drive JSON credential is invalid. $($_.Exception.Message)"
}

$propertyNames = @($credential.PSObject.Properties.Name)

if (($propertyNames -contains "reportedBy") -and ($propertyNames -contains "summary")) {
    Fail "Wrong Google Drive credential file: this is a support ticket JSON, not a service account key."
}

if (-not ($propertyNames -contains "type") -or [string]::IsNullOrWhiteSpace($credential.type)) {
    Fail "Google Drive credential type is missing."
}

if ($credential.type -ne "service_account") {
    Fail "Google Drive service account credential is required. Found type '$($credential.type)'."
}

$requiredProperties = @("client_email", "project_id", "private_key")
foreach ($property in $requiredProperties) {
    if (-not ($propertyNames -contains $property) -or [string]::IsNullOrWhiteSpace($credential.$property)) {
        Fail "Google Drive service account credential is missing required property '$property'."
    }
}

Write-Success "Detected service account credential."
Write-Info "ProjectId: $($credential.project_id)"
Write-Info "ClientEmail: $($credential.client_email)"

if ([string]::IsNullOrWhiteSpace($FolderId)) {
    $FolderId = Read-Host "Google Drive support tickets folder ID"
}

if ([string]::IsNullOrWhiteSpace($FolderId)) {
    Fail "Google Drive folder ID is required."
}

$base64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($rawJson))

Write-Info "Writing GoogleDrive:ServiceAccountJsonBase64 to user-secrets. The Base64 value will not be printed."
dotnet user-secrets set "GoogleDrive:ServiceAccountJsonBase64" $base64 --project $projectRoot | Out-Null
if ($LASTEXITCODE -ne 0) {
    Fail "Could not write GoogleDrive:ServiceAccountJsonBase64 to user-secrets."
}

Write-Info "Writing GoogleDrive:SupportTicketsFolderId to user-secrets."
dotnet user-secrets set "GoogleDrive:SupportTicketsFolderId" $FolderId --project $projectRoot | Out-Null
if ($LASTEXITCODE -ne 0) {
    Fail "Could not write GoogleDrive:SupportTicketsFolderId to user-secrets."
}

Write-Success "Google Drive user-secrets configured."
Write-Info "Restart the app: dotnet run --urls http://localhost:5158"
