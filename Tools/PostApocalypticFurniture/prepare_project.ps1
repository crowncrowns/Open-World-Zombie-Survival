$ErrorActionPreference='Stop'
$repoRoot=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$stageRoot=[System.IO.Path]::GetFullPath((Join-Path $repoRoot 'work\PostApocalypticFurniture\UnityProject'))
$expected=[System.IO.Path]::GetFullPath((Join-Path $repoRoot 'work\PostApocalypticFurniture'))
if (!$stageRoot.StartsWith($expected+[System.IO.Path]::DirectorySeparatorChar,[System.StringComparison]::OrdinalIgnoreCase)) {throw 'Invalid furniture staging path'}
foreach ($dir in @('Assets\Editor','Assets\PostApocalypticFurniture','Packages','ProjectSettings')) {
  New-Item -ItemType Directory -Force -Path (Join-Path $stageRoot $dir) | Out-Null
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'Assets\Scripts') -Destination (Join-Path $stageRoot 'Assets') -Recurse -Force
foreach ($item in @('manifest.json','packages-lock.json')) {
  Copy-Item -LiteralPath (Join-Path $repoRoot ('Packages\'+$item)) -Destination (Join-Path $stageRoot 'Packages') -Force
}
foreach ($item in @('ProjectVersion.txt','ProjectSettings.asset','InputManager.asset','TagManager.asset')) {
  Copy-Item -LiteralPath (Join-Path $repoRoot ('ProjectSettings\'+$item)) -Destination (Join-Path $stageRoot 'ProjectSettings') -Force
}
foreach ($item in @('InputSystem_Actions.inputactions','InputSystem_Actions.inputactions.meta')) {
  Copy-Item -LiteralPath (Join-Path $repoRoot ('Assets\'+$item)) -Destination (Join-Path $stageRoot 'Assets') -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'Assets\PostApocalypticFurniture\Scripts') -Destination (Join-Path $stageRoot 'Assets\PostApocalypticFurniture') -Recurse -Force
foreach ($item in @('FurnitureRuntimeChecks.cs','FurnitureStressChecks.cs')) {
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot $item) -Destination (Join-Path $stageRoot 'Assets\Editor') -Force
}
Write-Output ('Furniture validation project prepared: '+$stageRoot)
