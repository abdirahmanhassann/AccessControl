# Packs AccessControl + AccessControlFE and uploads them to your AWS storage (link valid for 1 hour).
$ErrorActionPreference = "Stop"
$out = Join-Path $env:TEMP "accesscontrol-src.tgz"
Set-Location "C:\Users\abdi1\source\repos"
tar.exe -czf $out --exclude=node_modules --exclude=bin --exclude=obj --exclude=.git --exclude=.vs --exclude=.vercel --exclude=.tanstack --exclude=.output --exclude=screenshots --exclude=attachments AccessControl AccessControlFE
Write-Host ("Packed {0:N1} MB, uploading..." -f ((Get-Item $out).Length/1MB))
curl.exe --fail --show-error -T $out "https://REMOVED-PRESIGNED-URL"
if ($LASTEXITCODE -eq 0) { Write-Host "UPLOAD OK - tell Claude it's done" -ForegroundColor Green } else { Write-Host "UPLOAD FAILED" -ForegroundColor Red }
Read-Host "Press Enter to close"
