#!/usr/bin/env bash
# 사용법: ./make_release.sh <github-owner> "<변경사항 한 줄>"
# 결과: release/ 에 배포 파일 생성 → 커밋/푸시하면 모두에게 자동 업데이트
set -euo pipefail
OWNER="$1"; NOTES="${2:-}"
REPO=ADOFAIMulti
ROOT="$(cd "$(dirname "$0")" && (pwd -W 2>/dev/null || pwd))"   # Windows(Git Bash)에서는 C:/... 형태
# 게임 Managed 폴더 (참조용): MANAGED_DIR > /tmp/M (Linux) > 게임 설치 폴더 (Windows)
GAME="${ADOFAI_PATH:-C:/Program Files (x86)/Steam/steamapps/common/A Dance of Fire and Ice}"
M="${MANAGED_DIR:-/tmp/M}"
[ -d "$M" ] || M="$GAME/A Dance of Fire and Ice_Data/Managed"
command -v cygpath >/dev/null && M="$(cygpath -m "$M")"
DOTNET_ROOT_DIR="$(dirname "$(command -v dotnet)")"
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll "$DOTNET_ROOT_DIR"/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | tail -1 || true)
command -v cygpath >/dev/null && CSC="$(cygpath -m "$CSC")"
# zip 이 없으면(Windows) 파이썬으로 같은 구조의 zip 생성:  mkzip <zip> <항목...>  (현재 폴더 기준)
mkzip() {
  local out="$1"; shift
  if command -v zip >/dev/null; then zip -q -r -X "$out" "$@" </dev/null
  else python - "$out" "$@" <<'PY'
import os, sys, zipfile
# 한글 파일명이 명령줄 인자로 오면 Windows에서 깨지므로, 인자 대신 현재 폴더를 직접 읽음 (두 호출 모두 폴더 전체를 묶음)
out, items = sys.argv[1], sorted(os.listdir("."))
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for it in items:
        if os.path.isdir(it):
            for d, _, fs in os.walk(it):
                for f in sorted(fs): z.write(os.path.join(d, f), os.path.join(d, f).replace(os.sep, "/"))
        else: z.write(it, it)
PY
  fi
}

VER=$(grep -oP 'Version = "\K[^"]+' "$ROOT/src/Main.cs")
TAG="v$VER"
DL="https://raw.githubusercontent.com/$OWNER/$REPO/main/release"
LATEST="$DL"
OUT="$ROOT/release"; rm -rf "$OUT"; mkdir -p "$OUT"
BUILD="$ROOT/build"; mkdir -p "$BUILD"

# 1) 빌드 (ADOFAIMulti.csproj — IDE 와 같은 프로젝트, 게임 DLL 참조)
dotnet build "$ROOT/ADOFAIMulti.csproj" -c Release -nologo -v q -p:ManagedDir="$M" -p:GamePath=/nonexistent \
  ${NUGET_CONFIG:+--configfile "$NUGET_CONFIG"} -o "$BUILD/out" >/dev/null
cp "$BUILD/out/ADOFAIMulti.dll" "$BUILD/ADOFAIMulti.dll"

# 2) Info.json (UMM이 업데이트 확인할 Repository 포함)
cat > "$BUILD/Info.json" <<EOF
{
  "Id": "ADOFAIMulti",
  "DisplayName": "ADOFAI Multi (얼불춤 멀티)",
  "Author": "창현",
  "Version": "$VER",
  "ManagerVersion": "0.22.14.0",
  "AssemblyName": "ADOFAIMulti.dll",
  "EntryMethod": "ADOFAIMulti.Main.Load",
  "HomePage": "https://github.com/$OWNER/$REPO",
  "Repository": "$LATEST/repository.json"
}
EOF

# 3) 설치기 exe
dotnet "$CSC" -nologo -noconfig -nostdlib -target:exe -platform:anycpu -langversion:9 -optimize -nowarn:1701,1702,0436 \
  -out:"$BUILD/ADOFAIMulti_Installer.exe" -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/System.Core.dll" \
  -resource:"$BUILD/ADOFAIMulti.dll",ADOFAIMulti.dll -resource:"$BUILD/Info.json",Info.json "$ROOT/installer/Installer.cs"

# 4) 릴리스 파일
cp "$BUILD/ADOFAIMulti.dll" "$BUILD/Info.json" "$OUT/"
cp "$ROOT/design/layout.json" "$OUT/layout.json"
echo "wss://adofai-multi-relay.444happy123.workers.dev" > "$OUT/relay.txt"
TMP=$(mktemp -d); mkdir -p "$TMP/umm/ADOFAIMulti" "$TMP/inst"
cp "$BUILD/ADOFAIMulti.dll" "$BUILD/Info.json" "$ROOT/design/layout.json" "$TMP/umm/ADOFAIMulti/"
(cd "$TMP/umm" && mkzip "$OUT/ADOFAIMulti_UMM.zip" ADOFAIMulti)
cp "$BUILD/ADOFAIMulti_Installer.exe" "$TMP/inst/"
printf '1. UnityModManager(UMM)를 먼저 설치하세요: https://www.nexusmods.com/site/mods/21\r\n2. 얼불춤을 끄고 ADOFAIMulti_Installer.exe 실행 -> 1 입력\r\n3. "Windows의 PC 보호" 창이 뜨면 "추가 정보 -> 실행"\r\n4. 메인 섬에 생기는 "멀티" 포탈로 이동\r\n5. 이후 업데이트는 게임 실행 시 자동으로 받아집니다 (재시작하면 적용)\r\n' > "$TMP/inst/읽어주세요.txt"
(cd "$TMP/inst" && mkzip "$OUT/ADOFAIMulti_Installer.zip" *)
rm -rf "$TMP"

SHA=$(sha256sum "$OUT/ADOFAIMulti.dll" | cut -d' ' -f1)
cat > "$OUT/version.json" <<EOF
{
  "Version": "$VER",
  "DllUrl": "$DL/ADOFAIMulti.dll",
  "InfoUrl": "$DL/Info.json",
  "Sha256": "$SHA",
  "Notes": "$NOTES"
}
EOF
cat > "$OUT/repository.json" <<EOF
{
  "Releases": [
    { "Id": "ADOFAIMulti", "Version": "$VER", "DownloadUrl": "$DL/ADOFAIMulti_UMM.zip" }
  ]
}
EOF
echo "OK $TAG -> $OUT"; ls -la "$OUT"
