// ADOFAI Multi 중계 서버
// - 방 코드 하나 = Durable Object 하나. WebSocket 으로 받은 메시지를 대상에게 그대로 전달만 한다.
// - 바이너리 프레임: [8바이트 리틀엔디언 대상 id (0 = 나 빼고 전원)] + 데이터
//   서버는 앞 8바이트를 "보낸 사람 id" 로 바꿔서 전달한다.
// - 텍스트 프레임: 제어 메시지(JSON). "ping" → "pong"
// - WebSocket Hibernation API 사용 → 조용할 때 과금/메모리 거의 0

const CODE_RE = /^[A-Z0-9]{4,8}$/;
const MAX_MEMBERS = 8;

export default {
  async fetch(req, env) {
    const url = new URL(req.url);
    if (url.pathname === "/" || url.pathname === "/health") {
      return new Response("ADOFAI Multi relay OK", { headers: { "content-type": "text/plain; charset=utf-8" } });
    }
    const m = url.pathname.match(/^\/room\/([A-Za-z0-9]+)$/);
    if (!m || !CODE_RE.test(m[1].toUpperCase())) return new Response("not found", { status: 404 });
    if (req.headers.get("Upgrade") !== "websocket") return new Response("expected websocket", { status: 426 });
    const stub = env.ROOMS.get(env.ROOMS.idFromName(m[1].toUpperCase()));
    return stub.fetch(req);
  },
};

export class Room {
  constructor(state, env) {
    this.state = state;
  }

  members() {
    const out = [];
    for (const ws of this.state.getWebSockets()) {
      const a = ws.deserializeAttachment();
      if (a) out.push(a);
    }
    return out;
  }

  async fetch(req) {
    const url = new URL(req.url);
    const host = url.searchParams.get("host") === "1";
    const id = url.searchParams.get("id") || "";
    const name = (url.searchParams.get("name") || "Player").slice(0, 32);
    const ver = (url.searchParams.get("v") || "").slice(0, 16);

    const pair = new WebSocketPair();
    const [client, server] = Object.values(pair);
    this.state.acceptWebSocket(server);

    const reject = (code, msg) => {
      server.send(JSON.stringify({ t: "error", code, msg }));
      server.close(4000, code);
      return new Response(null, { status: 101, webSocket: client });
    };

    const list = this.members();
    const hostMember = list.find((a) => a.host);
    if (!/^\d{1,20}$/.test(id) || id === "0") return reject("bad_id", "잘못된 접속 정보예요");
    if (host && hostMember) return reject("taken", "이미 쓰이는 방 코드예요. 다시 만들어 주세요");
    if (!host && !hostMember) return reject("no_room", "방을 찾을 수 없어요. 코드를 확인해 주세요");
    if (list.length >= MAX_MEMBERS) return reject("full", "방이 꽉 찼어요 (최대 8명)");
    if (list.some((a) => a.id === id)) return reject("dup", "이미 이 방에 접속해 있어요");

    const me = { id, name, host, ver };
    server.serializeAttachment(me);
    server.send(JSON.stringify({ t: "welcome", you: id, members: [...list, me] }));
    for (const ws of this.state.getWebSockets()) {
      if (ws !== server && ws.deserializeAttachment()) {
        try { ws.send(JSON.stringify({ t: "joined", id, name, host })); } catch {}
      }
    }
    return new Response(null, { status: 101, webSocket: client });
  }

  webSocketMessage(ws, msg) {
    if (typeof msg === "string") {
      if (msg === "ping") ws.send("pong");
      return;
    }
    const me = ws.deserializeAttachment();
    if (!me || msg.byteLength < 8) return;
    const view = new DataView(msg);
    const to = view.getBigUint64(0, true);
    view.setBigUint64(0, BigInt(me.id), true);   // 헤더를 보낸 사람 id 로 교체
    for (const o of this.state.getWebSockets()) {
      if (o === ws) continue;
      const a = o.deserializeAttachment();
      if (!a) continue;
      if (to === 0n || BigInt(a.id) === to) {
        try { o.send(msg); } catch {}
      }
    }
  }

  webSocketClose(ws) { this.leave(ws); }
  webSocketError(ws) { this.leave(ws); }

  leave(ws) {
    const a = ws.deserializeAttachment();
    if (!a) return;
    ws.serializeAttachment(null);
    for (const o of this.state.getWebSockets()) {
      if (o === ws || !o.deserializeAttachment()) continue;
      try { o.send(JSON.stringify({ t: "left", id: a.id, host: a.host })); } catch {}
      if (a.host) { try { o.close(4001, "host_left"); } catch {} }   // 방장이 나가면 방 닫힘
    }
  }
}
