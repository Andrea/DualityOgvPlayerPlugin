@echo off
cls
".nuget\NuGet.exe" "Install" "FAKE" "-OutputDirectory" "packages" "-ExcludeVersion"

echo "restored packages"
"packages\FAKE\tools\Fake.exe" CompleteBuild.fsx %*