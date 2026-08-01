[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string] $Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$resolvedPackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$expectedPackages = @(
    "AnyBase.Net.$Version.nupkg",
    "AnyBase.Net.Tool.$Version.nupkg"
)

foreach ($package in $expectedPackages) {
    $packagePath = Join-Path $resolvedPackageDirectory $package
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Expected package '$packagePath' was not found."
    }
}

$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $temporaryRoot "anybase-package-tests-$([Guid]::NewGuid().ToString('N'))"
$consumerDirectory = Join-Path $testRoot 'consumer'
$toolDirectory = Join-Path $testRoot 'tool'

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null

    $nugetConfigPath = Join-Path $testRoot 'NuGet.config'
    $escapedPackageDirectory = [Security.SecurityElement]::Escape($resolvedPackageDirectory)
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-packages" value="$escapedPackageDirectory" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@
    [IO.File]::WriteAllText(
        $nugetConfigPath,
        $nugetConfig,
        [Text.UTF8Encoding]::new($false))

    Invoke-DotNet -Arguments @(
        'new', 'console',
        '--name', 'PackageConsumer',
        '--output', $consumerDirectory,
        '--framework', 'net8.0',
        '--no-restore'
    )

    $program = @'
using AnyBase.Net;

var hexadecimal = new Base<char>("0123456789ABCDEF");
Console.Write(hexadecimal.EncodeToString("A"));
'@
    [IO.File]::WriteAllText(
        (Join-Path $consumerDirectory 'Program.cs'),
        $program,
        [Text.UTF8Encoding]::new($false))

    $consumerProject = Join-Path $consumerDirectory 'PackageConsumer.csproj'
    Invoke-DotNet -Arguments @(
        'add', $consumerProject,
        'package', 'AnyBase.Net',
        '--version', $Version,
        '--no-restore'
    )
    Invoke-DotNet -Arguments @(
        'restore', $consumerProject,
        '--configfile', $nugetConfigPath,
        '--no-cache'
    )
    Invoke-DotNet -Arguments @(
        'build', $consumerProject,
        '--configuration', 'Release',
        '--no-restore'
    )

    $consumerAssembly = Join-Path $consumerDirectory 'bin/Release/net8.0/PackageConsumer.dll'
    $consumerOutput = (& dotnet $consumerAssembly | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $consumerOutput -ne '41') {
        throw "The library package smoke test returned '$consumerOutput' instead of '41'."
    }

    Invoke-DotNet -Arguments @(
        'tool', 'install', 'AnyBase.Net.Tool',
        '--tool-path', $toolDirectory,
        '--version', $Version,
        '--configfile', $nugetConfigPath,
        '--no-cache'
    )

    $isWindows = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [Runtime.InteropServices.OSPlatform]::Windows)
    $toolName = if ($isWindows) { 'anybase.exe' } else { 'anybase' }
    $toolPath = Join-Path $toolDirectory $toolName
    $toolOutput = (& $toolPath encode A --base 16 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $toolOutput -ne '41') {
        throw "The CLI package smoke test returned '$toolOutput' instead of '41'."
    }

    Write-Output "Package smoke tests passed for AnyBase.Net $Version and AnyBase.Net.Tool $Version."
}
finally {
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $testRootName = Split-Path -Leaf $resolvedTestRoot
    if ($resolvedTestRoot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
        $testRootName.StartsWith('anybase-package-tests-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
