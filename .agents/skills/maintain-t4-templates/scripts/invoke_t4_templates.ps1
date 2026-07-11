[CmdletBinding(DefaultParameterSetName = 'Validate')]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$ProjectPath = 'src\DataTables.GeneratorCore\DataTables.GeneratorCore.csproj',
    [Parameter(ParameterSetName = 'Validate')]
    [string]$OutputDirectory,
    [Parameter(Mandatory = $true, ParameterSetName = 'Write')]
    [switch]$WriteGeneratedFiles,
    [switch]$AllowGeneratedOnlyChanges
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepositoryPath([string]$path) {
    if ([IO.Path]::IsPathRooted($path)) {
        return [IO.Path]::GetFullPath($path)
    }

    return [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $path))
}

function Remove-TrailingHorizontalWhitespace([string]$path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasUtf8Bom) { 3 } else { 0 }
    $text = [Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
    $normalized = [Text.RegularExpressions.Regex]::Replace(
        $text,
        '[\t ]+(?=\r?$)',
        '',
        [Text.RegularExpressions.RegexOptions]::Multiline)

    if ($normalized -cne $text) {
        [IO.File]::WriteAllText($path, $normalized, [Text.UTF8Encoding]::new($hasUtf8Bom))
    }
}

function Test-GitDirty([string]$relativePath) {
    $result = & git -C $RepositoryRoot status --porcelain=v1 --untracked-files=all -- $relativePath 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect git status for $relativePath"
    }

    return -not [string]::IsNullOrWhiteSpace(($result -join [Environment]::NewLine))
}

function Find-VisualStudioInstallation {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $installation = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($LASTEXITCODE -eq 0 -and $installation) {
            return ($installation | Select-Object -First 1)
        }
    }

    $root = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio'
    $candidate = Get-ChildItem -LiteralPath $root -Filter MSBuild.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'MSBuild\\Current\\Bin\\MSBuild\.exe$' } |
        Select-Object -First 1
    if ($candidate) {
        return Split-Path (Split-Path (Split-Path (Split-Path $candidate.FullName)))
    }

    throw 'Visual Studio MSBuild with T4 tooling was not found. Do not edit generated .cs files directly.'
}

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$projectFullPath = Resolve-RepositoryPath $ProjectPath
if (-not (Test-Path -LiteralPath $projectFullPath)) {
    throw "Project file not found: $projectFullPath"
}

[xml]$project = [IO.File]::ReadAllText($projectFullPath)
$projectDirectory = Split-Path $projectFullPath
$templateNodes = @($project.SelectNodes("/Project/ItemGroup/None[Generator='TextTemplatingFilePreprocessor']"))
if ($templateNodes.Count -eq 0) {
    throw "No TextTemplatingFilePreprocessor mappings found in $projectFullPath"
}

$mappings = foreach ($node in $templateNodes) {
    $templateRelative = if ($node.Update) { [string]$node.Update } else { [string]$node.Include }
    $generatedName = [string]$node.LastGenOutput
    if ([string]::IsNullOrWhiteSpace($templateRelative) -or [string]::IsNullOrWhiteSpace($generatedName)) {
        throw 'Each T4 mapping must define a template path and LastGenOutput.'
    }

    $templatePath = [IO.Path]::GetFullPath((Join-Path $projectDirectory $templateRelative))
    $generatedPath = Join-Path (Split-Path $templatePath) $generatedName
    if (-not (Test-Path -LiteralPath $templatePath)) {
        throw "Template not found: $templatePath"
    }
    if (-not (Test-Path -LiteralPath $generatedPath)) {
        throw "Generated file not found: $generatedPath"
    }

    [pscustomobject]@{
        TemplatePath = $templatePath
        GeneratedPath = $generatedPath
        GeneratedName = $generatedName
        TemplateGitPath = $templatePath.Substring($RepositoryRoot.Length).TrimStart('\').Replace('\', '/')
        GeneratedGitPath = $generatedPath.Substring($RepositoryRoot.Length).TrimStart('\').Replace('\', '/')
    }
}

if (-not $AllowGeneratedOnlyChanges) {
    foreach ($mapping in $mappings) {
        if ((Test-GitDirty $mapping.GeneratedGitPath) -and -not (Test-GitDirty $mapping.TemplateGitPath)) {
            throw "Generated file changed without its T4 source: $($mapping.GeneratedGitPath). Port the change to $($mapping.TemplateGitPath), or use -AllowGeneratedOnlyChanges only for explicit drift recovery."
        }
    }
}

$visualStudio = Find-VisualStudioInstallation
$msbuild = Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe'
$t4Targets = Get-ChildItem -LiteralPath (Join-Path $visualStudio 'MSBuild\Microsoft\VisualStudio') -Filter Microsoft.TextTemplating.targets -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName
if (-not (Test-Path -LiteralPath $msbuild) -or -not $t4Targets) {
    throw "Visual Studio T4 MSBuild targets were not found under $visualStudio"
}

if ($WriteGeneratedFiles) {
    $outputDescription = 'tracked generated files'
} else {
    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) 'DataTables-T4-Validation'
    }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $outputDescription = $OutputDirectory
}

$workDirectory = Join-Path ([IO.Path]::GetTempPath()) ("DataTables-T4-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
$projectFile = Join-Path $workDirectory 'PreprocessTemplates.proj'

try {
    $items = foreach ($mapping in $mappings) {
        $outputPath = if ($WriteGeneratedFiles) { Split-Path $mapping.GeneratedPath } else { $OutputDirectory }
        @"
    <T4Preprocess Include="$([Security.SecurityElement]::Escape($mapping.TemplatePath))">
      <LastGenOutput>$([Security.SecurityElement]::Escape($mapping.GeneratedName))</LastGenOutput>
      <OutputFilePath>$([Security.SecurityElement]::Escape($outputPath))</OutputFilePath>
      <OutputFileName>$([Security.SecurityElement]::Escape($mapping.GeneratedName))</OutputFileName>
      <CustomToolNamespace>DataTables.GeneratorCore</CustomToolNamespace>
    </T4Preprocess>
"@
    }

    $projectText = @"
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <RootNamespace>DataTables.GeneratorCore</RootNamespace>
    <PreprocessTemplateDefaultNamespace>DataTables.GeneratorCore</PreprocessTemplateDefaultNamespace>
    <IntermediateOutputPath>$([Security.SecurityElement]::Escape((Join-Path $workDirectory 'obj')))</IntermediateOutputPath>
    <TransformOutOfDateOnly>false</TransformOutOfDateOnly>
    <TrackFileAccess>false</TrackFileAccess>
    <OverwriteReadOnlyOutputFiles>true</OverwriteReadOnlyOutputFiles>
  </PropertyGroup>
  <ItemGroup>
$($items -join [Environment]::NewLine)
  </ItemGroup>
  <Import Project="$([Security.SecurityElement]::Escape($t4Targets))" />
</Project>
"@
    [IO.File]::WriteAllText($projectFile, $projectText, [Text.UTF8Encoding]::new($false))

    & $msbuild $projectFile /t:TransformAll /p:TransformOutOfDateOnly=false /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) {
        throw "T4 preprocessing failed with exit code $LASTEXITCODE"
    }

    foreach ($mapping in $mappings) {
        $expected = if ($WriteGeneratedFiles) { $mapping.GeneratedPath } else { Join-Path $OutputDirectory $mapping.GeneratedName }
        if (-not (Test-Path -LiteralPath $expected) -or (Get-Item -LiteralPath $expected).Length -eq 0) {
            throw "T4 did not produce the expected output: $expected"
        }
        Remove-TrailingHorizontalWhitespace $expected
        Write-Output "Generated: $expected"
    }

    Write-Output "T4 preprocessing completed for $($mappings.Count) template(s) into $outputDescription."
} finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $resolvedWork = [IO.Path]::GetFullPath($workDirectory)
    if ($resolvedWork.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedWork)) {
        Remove-Item -LiteralPath $resolvedWork -Recurse -Force
    }
}
