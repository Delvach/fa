function Resolve-FrameAngelRepoRoot {
    param(
        [string]$RepoRoot,
        [string]$CallerScriptRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RepoRoot)) {
        return [System.IO.Path]::GetFullPath($RepoRoot)
    }

    $cursor = if ([string]::IsNullOrWhiteSpace($CallerScriptRoot)) {
        (Get-Location).Path
    }
    else {
        [System.IO.Path]::GetFullPath($CallerScriptRoot)
    }

    while (-not [string]::IsNullOrWhiteSpace($cursor)) {
        $hasAgents = Test-Path -LiteralPath (Join-Path $cursor "AGENTS.md")
        $hasProducts = Test-Path -LiteralPath (Join-Path $cursor "products")
        $hasShared = Test-Path -LiteralPath (Join-Path $cursor "shared")
        if ($hasAgents -and $hasProducts -and $hasShared) {
            return $cursor
        }

        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $cursor) {
            break
        }

        $cursor = $parent
    }

    throw "Could not resolve FrameAngel repo root from '$CallerScriptRoot'."
}

function Ensure-FrameAngelDirectory {
    param([string]$PathValue)

    if (-not (Test-Path -LiteralPath $PathValue)) {
        New-Item -ItemType Directory -Path $PathValue -Force | Out-Null
    }
}

function Get-FrameAngelPlayerLaneRoots {
    param(
        [string]$RepoRoot,
        [string]$CallerScriptRoot,
        [switch]$EnsureAssetLaneScaffold
    )

    $resolvedRepoRoot = Resolve-FrameAngelRepoRoot -RepoRoot $RepoRoot -CallerScriptRoot $CallerScriptRoot
    $assetsPlayerRoot = Join-Path $resolvedRepoRoot "products\vam\assets\player"
    $pluginPlayerRoot = Join-Path $resolvedRepoRoot "products\vam\plugins\player"
    $assetsDocsRoot = Join-Path $assetsPlayerRoot "docs"
    $assetsHandoffsRoot = Join-Path $assetsDocsRoot "handoffs"
    $assetsChangelogRoot = Join-Path $assetsPlayerRoot "changelog"
    $assetsBuildRoot = Join-Path $assetsPlayerRoot "build"
    $assetsUnityRoot = Join-Path $assetsPlayerRoot "unity"
    $playerScreenUnityProjectRoot = Join-Path $assetsUnityRoot "player-screen-2018"
    $vamRoot = Join-Path $resolvedRepoRoot "products\vam"

    if ($EnsureAssetLaneScaffold.IsPresent) {
        Ensure-FrameAngelDirectory -PathValue $assetsPlayerRoot
        Ensure-FrameAngelDirectory -PathValue $assetsDocsRoot
        Ensure-FrameAngelDirectory -PathValue $assetsHandoffsRoot
        Ensure-FrameAngelDirectory -PathValue $assetsChangelogRoot
        Ensure-FrameAngelDirectory -PathValue $assetsBuildRoot
        Ensure-FrameAngelDirectory -PathValue $assetsUnityRoot
    }

    $compileProjectRoot = if (Test-Path -LiteralPath (Join-Path $assetsPlayerRoot "vs")) {
        Join-Path $assetsPlayerRoot "vs"
    }
    elseif (Test-Path -LiteralPath (Join-Path $pluginPlayerRoot "vs")) {
        Join-Path $pluginPlayerRoot "vs"
    }
    else {
        $pluginPlayerRoot
    }

    $compileSourceRoot = if (
        (Test-Path -LiteralPath (Join-Path $assetsPlayerRoot "src\scene-runtime")) -and
        (Test-Path -LiteralPath (Join-Path $assetsPlayerRoot "src\session-bridge"))
    ) {
        $assetsPlayerRoot
    }
    elseif (Test-Path -LiteralPath (Join-Path $pluginPlayerRoot "src\scene-runtime")) {
        $pluginPlayerRoot
    }
    else {
        $assetsPlayerRoot
    }

    $docsSourceRoot = if (Test-Path -LiteralPath $assetsDocsRoot) {
        $assetsDocsRoot
    }
    else {
        Join-Path $pluginPlayerRoot "docs"
    }

    return [pscustomobject]@{
        RepoRoot = $resolvedRepoRoot
        AssetsPlayerRoot = $assetsPlayerRoot
        PluginPlayerRoot = $pluginPlayerRoot
        CompilePlayerRoot = $compileSourceRoot
        CompileProjectRoot = $compileProjectRoot
        CompileSourceRoot = $compileSourceRoot
        DocsSourceRoot = $docsSourceRoot
        AssetsPlayerDocsRoot = $assetsDocsRoot
        AssetsPlayerHandoffsRoot = $assetsHandoffsRoot
        AssetsPlayerChangelogRoot = $assetsChangelogRoot
        AssetsPlayerBuildRoot = $assetsBuildRoot
        AssetsPlayerUnityRoot = $assetsUnityRoot
        PlayerScreenUnityProjectRoot = $playerScreenUnityProjectRoot
        SharedScriptsRoot = Join-Path $resolvedRepoRoot "shared\scripts"
    }
}
