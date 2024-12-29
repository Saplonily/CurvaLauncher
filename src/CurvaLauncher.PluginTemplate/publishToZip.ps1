dotnet build .\MyCurvaLauncherPlugin.csproj -c Release -p:OutputPath=_temp_output -p:NoZipOutputPlugin=true
Compress-Archive _temp_output/* "MyCurvaLauncherPlugin.zip" -Force
Remove-Item _temp_output -Recurse