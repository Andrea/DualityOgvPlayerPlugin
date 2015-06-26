@echo off
cls
".paket\paket.bootstrapper.exe" 
echo "installed"
".paket\paket.exe" "restore"
echo "restored packages"
"packages\FAKE\tools\Fake.exe" CompleteBuild.fsx %*