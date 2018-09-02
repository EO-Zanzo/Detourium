param($installPath, $toolsPath, $package, $project)
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Unzip
{
    param([string]$zipfile, [string]$outpath)
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipfile, $outpath)
}

Unzip (Get-Item -Path (join-path($toolsPath) "runtime.zip")) (join-path([Environment]::GetFolderPath("CommonApplicationData")) "Detourium")