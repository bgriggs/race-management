<#
.SYNOPSIS
    Publishes RaceManagementService and installs it as a Windows service on the local machine.

.DESCRIPTION
    - Self-elevates (UAC) because installing a service and writing to Program Files requires admin.
    - Publishes a self-contained, single-file win-x64 build of RaceManagementService.
    - If the service already exists it is stopped and removed, then re-created (clean replace).
    - The service runs as LocalSystem and starts automatically.
    - An existing SQLite database / logs in the install folder are preserved across re-installs.

.PARAMETER InstallPath
    Folder the published binaries are copied to and the service runs from.
    The SQLite db (race-management.db) and logs\ live here at runtime.

.PARAMETER Url
    HTTP endpoint Kestrel binds to (passed as --urls). Default http://0.0.0.0:5565.

.PARAMETER Uninstall
    Stop and remove the service, then exit (no publish/install).

.PARAMETER SkipPublish
    Reuse whatever is already in InstallPath instead of publishing (re-registers the service only).

.EXAMPLE
    .\Install-RaceManagementService.ps1

.EXAMPLE
    .\Install-RaceManagementService.ps1 -Url http://0.0.0.0:8080

.EXAMPLE
    .\Install-RaceManagementService.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [string]$ServiceName  = "Redmist Race Management",
    [string]$DisplayName  = "Redmist Race Management",
    [string]$Description   = "Redmist race management services to cars",
    [string]$InstallPath  = (Join-Path $env:ProgramFiles "Redmist\RaceManagementService"),
    [string]$Url          = "http://0.0.0.0:5565",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64",
    [switch]$Uninstall,
    [switch]$SkipPublish
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
    $host_exe = (Get-Process -Id $PID).Path   # pwsh.exe or powershell.exe - preserve edition
    $proc = Start-Process -FilePath $host_exe -Verb RunAs -ArgumentList $argList -PassThru -Wait
    exit $proc.ExitCode
}

# --- Resolve paths --------------------------------------------------------------
$ProjectPath = Join-Path $PSScriptRoot "RaceManagementService.csproj"
if (-not (Test-Path $ProjectPath)) {
    throw "Could not find project at '$ProjectPath'. Run this script from the RaceManagementService folder."
}
$StagingPath = Join-Path $PSScriptRoot "bin\ServicePublish"
$ExePath     = Join-Path $InstallPath "RaceManagementService.exe"

Write-Host "`nRedmist Race Management - service installer" -ForegroundColor Cyan
Write-Host "  Service name : $ServiceName"
Write-Host "  Install path : $InstallPath"
Write-Host "  Listen URL   : $Url"
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

    # sc.exe delete works on every PowerShell edition; deletion can be pending if a
    # handle (e.g. services.msc) is open, so poll until the service is actually gone.
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

# --- Publish --------------------------------------------------------------------
if (-not $SkipPublish) {
    Write-Host "`nPublishing ($Configuration, $Runtime, self-contained single-file)..." -ForegroundColor Cyan
    if (Test-Path $StagingPath) { Remove-Item $StagingPath -Recurse -Force }

    & dotnet publish $ProjectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $StagingPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

    # Copy published output into the install folder WITHOUT deleting an existing
    # race-management.db or logs\ that live there from a prior run.
    Write-Host "`nCopying to install folder..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
    Copy-Item -Path (Join-Path $StagingPath "*") -Destination $InstallPath -Recurse -Force
}

if (-not (Test-Path $ExePath)) {
    throw "Executable not found at '$ExePath'. Publish may have failed, or use without -SkipPublish."
}

# --- Create service -------------------------------------------------------------
Write-Host "`nCreating service..." -ForegroundColor Cyan
$binaryPath = "`"$ExePath`" --urls `"$Url`""
New-Service -Name $ServiceName `
            -BinaryPathName $binaryPath `
            -DisplayName $DisplayName `
            -Description $Description `
            -StartupType Automatic | Out-Null

# Pin the hosting environment for the service so it does not inherit a machine-level
# ASPNETCORE_ENVIRONMENT=Development. Stored as the service's per-process environment.
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
    Write-Host "  Endpoint : $Url"
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
