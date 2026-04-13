param(
    [string]$UnityEditorManagedDir = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\Managed"
)

$project = Join-Path $PSScriptRoot "validation\FrameAngel.UnityEditorBridge.Validation.csproj"
$legacyArtifacts = @(
    (Join-Path $PSScriptRoot "validation\bin"),
    (Join-Path $PSScriptRoot "validation\obj"),
    (Join-Path $PSScriptRoot "validation\bin.meta"),
    (Join-Path $PSScriptRoot "validation\obj.meta")
)

foreach ($path in $legacyArtifacts) {
    if (Test-Path $path) {
        Remove-Item -Recurse -Force $path
    }
}

dotnet build $project -p:UnityEditorManagedDir=$UnityEditorManagedDir

foreach ($path in $legacyArtifacts) {
    if (Test-Path $path) {
        Remove-Item -Recurse -Force $path
    }
}
