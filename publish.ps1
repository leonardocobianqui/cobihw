# Publishes both download packages for the version in Directory.Build.props, zips them and writes SHA256SUMS.txt.

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src\CobiHw.App\CobiHw.App.csproj'
$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
$output = Join-Path $root "publish\$version"

# Publish profiles live in src/CobiHw.App/Properties/PublishProfiles.
$packages = @(
    @{ Profile = 'Portable'; Name = "cobihw-$version-portable-win-x64" },
    @{ Profile = 'FrameworkDependent'; Name = "cobihw-$version-framework-dependent-win-x64" }
)

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

foreach ($package in $packages) {
    $folder = Join-Path $output "$($package.Name)\cobihw"

    dotnet publish $project -p:PublishProfile=$($package.Profile) -o $folder
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing the $($package.Profile) profile failed."
    }

    # The zip holds a cobihw folder, so extracting it doesn't scatter files.
    Compress-Archive -Path $folder -DestinationPath (Join-Path $output "$($package.Name).zip")
}

# Checksums in the sha256sum format, so users can verify the download.
Get-ChildItem $output -Filter '*.zip' |
    ForEach-Object { "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.Name)" } |
    Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii

Write-Host ''
Write-Host "Version $version is ready in $output"
Get-ChildItem $output -Filter '*.zip' | ForEach-Object { Write-Host ("  {0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
