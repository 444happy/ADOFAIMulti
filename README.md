# ADOFAI Multi (얼불춤 멀티)

A Dance of Fire and Ice 친구끼리 멀티 모드 (UnityModManager)

- Steam 로비 + P2P (서버/포트포워딩 불필요, 친구 초대로 입장, 최대 8명)
- 방장이 고른 맵을 모두에게 자동 전송 (일시정지/재개/취소, 캐시)
- 무적 모드 고정, 전원 준비(F5) 후 동시 시작
- 플레이 중 왼쪽 실시간 정확도 랭킹, 끝나면 세부 판정 결과
- 메인 섬의 "멀티" 포탈로 입장하는 전용 멀티 섬, 멀티 중 ESC·재시작 차단, 기권 Ctrl + Shift + Q
- 게임 실행 시 자동 업데이트 (재시작하면 적용)

## 설치
1. [UnityModManager](https://www.nexusmods.com/site/mods/21) 설치
2. 다운로드
   - [ADOFAIMulti_Installer.zip](https://github.com/444happy/ADOFAIMulti/raw/main/release/ADOFAIMulti_Installer.zip) → 압축 풀고 설치기 실행, 또는
   - [ADOFAIMulti_UMM.zip](https://github.com/444happy/ADOFAIMulti/raw/main/release/ADOFAIMulti_UMM.zip) → UMM의 Mods 탭에 그대로 드래그
3. 게임 실행 → 메인 메뉴 오른쪽 아래 **멀티플레이**

## 빌드
게임의 `A Dance of Fire and Ice_Data/Managed` DLL을 참조해 `make_release.sh <owner>`로 빌드합니다. (게임 DLL은 저장소에 포함하지 않습니다)
