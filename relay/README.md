# ADOFAI Multi 중계 서버 (Cloudflare Workers, 무료)

방 코드 하나당 Durable Object 하나가 WebSocket 메시지를 전달만 합니다. 저장하는 데이터 없음.

## 배포 (처음 한 번)
```bash
cd relay
npm install
npx wrangler login      # 브라우저에서 Cloudflare 로그인
npx wrangler deploy     # → https://adofai-multi-relay.<내 서브도메인>.workers.dev
```
배포 후 나온 주소를 `wss://` 로 바꿔 저장소의 `release/relay.txt` 에 한 줄로 넣으면
모든 모드가 다음 실행 때 그 서버를 씁니다. (모드 업데이트 불필요)

## 로컬 테스트
```bash
npx wrangler dev        # ws://127.0.0.1:8787
```
