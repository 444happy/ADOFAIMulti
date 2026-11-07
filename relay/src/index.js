// ADOFAI Multi 중계 서버
// - 방 코드 하나 = Room(Durable Object) 하나. WebSocket 으로 받은 메시지를 대상에게 그대로 전달만 한다.
//   바이너리 프레임: [8바이트 LE 대상 id (0 = 나 빼고 전원)] + 데이터 → 서버가 앞 8바이트를 "보낸 사람 id" 로 바꿔 전달
//   텍스트 프레임: 제어 JSON. "ping" → "pong". 방장이 {"t":"meta",...} 를 보내면 방 정보 갱신
// - 공개 방 목록 = Lobby(Durable Object) 하나. Room 이 생성/인원/맵/단계 변화 때 알려준다.
//   GET /rooms → 공개 방 목록 (3초 캐시)
// - WebSocket Hibernation API 사용 → 조용할 때 과금/메모리 거의 0

const CODE_RE = /^[A-Z0-9]{4,8}$/;
const MAX_MEMBERS = 8;
const STALE_MS = 90_000;          // 방장이 이 시간 동안 소식이 없으면 목록에서 숨김

const json = (obj, extra = {}) => new Response(JSON.stringify(obj), {
  headers: { "content-type": "application/json; charset=utf-8", "access-control-allow-origin": "*", ...extra },
});

export default {
  async fetch(req, env, ctx) {
    const url = new URL(req.url);
    if (url.pathname === "/" || url.pathname === "/health") {
      return new Response("ADOFAI Multi relay OK", { headers: { "content-type": "text/plain; charset=utf-8" } });
    }

    if (url.pathname === "/rooms" && req.method === "GET") {
      // 여러 명이 몇 초마다 새로고침해도 Durable Object 요청이 늘지 않게 3초 캐시
      const cache = caches.default;
      const key = new Request(url.origin + "/rooms", { method: "GET" });
      let res = await cache.match(key);
      if (res) return res;
      const lobby = env.LOBBY.get(env.LOBBY.idFromName("main"));
      const body = await (await lobby.fetch("https://lobby/list")).text();
      res = new Response(body, { headers: { "content-type": "application/json; charset=utf-8", "access-control-allow-origin": "*", "cache-control": "public, max-age=3" } });
      ctx.waitUntil(cache.put(key, res.clone()));
      return res;
    }

    const m = url.pathname.match(/^\/room\/([A-Za-z0-9]+)$/);
    if (!m || !CODE_RE.test(m[1].toUpperCase())) return new Response("not found", { status: 404 });
    if (req.headers.get("Upgrade") !== "websocket") return new Response("expected websocket", { status: 426 });
    const stub = env.ROOMS.get(env.ROOMS.idFromName(m[1].toUpperCase()));
    return stub.fetch(req);
  },
};

async function pwHash(code, pw) {
  if (!pw) return "";
  const d = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(code + ":" + pw));
  return [...new Uint8Array(d)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

// ===================================================================== Room
export class Room {
  constructor(state, env) {
    this.state = state;
    this.env = env;
  }

  members() {
    const out = [];
    for (const ws of this.state.getWebSockets()) {
      const a = ws.deserializeAttachment();
      if (a) out.push(a);
    }
    return out;
  }

  lobby() { return this.env.LOBBY.get(this.env.LOBBY.idFromName("main")); }

  /** 공개 방이면 목록에 등록/갱신, 아니면 목록에서 제거 */
  async publish() {
    const meta = (await this.state.storage.get("meta")) || {};
    const list = this.members();
    const host = list.find((a) => a.host);
    if (!host) { await this.unpublish(); return; }
    // 공개 방, 또는 비밀번호가 걸린 방(자물쇠 표시)을 목록에 올림
    if (!meta.public && !meta.pwHash) { await this.unpublish(); return; }
    const entry = {
      locked: !!meta.pwHash,
      code: meta.code, name: (meta.name || `${host.name}의 방`).slice(0, 40), host: host.name,
      count: list.length, max: MAX_MEMBERS, phase: meta.phase || "lobby",
      song: (meta.song || "").slice(0, 60), artist: (meta.artist || "").slice(0, 40), ver: host.ver || "",
    };
    try { await this.lobby().fetch("https://lobby/update", { method: "POST", body: JSON.stringify(entry) }); } catch {}
  }

  async unpublish() {
    const meta = (await this.state.storage.get("meta")) || {};
    if (!meta.code) return;
    try { await this.lobby().fetch("https://lobby/remove", { method: "POST", body: JSON.stringify({ code: meta.code }) }); } catch {}
  }

  async fetch(req) {
    const url = new URL(req.url);
    const host = url.searchParams.get("host") === "1";
    const id = url.searchParams.get("id") || "";
    const name = (url.searchParams.get("name") || "Player").slice(0, 32);
    const ver = (url.searchParams.get("v") || "").slice(0, 16);
    const code = (url.pathname.split("/").pop() || "").toUpperCase();

    const pair = new WebSocketPair();
    const [client, server] = Object.values(pair);
    this.state.acceptWebSocket(server);

    const reject = (c, msg) => {
      server.send(JSON.stringify({ t: "error", code: c, msg }));
      server.close(4000, c);
      return new Response(null, { status: 101, webSocket: client });
    };

    const list = this.members();
    const hostMember = list.find((a) => a.host);
    if (!/^\d{1,20}$/.test(id) || id === "0") return reject("bad_id", "잘못된 접속 정보예요");
    if (host && hostMember) return reject("taken", "이미 쓰이는 방 코드예요. 다시 만들어 주세요");
    if (!host && !hostMember) return reject("no_room", "방을 찾을 수 없어요. 코드를 확인해 주세요");
    if (list.length >= MAX_MEMBERS) return reject("full", "방이 꽉 찼어요 (최대 8명)");
    if (!host) {
      const meta = (await this.state.storage.get("meta")) || {};
      if (meta.pwHash && (await pwHash(code, url.searchParams.get("pw") || "")) !== meta.pwHash)
        return reject("bad_pw", url.searchParams.get("pw") ? "비밀번호가 틀렸어요" : "비밀번호가 필요한 방이에요");
    }
    if (list.some((a) => a.id === id)) return reject("dup", "이미 이 방에 접속해 있어요");

    if (host) {
      // 새 방: 공개 여부/이름은 접속 주소로 받고, 이후 방장이 meta 메시지로 바꿀 수 있음
      await this.state.storage.put("meta", {
        code, public: url.searchParams.get("pub") === "1",
        pwHash: await pwHash(code, (url.searchParams.get("pw") || "").slice(0, 32)),
        name: (url.searchParams.get("rname") || "").slice(0, 40), phase: "lobby", song: "", artist: "",
      });
    }

    const me = { id, name, host, ver };
    server.serializeAttachment(me);
    server.send(JSON.stringify({ t: "welcome", you: id, members: [...list, me] }));
    for (const ws of this.state.getWebSockets()) {
      if (ws !== server && ws.deserializeAttachment()) {
        try { ws.send(JSON.stringify({ t: "joined", id, name, host })); } catch {}
      }
    }
    this.state.waitUntil(this.publish());
    return new Response(null, { status: 101, webSocket: client });
  }

  async webSocketMessage(ws, msg) {
    if (typeof msg === "string") {
      if (msg === "ping") {
        ws.send("pong");
        const a = ws.deserializeAttachment();
        if (a && a.host) await this.publish();   // 방장 핑 = 목록 하트비트
        return;
      }
      const a = ws.deserializeAttachment();
      if (!a || !a.host) return;
      let j; try { j = JSON.parse(msg); } catch { return; }
      if (j.t === "meta") {
        const meta = (await this.state.storage.get("meta")) || {};
        for (const k of ["public", "name", "phase", "song", "artist"]) if (k in j) meta[k] = j[k];
        if ("pw" in j) meta.pwHash = await pwHash(meta.code, String(j.pw || "").slice(0, 32));
        await this.state.storage.put("meta", meta);
        await this.publish();
      }
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

  async webSocketClose(ws) { await this.leave(ws); }
  async webSocketError(ws) { await this.leave(ws); }

  async leave(ws) {
    const a = ws.deserializeAttachment();
    if (!a) return;
    ws.serializeAttachment(null);
    for (const o of this.state.getWebSockets()) {
      if (o === ws || !o.deserializeAttachment()) continue;
      try { o.send(JSON.stringify({ t: "left", id: a.id, host: a.host })); } catch {}
      if (a.host) { try { o.close(4001, "host_left"); } catch {} }   // 방장이 나가면 방 닫힘
    }
    if (a.host) { await this.unpublish(); await this.state.storage.delete("meta"); }
    else await this.publish();
  }
}

// ===================================================================== Lobby (공개 방 목록)
export class Lobby {
  constructor(state, env) {
    this.state = state;
  }

  async fetch(req) {
    const url = new URL(req.url);
    if (url.pathname === "/list") {
      const all = await this.state.storage.list();
      const now = Date.now(), rooms = [], stale = [];
      for (const [k, v] of all) {
        if (now - v.updated > STALE_MS) stale.push(k); else rooms.push(v);
      }
      if (stale.length) await this.state.storage.delete(stale);
      rooms.sort((a, b) => b.updated - a.updated);
      return json({ rooms: rooms.slice(0, 100) });
    }
    if (req.method === "POST" && url.pathname === "/update") {
      const e = await req.json();
      if (!e.code) return json({ ok: false });
      e.updated = Date.now();
      await this.state.storage.put(e.code, e);
      return json({ ok: true });
    }
    if (req.method === "POST" && url.pathname === "/remove") {
      const e = await req.json();
      if (e.code) await this.state.storage.delete(e.code);
      return json({ ok: true });
    }
    return new Response("not found", { status: 404 });
  }
}
