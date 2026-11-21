@echo off
rem Builds the API and tests every database script on throwaway local databases.
rem Results: deploy\tests\local-test-results.txt
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-local-tests.ps1" %*
pause
