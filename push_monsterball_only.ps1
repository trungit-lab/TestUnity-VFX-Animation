# Script đẩy riêng các file bài test Monsterball VFX & Controller lên GitHub
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   PUSH MONSTERBALL VFX & CONTROLLER (CHỈ FILE BÀI TEST)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path "$PSScriptRoot/.git")) {
    $repoRoot = $PSScriptRoot
}
Set-Location $repoRoot

Write-Host "`n[1/3] Adding gitignore and Monsterball files..." -ForegroundColor Yellow
git add .gitignore
git add "TestPJ/Assets/Scripts/Character/MonsterballEffects.cs*"
git add "TestPJ/Assets/Scripts/Character/MonsterballRedController.cs*"
git add "TestPJ/Assets/Scripts/Character/MonsterballRedStats.cs*"
git add "TestPJ/Assets/Scripts/Camera/MonsterballTestCameraFollow.cs*"
git add "TestPJ/Assets/Resources/Monsterball/*"
git add "TestPJ/Assets/Editor/MonsterballVfxAuthoring.cs*"
git add "TestPJ/Assets/Editor/MonsterballVisualProbe.cs*"
git add "TestPJ/Assets/Scenes/SampleScene.unity*"

Write-Host "`n[2/3] Committing changes..." -ForegroundColor Yellow
git commit -m "Refactor Monsterball VFX to authored ParticleSystem prefabs, fix dark smoke shader and horn fire"

Write-Host "`n[3/3] Pushing to GitHub (origin main)..." -ForegroundColor Yellow
git push origin main

Write-Host "`nHoàn thành!" -ForegroundColor Green
