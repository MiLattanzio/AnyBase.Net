[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $DotNetPath = 'dotnet'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    & $DotNetPath @Arguments
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
using AnyBaseFactory = global::AnyBase.Net.AnyBase;

var hexadecimal = AnyBaseFactory.CreateHex();
var rfcBase64 = AnyBaseFactory.CreateRfc4648Base64();
var source = new byte[] { 0x00, 0x0A, 0xFF };
var encoded = new char[hexadecimal.GetEncodedLength(source.Length)];
var symbolsWritten = hexadecimal.Encode(source, encoded);
var decoded = new byte[hexadecimal.GetDecodedLength(symbolsWritten)];
hexadecimal.Decode(encoded, decoded);

using var streamInput = new MemoryStream(source);
using var streamEncoded = new MemoryStream();
await hexadecimal.EncodeAsync(streamInput, streamEncoded, bufferSize: 1);
streamEncoded.Position = 0;
using var streamDecoded = new MemoryStream();
await hexadecimal.DecodeAsync(streamEncoded, streamDecoded, bufferSize: 1);

Console.Write(
    $"{new string(encoded)}|{Convert.ToHexString(decoded)}|" +
    $"{System.Text.Encoding.ASCII.GetString(streamEncoded.ToArray())}|" +
    $"{Convert.ToHexString(streamDecoded.ToArray())}|" +
    $"{rfcBase64.EncodeToString("f")}|" +
    Convert.ToHexString(rfcBase64.DecodeToBytes("Zg==")));
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
    $consumerOutput = (& $DotNetPath $consumerAssembly | Out-String).Trim()
    $expectedConsumerOutput = '000AFF|000AFF|000AFF|000AFF|Zg==|66'
    if ($LASTEXITCODE -ne 0 -or $consumerOutput -ne $expectedConsumerOutput) {
        throw "The library package smoke test returned '$consumerOutput' instead of '$expectedConsumerOutput'."
    }

    Invoke-DotNet -Arguments @(
        'tool', 'install', 'AnyBase.Net.Tool',
        '--tool-path', $toolDirectory,
        '--version', $Version,
        '--configfile', $nugetConfigPath,
        '--no-cache'
    )

    $runningOnWindows = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [Runtime.InteropServices.OSPlatform]::Windows)
    $toolName = if ($runningOnWindows) { 'anybase.exe' } else { 'anybase' }
    $toolPath = Join-Path $toolDirectory $toolName
    $toolOutput = (& $toolPath encode A --alphabet hex --separator '-' | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $toolOutput -ne '4-1') {
        throw "The CLI package smoke test returned '$toolOutput' instead of '4-1'."
    }

    $packedToolOutput = (& $toolPath encode f `
        --mode packed `
        --alphabet rfc-base64 `
        --padding omit | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $packedToolOutput -ne 'Zg') {
        throw "The packed CLI smoke test returned '$packedToolOutput' instead of 'Zg'."
    }

    $binaryInputPath = Join-Path $testRoot 'input.bin'
    $encodedOutputPath = Join-Path $testRoot 'encoded.bin'
    $decodedOutputPath = Join-Path $testRoot 'decoded.bin'
    [IO.File]::WriteAllBytes($binaryInputPath, [byte[]]@(0, 10, 13, 255))

    & $toolPath encode `
        --input $binaryInputPath `
        --input-format binary `
        --output $encodedOutputPath `
        --output-format binary `
        --alphabet hex
    if ($LASTEXITCODE -ne 0) {
        throw "The CLI binary encoding smoke test failed with exit code $LASTEXITCODE."
    }

    $encodedOutput = [Text.Encoding]::ASCII.GetString(
        [IO.File]::ReadAllBytes($encodedOutputPath))
    if ($encodedOutput -cne '000A0DFF') {
        throw "The CLI binary encoding smoke test returned '$encodedOutput' instead of '000A0DFF'."
    }

    & $toolPath decode `
        --input $encodedOutputPath `
        --input-format binary `
        --output $decodedOutputPath `
        --output-format binary `
        --alphabet hex
    if ($LASTEXITCODE -ne 0) {
        throw "The CLI binary decoding smoke test failed with exit code $LASTEXITCODE."
    }

    $decodedOutput = [IO.File]::ReadAllBytes($decodedOutputPath)
    $decodedOutputHex = ($decodedOutput | ForEach-Object { $_.ToString('X2') }) -join ''
    if ($decodedOutputHex -cne '000A0DFF') {
        throw "The CLI binary decoding smoke test returned '$decodedOutputHex' instead of '000A0DFF'."
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
