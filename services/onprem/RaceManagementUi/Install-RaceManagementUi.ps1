<#
.SYNOPSIS
    Builds the race-management-local Angular app, publishes the RaceManagementUi host,
    and installs it as a Windows service that serves the SPA and reverse-proxies the API.

.DESCRIPTION
    - Self-elevates (UAC); installing a service and writing to Program Files requires admin.
    - Runs "npm run build:local" to produce the Angular bundle.
    - Publishes RaceManagementUi (self-contained, single-file, win-x64) and copies the
      Angular output into the published wwwroot.
    - Rewrites the deployed site-settings.json so the browser calls the API same-origin
      (managementDataServiceBaseUrl = "") - those /v1.0/* calls are reverse-proxied to
      the RaceManagementService backend, so no CORS configuration is needed.
    - If the service already exists it is stopped and removed, then re-created.
    - The service runs as LocalSystem and starts automatically.

.PARAMETER Url
    Address the UI host binds to (passed as --urls). Default http://localhost:8080.

.PARAMETER BackendUrl
    RaceManagementService address the host reverse-proxies /v1.0/* to. Default http://localhost:5565.

.PARAMETER Uninstall
    Stop and remove the service, then exit (no build/publish/install).

.PARAMETER SkipUiBuild
    Reuse the existing Angular build output instead of running npm.

.EXAMPLE
    .\Install-RaceManagementUi.ps1

.EXAMPLE
    .\Install-RaceManagementUi.ps1 -Url http://0.0.0.0:8080 -BackendUrl http://localhost:5565

.EXAMPLE
    .\Install-RaceManagementUi.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [string]$ServiceName   = "Redmist Race Management UI",
    [string]$DisplayName   = "Redmist Race Management UI",
    [string]$Description    = "Redmist race management web interface to cars",
    [string]$InstallPath   = (Join-Path $env:ProgramFiles "Redmist\RaceManagementUi"),
    [string]$Url           = "http://localhost:8080",
    [string]$BackendUrl    = "http://localhost:5565",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration  = "Release",
    [string]$Runtime        = "win-x64",
    [switch]$Uninstall,
    [switch]$SkipUiBuild
)

$ErrorActionPreference = "Stop"

# --- Self-elevate ---------------------------------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Elevation required - relaunching with administrator rights (UAC)..." -ForegroundColor Yellow
    $argList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"")
    foreach ($kv in $PSBoundParameters.GetEnumerator()) {
        if ($kv.Value -is [switch]) {
            if ($kv.Value.IsPresent) { $argList += "-$($kv.Key)" }
        } else {
            $argList += "-$($kv.Key)"; $argList += "`"$($kv.Value)`""
        }
    }
    $hostExe = (Get-Process -Id $PID).Path   # pwsh.exe or powershell.exe - preserve edition
    $proc = Start-Process -FilePath $hostExe -Verb RunAs -ArgumentList $argList -PassThru -Wait
    exit $proc.ExitCode
}

# --- Resolve paths --------------------------------------------------------------
$ProjectPath = Join-Path $PSScriptRoot "RaceManagementUi.csproj"
$RepoRoot    = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$UiDir       = Join-Path $RepoRoot "ui"
$AngularOut  = Join-Path $UiDir "dist\race-management-local\browser"
$StagingPath = Join-Path $PSScriptRoot "bin\ServicePublish"
$ExePath     = Join-Path $InstallPath "RaceManagementUi.exe"

if (-not (Test-Path $ProjectPath)) {
    throw "Could not find project at '$ProjectPath'. Run this script from the RaceManagementUi folder."
}

Write-Host "`nRedmist Race Management UI - service installer" -ForegroundColor Cyan
Write-Host "  Service name : $ServiceName"
Write-Host "  Install path : $InstallPath"
Write-Host "  Listen URL   : $Url"
Write-Host "  API backend  : $BackendUrl  (reverse-proxied at /v1.0/*)"
Write-Host "  Run-as       : LocalSystem"

# --- Remove existing service ----------------------------------------------------
function Remove-ExistingService {
    param([string]$Name)

    $svc = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $svc) { return }

    Write-Host "`nExisting service found - stopping and removing..." -ForegroundColor Yellow
    if ($svc.Status -ne "Stopped") {
        Stop-Service -Name $Name -Force -ErrorAction SilentlyContinue
        try { $svc.WaitForStatus("Stopped", "00:00:30") } catch {
            throw "Service '$Name' did not stop within 30s. Stop it manually and retry."
        }
    }

    & sc.exe delete "$Name" | Out-Null
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Service -Name $Name -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }
    if (Get-Service -Name $Name -ErrorAction SilentlyContinue) {
        throw "Service '$Name' is still present after delete (a handle may be open in services.msc). Close it and retry."
    }
    Write-Host "  Removed." -ForegroundColor Green
}

Remove-ExistingService -Name $ServiceName

if ($Uninstall) {
    Write-Host "`nUninstall complete. (Install folder '$InstallPath' left in place.)" -ForegroundColor Green
    exit 0
}

# --- Build Angular --------------------------------------------------------------
if (-not $SkipUiBuild) {
    Write-Host "`nBuilding Angular app (npm run build:local)..." -ForegroundColor Cyan
    if (-not (Test-Path (Join-Path $UiDir "node_modules"))) {
        throw "node_modules not found in '$UiDir'. Run 'npm ci' there first, or pass -SkipUiBuild."
    }
    Push-Location $UiDir
    try {
        & npm run build:local
        if ($LASTEXITCODE -ne 0) { throw "Angular build failed (exit $LASTEXITCODE)." }
    } finally { Pop-Location }
}
if (-not (Test-Path (Join-Path $AngularOut "index.html"))) {
    throw "Angular output not found at '$AngularOut'. Build the app or omit -SkipUiBuild."
}

# --- Publish host ---------------------------------------------------------------
Write-Host "`nPublishing host ($Configuration, $Runtime, self-contained single-file)..." -ForegroundColor Cyan
if (Test-Path $StagingPath) { Remove-Item $StagingPath -Recurse -Force }

& dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $StagingPath
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

# --- Inject SPA + configure -----------------------------------------------------
Write-Host "Copying Angular bundle into wwwroot..." -ForegroundColor Cyan
$wwwroot = Join-Path $StagingPath "wwwroot"
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item -Path (Join-Path $AngularOut "*") -Destination $wwwroot -Recurse -Force

# Point the browser at the API same-origin so /v1.0/* requests are reverse-proxied
# (avoids CORS). Keycloak settings are left untouched.
$siteSettingsPath = Join-Path $wwwroot "site-settings.json"
if (Test-Path $siteSettingsPath) {
    $site = Get-Content $siteSettingsPath -Raw | ConvertFrom-Json
    $site.managementDataServiceBaseUrl = ""
    $site | ConvertTo-Json -Depth 10 | Set-Content $siteSettingsPath -Encoding utf8
}

# Set the reverse-proxy backend address in the deployed appsettings.json.
$appsettingsPath = Join-Path $StagingPath "appsettings.json"
$appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
$appsettings.ReverseProxy.Clusters.raceManagement.Destinations.primary.Address = $BackendUrl
$appsettings | ConvertTo-Json -Depth 20 | Set-Content $appsettingsPath -Encoding utf8

# --- Copy to install folder -----------------------------------------------------
Write-Host "Copying to install folder..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
# Replace wwwroot wholesale (drop stale hashed bundles) but keep logs\ from prior runs.
$installWwwroot = Join-Path $InstallPath "wwwroot"
if (Test-Path $installWwwroot) { Remove-Item $installWwwroot -Recurse -Force }
Copy-Item -Path (Join-Path $StagingPath "*") -Destination $InstallPath -Recurse -Force

if (-not (Test-Path $ExePath)) {
    throw "Executable not found at '$ExePath'. Publish may have failed."
}

# --- Create service -------------------------------------------------------------
Write-Host "`nCreating service..." -ForegroundColor Cyan
$binaryPath = "`"$ExePath`" --urls `"$Url`""
New-Service -Name $ServiceName `
            -BinaryPathName $binaryPath `
            -DisplayName $DisplayName `
            -Description $Description `
            -StartupType Automatic | Out-Null

# Pin the hosting environment so the service does not inherit ASPNETCORE_ENVIRONMENT=Development.
$svcKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
New-ItemProperty -Path $svcKey -Name "Environment" -PropertyType MultiString `
                 -Value @("ASPNETCORE_ENVIRONMENT=Production") -Force | Out-Null

# Restart automatically on failure (reset failure count daily; 5s between restarts).
& sc.exe failure "$ServiceName" reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

# --- Start ----------------------------------------------------------------------
Write-Host "Starting service..." -ForegroundColor Cyan
Start-Service -Name $ServiceName
$svc = Get-Service -Name $ServiceName
try { $svc.WaitForStatus("Running", "00:00:30") } catch { }

if ($svc.Status -eq "Running") {
    Write-Host "`nService '$ServiceName' is running." -ForegroundColor Green
    Write-Host "  UI       : $Url"
    Write-Host "  API proxy: $Url/v1.0/*  ->  $BackendUrl"
    Write-Host "  Logs     : $(Join-Path $InstallPath 'logs')"
    Write-Host "  Manage   : Get-Service '$ServiceName' | Stop-Service / Start-Service"
} else {
    Write-Host "`nService was created but is not running (status: $($svc.Status))." -ForegroundColor Red
    $logDir = Join-Path $InstallPath "logs"
    $latest = Get-ChildItem $logDir -Filter *.log -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        Write-Host "Last lines of $($latest.FullName):" -ForegroundColor Yellow
        Get-Content $latest.FullName -Tail 20
    } else {
        Write-Host "No log file found yet under $logDir. Check Event Viewer (Windows Logs > Application)." -ForegroundColor Yellow
    }
    exit 1
}
