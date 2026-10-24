#Requires -Version 7.0
# Publishes the download packages for the version in Directory.Build.props, zips them and writes SHA256SUMS.txt.
# Run with PowerShell 7: pwsh ./publish.ps1 (x64 and x86) or pwsh ./publish.ps1 -Architecture x64 (one only).
# Windows PowerShell 5.1 isn't used because its Compress-Archive writes backslashes in zip paths, which the zip format forbids.

param(
    # Each architecture gets a portable and a framework-dependent package.
    [ValidateSet('x64', 'x86')]
    [string[]]$Architecture = @('x64', 'x86')
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src/CobiHw.App/CobiHw.App.csproj'
$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
$output = Join-Path $root "publish/$version"
New-Item -ItemType Directory -Path $output -Force | Out-Null

foreach ($arch in $Architecture) {
    # Publish profiles live in src/CobiHw.App/Properties/PublishProfiles; -r replaces their win-x64 runtime.
    $packages = @(
        @{ Profile = 'Portable'; Name = "cobihw-$version-portable-win-$arch" },
        @{ Profile = 'FrameworkDependent'; Name = "cobihw-$version-framework-dependent-win-$arch" }
    )

    foreach ($package in $packages) {
        $folder = Join-Path $output "$($package.Name)/cobihw"
        $zip = Join-Path $output "$($package.Name).zip"

        # Only this package is replaced; the other architecture's packages stay.
        foreach ($old in @((Join-Path $output $package.Name), $zip)) {
            if (Test-Path $old) {
                Remove-Item $old -Recurse -Force
            }
        }

        dotnet publish $project -p:PublishProfile=$($package.Profile) -r "win-$arch" -o $folder
        if ($LASTEXITCODE -ne 0) {
            throw "Publishing the $($package.Profile) profile for $arch failed."
        }

        # The zip holds a cobihw folder, so extracting it doesn't scatter files.
        Compress-Archive -Path $folder -DestinationPath $zip
    }
}

# One checksum file for every zip of this version, in the sha256sum format, so users can verify the download.
Get-ChildItem $output -Filter '*.zip' | Sort-Object Name |
    ForEach-Object { "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.Name)" } |
    Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii

Write-Host ''
Write-Host "Version $version is ready in $output"
Get-ChildItem $output -Filter '*.zip' | Sort-Object Name | ForEach-Object { Write-Host ("  {0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
