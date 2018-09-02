Add-Type -AssemblyName System.IO.Compression.FileSystem

$tempPluginsDirectory = (join-path([Environment]::GetFolderPath("CommonApplicationData")) "Detourium/plugins")
$nugetLibDirectory = (join-path($PSScriptRoot) "nuspec/lib")
$source = (join-path([Environment]::GetFolderPath("CommonApplicationData")) "Detourium")
$destination = (join-path($PSScriptRoot) "nuspec/tools/runtime.zip")

If (Test-path $destination) {Remove-item $destination}
If (Test-path $tempPluginsDirectory) {Remove-item -Recurse -Force $tempPluginsDirectory}
If (Test-path $nugetLibDirectory) {Remove-item -Recurse -Force $nugetLibDirectory}

New-Item -ItemType Directory -Force -Path $nugetLibDirectory

Copy-Item -LiteralPath (join-path($source) "Detourium.Plugins.dll") -Destination (join-path($nugetLibDirectory) "Detourium.Plugins.dll") -Recurse -Filter {PSIsContainer -eq $true}

Copy-Item -LiteralPath (join-path($source) "Detourium.Plugins.xml") -Destination (join-path($nugetLibDirectory) "Detourium.Plugins.xml") -Recurse -Filter {PSIsContainer -eq $true}

[System.IO.Compression.ZipFile]::CreateFromDirectory($Source, $destination)