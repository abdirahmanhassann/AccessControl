#Requires -RunAsAdministrator
<#
  One-time setup for the AccessControl Azure Windows VM.
  Run on the VM in an elevated PowerShell window:

    powershell -ExecutionPolicy Bypass -File setup-vm.ps1 -AppHost accesscontrol-demo.uksouth.cloudapp.azure.com

  It installs Node.js 24, Caddy (HTTPS) and WinSW (service wrapper), creates three Windows services
  (ACApi, ACWeb, ACCaddy), opens the firewall and writes a settings template for the API.
  Safe to run again. After this, deployments come from the GitHub pipelines.
#>
param(
    [Parameter(Mandatory)] [string] $AppHost,   # public name of the frontend
    [string] $ApiHost = "",                     # public name of the API; defaults to <AppHost>:8443
    [string] $Root = "C:\apps"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (-not $ApiHost) { $ApiHost = "${AppHost}:8443" }

$svcDir = Join-Path $Root "services"
$logDir = Join-Path $Root "logs"
foreach ($d in "api", "web", "caddy", "caddy\data", "services", "logs") {
    New-Item -ItemType Directory -Force -Path (Join-Path $Root $d) | Out-Null
}

# --- Node.js 24 (runs the frontend) ---
$node = "C:\Program Files\nodejs\node.exe"
if (-not (Test-Path $node)) {
    Write-Host "Installing Node.js 24..."
    $rel = (Invoke-RestMethod "https://nodejs.org/dist/index.json") | Where-Object { $_.version -like "v24.*" } | Select-Object -First 1
    $msi = Join-Path $env:TEMP "node-$($rel.version)-x64.msi"
    Invoke-WebRequest "https://nodejs.org/dist/$($rel.version)/node-$($rel.version)-x64.msi" -OutFile $msi
    Start-Process msiexec.exe -Wait -ArgumentList "/i `"$msi`" /qn"
}

# --- Caddy (HTTPS with free Let's Encrypt certificates) ---
$caddy = Join-Path $Root "caddy\caddy.exe"
if (-not (Test-Path $caddy)) {
    Write-Host "Downloading Caddy..."
    Invoke-WebRequest "https://caddyserver.com/api/download?os=windows&arch=amd64" -OutFile $caddy
}

$caddyfile = Join-Path $Root "caddy\Caddyfile"
@"
$AppHost {
	encode gzip
	reverse_proxy 127.0.0.1:3000
}

$ApiHost {
	encode gzip
	reverse_proxy 127.0.0.1:5000
}
"@ | Set-Content -Path $caddyfile -Encoding ASCII

# --- WinSW (turns any program into a Windows service) ---
$winsw = Join-Path $env:TEMP "WinSW-x64.exe"
if (-not (Test-Path $winsw)) {
    Write-Host "Downloading WinSW..."
    Invoke-WebRequest "https://github.com/winsw/winsw/releases/download/v2.12.0/WinSW-x64.exe" -OutFile $winsw
}

function Install-AppService {
    param([string]$Id, [string]$Name, [string]$Exe, [string]$Arguments, [string]$WorkDir, [hashtable]$Env)
    $wrapper = Join-Path $svcDir "$Id.exe"
    $xml     = Join-Path $svcDir "$Id.xml"
    if (Get-Service $Id -ErrorAction SilentlyContinue) {
        & $wrapper stop | Out-Null
        & $wrapper uninstall | Out-Null
    }
    Copy-Item $winsw $wrapper -Force
    $envLines = ($Env.GetEnumerator() | ForEach-Object { "  <env name=`"$($_.Key)`" value=`"$($_.Value)`" />" }) -join "`r`n"
    @"
<service>
  <id>$Id</id>
  <name>$Name</name>
  <description>$Name</description>
  <executable>$Exe</executable>
  <arguments>$Arguments</arguments>
  <workingdirectory>$WorkDir</workingdirectory>
$envLines
  <startmode>Automatic</startmode>
  <onfailure action="restart" delay="10 sec" />
  <logpath>$logDir</logpath>
  <log mode="roll-by-size"><sizeThreshold>10240</sizeThreshold><keepFiles>5</keepFiles></log>
</service>
"@ | Set-Content -Path $xml -Encoding UTF8
    & $wrapper install | Out-Host
}

Install-AppService -Id "ACApi" -Name "AccessControl API" `
    -Exe (Join-Path $Root "api\AccessControl.exe") -Arguments "" -WorkDir (Join-Path $Root "api") `
    -Env @{ ASPNETCORE_URLS = "http://127.0.0.1:5000"; ASPNETCORE_ENVIRONMENT = "Production" }

Install-AppService -Id "ACWeb" -Name "AccessControl Frontend" `
    -Exe $node -Arguments (Join-Path $Root "web\server\index.mjs") -WorkDir (Join-Path $Root "web") `
    -Env @{ NODE_ENV = "production"; HOST = "127.0.0.1"; PORT = "3000" }

Install-AppService -Id "ACCaddy" -Name "AccessControl HTTPS proxy (Caddy)" `
    -Exe $caddy -Arguments "run --config `"$caddyfile`" --adapter caddyfile" -WorkDir (Join-Path $Root "caddy") `
    -Env @{ XDG_DATA_HOME = (Join-Path $Root "caddy\data") }

# --- API settings that stay on the VM (the pipeline never overwrites this file) ---
$local = Join-Path $Root "api\appsettings.Local.json"
if (-not (Test-Path $local)) {
    @"
{
  "ConnectionStrings": {
    "AccessControlDB": "Server=localhost\\SQLEXPRESS;Database=AccessControl;User Id=accesscontrol;Password=CHANGE_ME;TrustServerCertificate=True;"
  },
  "PublicApiBaseUrl": "https://$ApiHost",
  "Security": { "EncryptionKey": "CHANGE_ME (use the same key as your local user-secrets if you copy existing users across)" },
  "Twilio": false,
  "TWILIO_ACCOUNT_SID": "",
  "TWILIO_AUTH_TOKEN": "",
  "TWILIO_FROM_NUMBER": "",
  "TWILIO_VERIFY_SERVICE_SID": ""
}
"@ | Set-Content -Path $local -Encoding ASCII
    Write-Host "Wrote $local - fill it in before the first deploy." -ForegroundColor Yellow
}

# --- Windows firewall (also open these in the Azure network security group) ---
if (-not (Get-NetFirewallRule -DisplayName "AccessControl web" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName "AccessControl web" -Direction Inbound -Protocol TCP -LocalPort 80, 443, 8443 -Action Allow | Out-Null
}

Start-Service ACCaddy
Write-Host ""
Write-Host "Done. Services: ACApi, ACWeb (start on first deploy), ACCaddy (running)." -ForegroundColor Green
Write-Host "Frontend: https://$AppHost    API: https://$ApiHost"
Write-Host "Next: fill in $local, then register the GitHub runners (README step 5)."
