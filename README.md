# ADOFAI Multi (얼불춤 멀티)

A Dance of Fire and Ice 친구끼리 멀티 모드 (UnityModManager)

- Steam 로비 + P2P (서버/포트포워딩 불필요, 친구 초대로 입장, 최대 8명)
- 방장이 고른 맵을 모두에게 자동 전송 (일시정지/재개/취소, 캐시)
- 무적 모드 고정, 전원 준비(F5) 후 동시 시작
- 플레이 중 왼쪽 실시간 정확도 랭킹, 끝나면 세부 판정 결과
- 메인 섬의 "멀티" 포탈로 입장하는 전용 멀티 섬, 멀티 중 ESC·재시작 차단, 기권 Ctrl + Shift + Q
- 방장이 TUF(The Universal Forums)에서 맵을 검색해 고르면 모두가 TUF CDN에서 직접 다운로드
- 게임 실행 시 자동 업데이트 (재시작하면 적용)

## 설치 (가장 쉬운 방법)

게임을 끈 상태에서 **PowerShell**(시작 → "PowerShell" 검색)을 열고 아래 한 줄을 붙여넣으세요.

```powershell
irm https://raw.githubusercontent.com/444happy/ADOFAIMulti/main/install.ps1 | iex
```

파일을 내려받지 않아서 브라우저/백신 경고가 뜨지 않아요. UnityModManager(UMM)는 먼저 설치되어 있어야 해요.

## 설치
1. [UnityModManager](https://www.nexusmods.com/site/mods/21) 설치
2. 다운로드
   - [ADOFAIMulti_Installer.zip](https://github.com/444happy/ADOFAIMulti/raw/main/release/ADOFAIMulti_Installer.zip) → 압축 풀고 설치기 실행, 또는
   - [ADOFAIMulti_UMM.zip](https://github.com/444happy/ADOFAIMulti/raw/main/release/ADOFAIMulti_UMM.zip) → UMM의 Mods 탭에 그대로 드래그
3. 게임 실행 → 메인 메뉴 오른쪽 아래 **멀티플레이**

## 빌드
게임의 `A Dance of Fire and Ice_Data/Managed` DLL을 참조해 `make_release.sh <owner>`로 빌드합니다. (게임 DLL은 저장소에 포함하지 않습니다)

## 개발 (Visual Studio / Rider / VS Code)

1. `ADOFAIMulti.csproj` 를 IDE로 열기 (VS Code는 폴더를 열고 C# Dev Kit 설치)
2. 게임이 기본 위치(`C:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice`)가 아니면
   환경 변수 `ADOFAI_PATH` 를 게임 폴더로 지정
3. **Debug 빌드** (VS: Ctrl+Shift+B, VS Code: Ctrl+Shift+B) → 게임 `Mods\ADOFAIMulti` 에 자동 복사 → 게임 재시작해서 확인

- 게임 폴더의 DLL(mscorlib 포함)로 빌드하므로 실제 게임과 같은 환경이에요.
- 버전은 `src/Main.cs` 의 `Version` 한 곳에서만 바꾸면 돼요.
- Debug 빌드는 `design/layout.json` 을 `Mods\ADOFAIMulti\layout.dev.json` 으로도 복사해요. 이 파일이 있으면 게임이 GitHub의 배치 대신 이걸 쓰고, 고치면 1초 안에 다시 읽어요. (배포본 배치를 다시 보려면 이 파일을 지우세요)
- 모두에게 배포(자동 업데이트)는 `./make_release.sh 444happy "변경 내용"` → 커밋/푸시.
