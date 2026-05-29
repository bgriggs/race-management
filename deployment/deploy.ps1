# Deploy Race Management cloud services to Kubernetes.
#
# Examples:
#   ./deploy.ps1 -Environment prod -Version 0.3.1
#   ./deploy.ps1 -Environment dev                      # defaults Version to "latest"
#   ./deploy.ps1 -Environment test -Namespace rm-qa    # override the default namespace
#   ./deploy.ps1 -Environment prod -Version 0.3.1 -DryRun
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment,

    # Container image tag to deploy across all three services.
    [string]$Version = "latest",

    # Override the namespace that the environment maps to (optional).
    [string]$Namespace,

    # Render and diff without applying (passes --dry-run to helm).
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Environment -> deployment target. Namespace can be overridden with -Namespace.
$config = switch ($Environment) {
    "dev"  { @{ namespace = "race-management-dev";  releaseName = "race-management-dev";  color = "Green"  } }
    "test" { @{ namespace = "race-management-test"; releaseName = "race-management-test"; color = "Yellow" } }
    "prod" { @{ namespace = "race-management";      releaseName = "race-management";      color = "Red"    } }
}
if ($Namespace) { $config.namespace = $Namespace }

$chartDir = $PSScriptRoot

Write-Host "`nDeploying Race Management to $($Environment.ToUpper())" -ForegroundColor $config.color
Write-Host "  Chart:     $chartDir"            -ForegroundColor Cyan
Write-Host "  Release:   $($config.releaseName)" -ForegroundColor Cyan
Write-Host "  Namespace: $($config.namespace)"   -ForegroundColor Cyan
Write-Host "  Version:   $Version"               -ForegroundColor Cyan
if ($DryRun) { Write-Host "  Mode:      DRY RUN (no changes applied)" -ForegroundColor Yellow }

# --- Kubernetes context check ---------------------------------------------------
$k8sContext = kubectl config current-context 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "X  Failed to get Kubernetes context. Is kubectl configured?" -ForegroundColor Red
    exit 1
}
Write-Host "`nCurrent kubectl context: " -ForegroundColor Cyan -NoNewline
Write-Host $k8sContext -ForegroundColor White

$contextConfirm = Read-Host "Is this the correct context for '$Environment'? (yes/no)"
if ($contextConfirm -ne "yes") {
    Write-Host "Deployment cancelled. Switch with: kubectl config use-context <context-name>" -ForegroundColor Yellow
    exit 1
}

# --- Namespace check ------------------------------------------------------------
kubectl get namespace $config.namespace *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Namespace '$($config.namespace)' does not exist; it will be created (--create-namespace)." -ForegroundColor Yellow
} else {
    Write-Host "Namespace '$($config.namespace)' exists." -ForegroundColor Cyan
}

# Reminder: the chart expects the race-management-secrets secret (db / password / redis).
kubectl get secret race-management-secrets -n $config.namespace *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "!  Secret 'race-management-secrets' not found in '$($config.namespace)'." -ForegroundColor Yellow
    Write-Host "   Redis (operator requirepass) and Postgres will fail without it. See README.md." -ForegroundColor Yellow
    $secretConfirm = Read-Host "Continue anyway? (yes/no)"
    if ($secretConfirm -ne "yes") {
        Write-Host "Deployment cancelled." -ForegroundColor Yellow
        exit 1
    }
}

# --- Production safety check ----------------------------------------------------
if ($Environment -eq "prod") {
    $confirm = Read-Host "Are you sure you want to deploy to PRODUCTION? (yes/no)"
    if ($confirm -ne "yes") {
        Write-Host "Production deployment cancelled." -ForegroundColor Yellow
        exit 1
    }
}

# --- Chart dependencies ---------------------------------------------------------
Write-Host "`nBuilding chart dependencies..." -ForegroundColor Cyan
helm dependency build $chartDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "X  helm dependency build FAILED. Try: helm dependency update $chartDir" -ForegroundColor Red
    exit 1
}

# --- Deploy ---------------------------------------------------------------------
$helmArgs = @(
    "upgrade", "--install", $config.releaseName, $chartDir,
    "--namespace", $config.namespace,
    "--create-namespace",
    "--set", "race-management-web-api.image.tag=$Version",
    "--set", "race-management-car-gateway.image.tag=$Version",
    "--set", "race-management-channel-processor.image.tag=$Version",
    "--set", "race-management-web-api.ingress.environment=$Environment",
    "--set", "race-management-car-gateway.ingress.environment=$Environment"
)

# Optional per-environment overlay (e.g. values-prod.yaml), applied only if present.
$valuesFile = Join-Path $chartDir "values-$Environment.yaml"
if (Test-Path $valuesFile) {
    Write-Host "Using overlay: $valuesFile" -ForegroundColor Cyan
    $helmArgs += @("-f", $valuesFile)
}

if ($DryRun) {
    $helmArgs += "--dry-run"
} else {
    $helmArgs += "--wait"
}

Write-Host "`nRunning: helm $($helmArgs -join ' ')" -ForegroundColor DarkGray
helm @helmArgs

if ($LASTEXITCODE -eq 0) {
    if ($DryRun) {
        Write-Host "`nDry run complete (nothing applied)." -ForegroundColor $config.color
    } else {
        Write-Host "`n$($Environment.ToUpper()) deployment complete." -ForegroundColor $config.color
        Write-Host "`nCheck rollout:" -ForegroundColor Yellow
        Write-Host "  kubectl get pods -n $($config.namespace)" -ForegroundColor White
        Write-Host "  kubectl get redisfailover -n $($config.namespace)" -ForegroundColor White
    }
} else {
    Write-Host "`nX  $($Environment.ToUpper()) deployment FAILED." -ForegroundColor Red
    Write-Host "Debug:" -ForegroundColor Yellow
    Write-Host "  kubectl get events -n $($config.namespace) --sort-by=.lastTimestamp" -ForegroundColor White
    Write-Host "  kubectl describe redisfailover -n $($config.namespace)" -ForegroundColor White
    exit 1
}
