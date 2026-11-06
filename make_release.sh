#!/usr/bin/env bash
# 사용법: ./make_release.sh <github-owner> "<변경사항 한 줄>"
# 결과: release/ 에 배포 파일 생성 → 커밋/푸시하면 모두에게 자동 업데이트
set -euo pipefail
OWNER="$1"; NOTES="${2:-}"
REPO=ADOFAIMulti
ROOT="$(cd "$(dirname "$0")" && pwd)"
M=/tmp/M   # 게임 Managed 폴더 (참조용)
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)

VER=$(grep -oP 'Version = "\K[^"]+' "$ROOT/src/Main.cs")
TAG="v$VER"
DL="https://raw.githubusercontent.com/$OWNER/$REPO/main/release"
LATEST="$DL"
OUT="$ROOT/release"; rm -rf "$OUT"; mkdir -p "$OUT"
BUILD="$ROOT/build"; mkdir -p "$BUILD"

# 1) 소스에 저장소 주인 반영 후 빌드
sed "s/__OWNER__/$OWNER/" "$ROOT/src/Updater.cs" > /tmp/Updater.gen.cs
SRCS=$(ls "$ROOT"/src/*.cs | grep -v Updater.cs)
REFS=""; for f in "$M"/*.dll "$M"/UnityModManager/UnityModManager.dll "$M"/UnityModManager/0Harmony.dll; do REFS="$REFS -r:$f"; done
dotnet "$CSC" -nologo -noconfig -nostdlib -target:library -langversion:9 -optimize -nowarn:1701,1702,0436 \
  -out:"$BUILD/ADOFAIMulti.dll" $REFS $SRCS /tmp/Updater.gen.cs

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
  -out:"$BUILD/ADOFAIMulti_Installer.exe" -r:$M/mscorlib.dll -r:$M/System.dll -r:$M/System.Core.dll \
  -resource:"$BUILD/ADOFAIMulti.dll",ADOFAIMulti.dll -resource:"$BUILD/Info.json",Info.json "$ROOT/installer/Installer.cs"

# 4) 릴리스 파일
cp "$BUILD/ADOFAIMulti.dll" "$BUILD/Info.json" "$OUT/"
echo "wss://adofai-multi-relay.444happy123.workers.dev" > "$OUT/relay.txt"
TMP=$(mktemp -d); mkdir -p "$TMP/umm/ADOFAIMulti" "$TMP/inst"
cp "$BUILD/ADOFAIMulti.dll" "$BUILD/Info.json" "$TMP/umm/ADOFAIMulti/"
(cd "$TMP/umm" && zip -q -r -X "$OUT/ADOFAIMulti_UMM.zip" ADOFAIMulti </dev/null)
cp "$BUILD/ADOFAIMulti_Installer.exe" "$TMP/inst/"
printf '1. UnityModManager(UMM)를 먼저 설치하세요: https://www.nexusmods.com/site/mods/21\r\n2. 얼불춤을 끄고 ADOFAIMulti_Installer.exe 실행 -> 1 입력\r\n3. "Windows의 PC 보호" 창이 뜨면 "추가 정보 -> 실행"\r\n4. 메인 섬에 생기는 "멀티" 포탈로 이동\r\n5. 이후 업데이트는 게임 실행 시 자동으로 받아집니다 (재시작하면 적용)\r\n' > "$TMP/inst/읽어주세요.txt"
(cd "$TMP/inst" && zip -q -X "$OUT/ADOFAIMulti_Installer.zip" * </dev/null)
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
