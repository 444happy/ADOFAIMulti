# ADOFAI Multi (얼불춤 멀티)

A Dance of Fire and Ice를 친구끼리 같이 플레이하는 멀티 모드 (UnityModManager)

**[⬇ 설치기 다운로드 (ADOFAIMulti_Installer.exe)](https://github.com/444happy/ADOFAIMulti/raw/main/release/ADOFAIMulti_Installer.exe)** · [릴리스 목록](https://github.com/444happy/ADOFAIMulti/releases)

## 기능
- 방 만들기 / 참가 코드로 입장 (공개·비공개 방, 최대 8명)
- 방장이 고른 맵을 모두에게 자동 전송, TUF(The Universal Forums) 맵 검색
- 방 채팅
- 레벨 안에서 아무 키나 눌러 준비 → 모두 준비하면 동시 시작
- 플레이 중 실시간 절대 정확도 랭킹, 모두 끝나면 랭킹 창 (절대 정확도 · 세부 판정)
- 멀티 중 무적 모드 고정, 기권 Ctrl + Shift + Q
- 게임 실행 시 자동 업데이트 (재시작하면 적용)

## 설치

### 방법 1. 설치기 (추천)

1. 얼불춤을 끄고 [ADOFAIMulti_Installer.exe](https://github.com/444happy/ADOFAIMulti/raw/main/release/ADOFAIMulti_Installer.exe) 실행
2. 게임 폴더는 자동으로 찾아요 (못 찾으면 "찾아보기")
3. **설치** 클릭 → 끝나면 **게임 실행**

UnityModManager(UMM)가 없으면 설치기가 같이 설치해요. 따로 받을 필요 없어요.

> ⚠️ **Windows 경고 안내**
> 설치기는 코드 서명이 안 된 exe라서 **"Windows의 PC 보호"** 창이 뜰 수 있어요.
> **추가 정보 → 실행**을 누르면 돼요. 브라우저가 다운로드를 막으면 "유지"를 눌러주세요.

### 방법 2. PowerShell 한 줄

[UnityModManager](https://www.nexusmods.com/site/mods/21)가 이미 깔려 있다면, PowerShell에 붙여넣기:

```powershell
irm https://raw.githubusercontent.com/444happy/ADOFAIMulti/main/install.ps1 | iex
```

### 방법 3. UMM에 직접

[ADOFAIMulti_UMM.zip](https://github.com/444happy/ADOFAIMulti/raw/main/release/ADOFAIMulti_UMM.zip)을 UMM의 Mods 탭에 그대로 드래그

### 설치 후

게임 실행 → 메인 섬에 생긴 **"멀티" 포탈**로 이동. 친구와 **버전이 같아야** 같은 방에 들어갈 수 있어요 (게임을 켜면 자동으로 최신 버전을 받고, 재시작하면 적용).

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
