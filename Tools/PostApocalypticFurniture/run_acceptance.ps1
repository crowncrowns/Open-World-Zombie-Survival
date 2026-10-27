param([string]$UnityEditor='E:\Unity\6000.6.3f1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$repoRoot=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$projectRoot=Join-Path $repoRoot 'work\PostApocalypticFurniture\UnityProject'
$proofRoot=Join-Path $repoRoot 'Verification\PostApocalypticFurniture'
$logRoot=Join-Path $repoRoot 'work\PostApocalypticFurniture'
if (!(Test-Path -LiteralPath $UnityEditor) -or !(Test-Path -LiteralPath $projectRoot)) {throw 'Furniture validation project or pinned Unity editor is missing'}
$steps=@(
  @('body','FurnitureBodyContactAudit.Run','FURNITURE_NATIVE_BODY_CONTACT_DIAGNOSTIC'),
  @('runtime','FurnitureRuntimeChecks.Run','FURNITURE_RUNTIME_PASS'),
  @('cold-state','FurnitureRuntimeChecks.RunCold','FURNITURE_COLD_STATE_RESTORE_PASS'),
  @('stress','FurnitureStressChecks.Run','FURNITURE_STRESS_PASS'),
  @('catalog','ZombieSurvival.Furniture.Editor.FurniturePackBuilder.RenderAll','FURNITURE_NATIVE_RENDERS_PASS 200'),
  @('professional','FurnitureProfessionalReview.Run','FURNITURE_PROFESSIONAL_REVIEW_PASS'),
  @('cord','FurnitureCordAudit.Run','FURNITURE_CORD158_PASS'),
  @('cold-assets','ZombieSurvival.Furniture.Editor.FurniturePackBuilder.ColdCheck','FURNITURE_COLD_PASS')
)
foreach ($step in $steps) {
  $logFile=Join-Path $logRoot ('accept_'+$step[0]+'.log')
  Write-Output ('Checking furniture: '+$step[0])
  $stageProcess=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-force-d3d11','-projectPath',$projectRoot,'-executeMethod',$step[1],'-logFile',$logFile) -WindowStyle Hidden -PassThru -Wait
  if ($stageProcess.ExitCode -ne 0) {throw ('Furniture stage failed: '+$step[0]+'; see '+$logFile)}
  if (!(Select-String -LiteralPath $logFile -SimpleMatch $step[2] -Quiet)) {throw ('Furniture stage lacks completion proof: '+$step[0])}
  if ($step[0] -eq 'body') {
    $bodyAudit=Get-Content -LiteralPath (Join-Path $proofRoot 'native_body_contacts.json') -Raw | ConvertFrom-Json
    if ($bodyAudit.contacts.Count -ne 0) {throw ('Furniture own-body clearance still has '+$bodyAudit.contacts.Count+' findings')}
  }
  Write-Output ('Furniture PASS: '+$step[0])
}
Write-Output 'FURNITURE_ACCEPTANCE_SEQUENCE_PASS'
