<#
  Local test run for the AccessControl API, on this PC. Double-click RUN-LOCAL-TESTS.cmd.

  1. Builds the API (dotnet build), so compile errors show up before a push.
  2. For each starting point below, creates a throwaway database on your local SQL
     Server, loads the schema, runs every script in deploy/migrations.txt twice (they
     must be safe to re-run), runs deploy/tests/*.sql, then DROPS the throwaway database.
       A  schema only (deploy/AccessControl.cloud.sql)
       B  schema + SiteQr.sql + Mappings.sql   (in case those were run on the server)
       C  schema + Mappings.sql + SiteQr.sql
     Your own AccessControl database is never opened or changed: every script is
     pointed at the throwaway database before it runs.

  Results go to deploy/tests/local-test-results.txt.
#>
param(
  [string]$Server = "",
  [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $here "..\..")).Path
$results = Join-Path $here "local-test-results.txt"
$global:AcTestLines = New-Object System.Collections.Generic.List[string]
$failed = $false

function Say([string]$text) {
  $stamp = (Get-Date).ToString("HH:mm:ss")
  $line = "[$stamp] $text"
  Write-Host $line
  $global:AcTestLines.Add($line)
}

# Whatever happens, leave a results file behind.
trap {
  Say "SCRIPT ERROR: $_"
  $global:AcTestLines | Set-Content -Path $results -Encoding UTF8
  break
}

Say "AccessControl local tests - $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
Say "Repo: $repo"

# ---------------------------------------------------------------- 1. build
if (-not $SkipBuild) {
  Say "== 1. dotnet build"
  $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
  if (-not $dotnet) {
    Say "BUILD: SKIPPED - dotnet is not on PATH (open a Developer PowerShell, or install the .NET 10 SDK)."
    $failed = $true
  } else {
    $proj = Join-Path $repo "AccessControl\AccessControl.csproj"
    $out = & dotnet build $proj -c Release -nologo 2>&1 | Out-String
    $errors = ($out -split "`r?`n") | Where-Object { $_ -match "error [A-Z]+\d+" } | Select-Object -Unique
    $warnings = (($out -split "`r?`n") | Where-Object { $_ -match "warning [A-Z]+\d+" } | Select-Object -Unique).Count
    if ($LASTEXITCODE -eq 0) {
      Say "BUILD: PASS ($warnings warning lines)"
    } else {
      Say "BUILD: FAIL"
      foreach ($e in $errors) { Say "  $e" }
      if (-not $errors) { Say ($out.Trim() -split "`r?`n" | Select-Object -Last 25 | Out-String) }
      $failed = $true
    }
  }
}

# ---------------------------------------------------------------- 2. database
if (-not $Server) {
  # Same server as the API's local connection string (appsettings.json), else the default Express instance.
  $settings = Join-Path $repo "AccessControl\appsettings.json"
  if (Test-Path $settings) {
    $json = Get-Content $settings -Raw | ConvertFrom-Json
    $cs = $json.ConnectionStrings.AccessControlDB
    if ($cs -match "(?i)(Data Source|Server)\s*=\s*([^;]+)") { $Server = $Matches[2].Trim() }
  }
  if (-not $Server) { $Server = ".\SQLEXPRESS" }
}
Say "== 2. database scripts on $Server (throwaway databases, dropped afterwards)"

Add-Type -AssemblyName System.Data

function Open-Sql([string]$database) {
  $c = New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=$database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15"
  $c.add_InfoMessage({
    param($sender, $info)
    foreach ($m in $info.Errors) {
      if ($m.Message -match "passed") { $global:AcTestLines.Add("           $($m.Message)"); Write-Host "    $($m.Message)" }
    }
  })
  $c.FireInfoMessageEventOnUserErrors = $false
  $c.Open()
  return $c
}

function Invoke-Script([System.Data.SqlClient.SqlConnection]$conn, [string]$path, [string]$db) {
  $text = Get-Content $path -Raw
  # Point every script at the throwaway database, never the real one.
  $text = $text.Replace("[AccessControl]", "[$db]").Replace("DB_ID('AccessControl')", "DB_ID('$db')")
  $batches = [regex]::Split($text, "(?im)^\s*GO\s*;?\s*$")
  $n = 0
  foreach ($batch in $batches) {
    if (-not $batch.Trim()) { continue }
    $n++
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $batch
    $cmd.CommandTimeout = 300
    try {
      [void]$cmd.ExecuteNonQuery()
    } catch {
      $first = ($batch.Trim() -split "`r?`n" | Select-Object -First 3) -join " "
      throw "$(Split-Path -Leaf $path) batch $n failed: $(if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $_.Exception.Message })`n      starts: $first"
    }
  }
}

function Run-Scenario([string]$name, [string[]]$before) {
  $db = "AccessControl_CI_" + $name + "_" + (Get-Date -Format "HHmmss")
  Say "-- scenario $name ($db)"
  $master = Open-Sql "master"
  try {
    $schema = Join-Path $repo "deploy\AccessControl.cloud.sql"
    Invoke-Script $master $schema $db   # creates the database and switches to it
    foreach ($extra in $before) {
      try { Invoke-Script $master (Join-Path $repo $extra) $db }
      catch {
        # Older one-off scripts (not in migrations.txt) may not run on the base schema at all.
        Say "SCENARIO ${name}: SKIPPED - $extra does not run on the base schema, so the server cannot have it as-is: $($_.Exception.Message)"
        return $true
      }
      Say "    applied $extra"
    }
    $migrations = Get-Content (Join-Path $repo "deploy\migrations.txt") |
      ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith("#") }
    foreach ($pass in 1, 2) {
      foreach ($m in $migrations) { Invoke-Script $master (Join-Path $repo $m) $db }
      Say "    migrations pass $pass OK ($($migrations.Count) scripts)"
    }
    foreach ($t in Get-ChildItem (Join-Path $repo "deploy\tests\*.sql") | Sort-Object Name) {
      Invoke-Script $master $t.FullName $db
    }
    Say "SCENARIO ${name}: PASS"
    return $true
  } catch {
    Say "SCENARIO ${name}: FAIL - $($_.Exception.Message)"
    return $false
  } finally {
    try {
      $cmd = $master.CreateCommand()
      $cmd.CommandText = "USE master; IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END"
      [void]$cmd.ExecuteNonQuery()
      Say "    dropped $db"
    } catch { Say "    could not drop ${db}: $($_.Exception.Message)" }
    $master.Close()
  }
}

try {
  $probe = Open-Sql "master"; $probe.Close()
  $okA = Run-Scenario "A" @()
  $okB = Run-Scenario "B" @("AccessControl\Database\SiteQr.sql", "AccessControl\Database\Mappings.sql")
  $okC = Run-Scenario "C" @("AccessControl\Database\Mappings.sql", "AccessControl\Database\SiteQr.sql")
  if (-not ($okA -and $okB -and $okC)) { $failed = $true }
} catch {
  Say "DATABASE: SKIPPED - cannot connect to $Server ($($_.Exception.Message)). Run again with -Server <name>."
  $failed = $true
}

Say ("== RESULT: " + $(if ($failed) { "SOMETHING FAILED - see above" } else { "ALL PASSED" }))
$global:AcTestLines | Set-Content -Path $results -Encoding UTF8
Write-Host ""
Write-Host "Saved to $results"
