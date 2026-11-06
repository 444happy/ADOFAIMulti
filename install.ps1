# ADOFAI Multi 설치 스크립트
# 사용법 (PowerShell에 붙여넣기):
#   irm https://raw.githubusercontent.com/444happy/ADOFAIMulti/main/install.ps1 | iex
$ErrorActionPreference = 'Stop'
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}

function Ok($m)   { Write-Host "  [OK] $m" -ForegroundColor Green }
function Info($m) { Write-Host "  $m" -ForegroundColor Gray }
function Fail($m) { Write-Host "  [!] $m" -ForegroundColor Red }

Write-Host ""
Write-Host "  ADOFAI Multi 설치" -ForegroundColor Cyan
Write-Host ""

$base = 'https://raw.githubusercontent.com/444happy/ADOFAIMulti/main/release'
$name = 'A Dance of Fire and Ice'

# 1. 게임 폴더 찾기
$steamRoots = @()
foreach ($k in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
  try {
    $p = Get-ItemProperty -Path $k -ErrorAction Stop
    foreach ($v in $p.SteamPath, $p.InstallPath) { if ($v) { $steamRoots += ($v -replace '/', '\') } }
  } catch {}
}
$steamRoots += "${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam"

$libs = @()
foreach ($r in ($steamRoots | Select-Object -Unique)) {
  if (-not (Test-Path $r)) { continue }
  $libs += $r
  $vdf = Join-Path $r 'steamapps\libraryfolders.vdf'
  if (Test-Path $vdf) {
    foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
      $libs += ($m.Groups[1].Value -replace '\\\\', '\')
    }
  }
}

$game = $null
foreach ($l in ($libs | Select-Object -Unique)) {
  $g = Join-Path $l "steamapps\common\$name"
  if (Test-Path (Join-Path $g "$name.exe")) { $game = $g; break }
}
if (-not $game) {
  Fail "얼불춤 설치 폴더를 못 찾았어요."
  $game = Read-Host "  게임 폴더 경로를 붙여넣어 주세요 (예: D:\SteamLibrary\steamapps\common\$name)"
  $game = $game.Trim('"', ' ')
  if (-not (Test-Path (Join-Path $game "$name.exe"))) { Fail "그 폴더에 게임이 없어요."; return }
}
Ok "게임 폴더: $game"

# 2. 게임 실행 중이면 중단
if (Get-Process -Name $name -ErrorAction SilentlyContinue) {
  Fail "게임이 켜져 있어요. 게임을 끄고 다시 실행해 주세요."; return
}

# 3. UMM 확인
$umm = Join-Path $game "${name}_Data\Managed\UnityModManager\UnityModManager.dll"
if (Test-Path $umm) { Ok "UnityModManager 확인" }
else {
  Fail "UnityModManager(UMM)가 설치되어 있지 않아요."
  Info "https://www.nexusmods.com/site/mods/21 에서 UMM을 받아 얼불춤에 설치한 뒤 다시 실행해 주세요."
  Info "(모드 파일은 지금 넣어둘게요. UMM만 설치하면 바로 작동해요.)"
}

# 4. 모드 내려받기 + 검증
$dir = Join-Path $game 'Mods\ADOFAIMulti'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$t = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$ver = Invoke-RestMethod "$base/version.json?t=$t"
$tmp = Join-Path $env:TEMP "ADOFAIMulti_$t.dll"
Invoke-WebRequest "$base/ADOFAIMulti.dll?t=$t" -OutFile $tmp -UseBasicParsing
$hash = (Get-FileHash $tmp -Algorithm SHA256).Hash.ToLower()
if ($hash -ne $ver.Sha256.ToLower()) { Remove-Item $tmp -Force; Fail "파일 검증 실패. 잠시 후 다시 시도해 주세요."; return }
Invoke-WebRequest "$base/Info.json?t=$t" -OutFile (Join-Path $dir 'Info.json') -UseBasicParsing
Move-Item $tmp (Join-Path $dir 'ADOFAIMulti.dll') -Force
Get-ChildItem $dir -Filter '*.old' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Ok "ADOFAI Multi v$($ver.Version) 설치 완료"

Write-Host ""
Info "게임을 켜고 메인섬에서 조금 움직이면 커스텀 레벨 포탈 옆에 멀티 포탈이 나타나요."
Info "이후 업데이트는 자동으로 받아요."
Write-Host ""
