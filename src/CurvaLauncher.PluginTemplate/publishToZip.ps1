$filename = $PWD.Path + "/MyCurvaLauncherPlugin.zip"
dotnet build .\MyCurvaLauncherPlugin.csproj -c Release -p:ZipFileDestination=$filename