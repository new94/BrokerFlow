# ============================================================================
# BrokerFlow — Windows Server Installation Script
# Run as Administrator in PowerShell
# ============================================================================

param(
    [string]$InstallDir = "C:\BrokerFlow",
    [string]$SqlServer = "localhost",
    [string]$SqlDatabase = "BrokerFlow",
    [string]$SqlUser = "",
    [string]$SqlPassword = "",
    [switch]$TrustedConnection,
    [int]$Port = 5000,
    [switch]$InstallAsService
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  BrokerFlow Installation Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# ── 1. Check prerequisites ────────────────────────────────────────────────────
Write-Host "[1/7] Checking prerequisites..." -ForegroundColor Yellow

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    exit 1
}

# Check .NET 8 SDK/Runtime
$dotnetVersion = dotnet --version 2>$null
if (-not $dotnetVersion) {
    Write-Host "ERROR: .NET 8 SDK is not installed. Download from https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}
Write-Host "  .NET version: $dotnetVersion" -ForegroundColor Green

# ── 2. Create directory structure ─────────────────────────────────────────────
Write-Host "[2/7] Creating directory structure..." -ForegroundColor Yellow

$dirs = @(
    $InstallDir,
    "$InstallDir\app",
    "$InstallDir\reports",
    "$InstallDir\output",
    "$InstallDir\uploads",
    "$InstallDir\logs"
)

foreach ($dir in $dirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Gray
    }
}

# ── 3. Build the application ──────────────────────────────────────────────────
Write-Host "[3/7] Building application..." -ForegroundColor Yellow

$sourceDir = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $sourceDir "BrokerFlow.Api\BrokerFlow.Api.csproj"

if (-not (Test-Path $projectFile)) {
    Write-Host "ERROR: Project file not found at $projectFile" -ForegroundColor Red
    Write-Host "  Make sure to run this script from the Scripts directory" -ForegroundColor Red
    exit 1
}

dotnet publish $projectFile `
    -c Release `
    -o "$InstallDir\app" `
    --self-contained false `
    -r win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  Build successful" -ForegroundColor Green

# ── 4. Configure connection string ────────────────────────────────────────────
Write-Host "[4/7] Configuring application..." -ForegroundColor Yellow

if ($TrustedConnection) {
    $connString = "Server=$SqlServer;Database=$SqlDatabase;Trusted_Connection=True;TrustServerCertificate=True;"
} elseif ($SqlUser) {
    $connString = "Server=$SqlServer;Database=$SqlDatabase;User Id=$SqlUser;Password=$SqlPassword;TrustServerCertificate=True;Encrypt=True;"
} else {
    $connString = "Server=$SqlServer;Database=$SqlDatabase;Trusted_Connection=True;TrustServerCertificate=True;"
}

$appSettings = @{
    ConnectionStrings = @{
        DefaultConnection = $connString
    }
    Paths = @{
        Base = $InstallDir
        Reports = "$InstallDir\reports"
        Output = "$InstallDir\output"
        Uploads = "$InstallDir\uploads"
    }
    Logging = @{
        LogLevel = @{
            Default = "Information"
            "Microsoft.AspNetCore" = "Warning"
        }
    }
    AllowedHosts = "*"
    Kestrel = @{
        Endpoints = @{
            Http = @{
                Url = "http://0.0.0.0:$Port"
            }
        }
    }
} | ConvertTo-Json -Depth 5

$appSettings | Out-File -FilePath "$InstallDir\app\appsettings.Production.json" -Encoding utf8
Write-Host "  Configuration written to appsettings.Production.json" -ForegroundColor Green
Write-Host "  Connection: $SqlServer / $SqlDatabase" -ForegroundColor Gray

# ── 5. Create database ────────────────────────────────────────────────────────
Write-Host "[5/7] Creating database (if not exists)..." -ForegroundColor Yellow

$sqlScript = Join-Path $sourceDir "Scripts\init-database.sql"
if (Test-Path $sqlScript) {
    try {
        if ($TrustedConnection -or -not $SqlUser) {
            sqlcmd -S $SqlServer -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'$SqlDatabase') CREATE DATABASE [$SqlDatabase]" 2>$null
            sqlcmd -S $SqlServer -d $SqlDatabase -i $sqlScript 2>$null
        } else {
            sqlcmd -S $SqlServer -U $SqlUser -P $SqlPassword -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'$SqlDatabase') CREATE DATABASE [$SqlDatabase]" 2>$null
            sqlcmd -S $SqlServer -U $SqlUser -P $SqlPassword -d $SqlDatabase -i $sqlScript 2>$null
        }
        Write-Host "  Database initialized" -ForegroundColor Green
    } catch {
        Write-Host "  WARNING: Could not run SQL script. The application will auto-migrate on first start." -ForegroundColor Yellow
    }
} else {
    Write-Host "  SQL script not found. EF Core will auto-migrate on startup." -ForegroundColor Yellow
}

# ── 6. Configure firewall ────────────────────────────────────────────────────
Write-Host "[6/7] Configuring firewall..." -ForegroundColor Yellow

$ruleName = "BrokerFlow-HTTP-$Port"
$existingRule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (-not $existingRule) {
    New-NetFirewallRule -DisplayName $ruleName `
        -Direction Inbound -Protocol TCP -LocalPort $Port `
        -Action Allow -Profile Domain,Private | Out-Null
    Write-Host "  Firewall rule created for port $Port" -ForegroundColor Green
} else {
    Write-Host "  Firewall rule already exists" -ForegroundColor Gray
}

# ── 7. Install as Windows Service (optional) ─────────────────────────────────
if ($InstallAsService) {
    Write-Host "[7/7] Installing Windows Service..." -ForegroundColor Yellow
    
    $serviceName = "BrokerFlowService"
    $serviceExe = "$InstallDir\app\BrokerFlow.Api.exe"
    
    # Stop existing service
    $existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existing) {
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $serviceName 2>$null
        Start-Sleep -Seconds 2
    }
    
    # Create the service
    New-Service -Name $serviceName `
        -BinaryPathName $serviceExe `
        -DisplayName "BrokerFlow Report Processor" `
        -Description "Processes broker reports and generates XML outputs for accounting systems" `
        -StartupType Automatic | Out-Null
    
    # Set environment
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName"
    $envs = @("ASPNETCORE_ENVIRONMENT=Production", "DOTNET_ENVIRONMENT=Production")
    Set-ItemProperty -Path $regPath -Name "Environment" -Value $envs -Type MultiString
    
    # Start the service
    Start-Service -Name $serviceName
    Write-Host "  Service '$serviceName' installed and started" -ForegroundColor Green
} else {
    Write-Host "[7/7] Skipping service installation (use -InstallAsService to enable)" -ForegroundColor Gray
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Install directory:  $InstallDir" -ForegroundColor White
Write-Host "  Reports directory:  $InstallDir\reports" -ForegroundColor White
Write-Host "  Output directory:   $InstallDir\output" -ForegroundColor White
Write-Host "  Application URL:    http://localhost:$Port" -ForegroundColor Cyan
Write-Host "  Swagger API docs:   http://localhost:$Port/swagger" -ForegroundColor Cyan
Write-Host ""

if (-not $InstallAsService) {
    Write-Host "  To start manually:" -ForegroundColor Yellow
    Write-Host "    cd $InstallDir\app" -ForegroundColor White
    Write-Host "    set ASPNETCORE_ENVIRONMENT=Production" -ForegroundColor White
    Write-Host "    BrokerFlow.Api.exe" -ForegroundColor White
    Write-Host ""
    Write-Host "  To install as a Windows Service, re-run with -InstallAsService" -ForegroundColor Yellow
}
