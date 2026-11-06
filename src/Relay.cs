using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ADOFAIMulti
{
    /// <summary>
    /// 중계 서버(Cloudflare Worker) WebSocket 클라이언트.
    /// 송수신은 백그라운드 스레드에서 하고, 받은 것은 큐에 쌓아 메인 스레드(Poll)에서 꺼낸다.
    /// 바이너리: [8바이트 LE 대상/보낸사람 id] + 데이터 / 텍스트: 서버 제어 JSON
    /// </summary>
    public class RelayClient
    {
        public struct Inbound { public bool IsText; public string Text; public ulong From; public byte[] Data; public bool Closed; public string Error; }

        readonly ClientWebSocket _ws = new ClientWebSocket();
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        readonly Queue<Inbound> _in = new Queue<Inbound>();
        readonly Queue<KeyValuePair<WebSocketMessageType, byte[]>> _out = new Queue<KeyValuePair<WebSocketMessageType, byte[]>>();
        readonly SemaphoreSlim _outSignal = new SemaphoreSlim(0);
        volatile bool _closed;
        public volatile bool Open;

        public void Connect(string url)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _ws.ConnectAsync(new Uri(url), _cts.Token);
                    Open = true;
                    var send = Task.Run(SendLoop);
                    await RecvLoop();
                }
                catch (Exception ex)
                {
                    Push(new Inbound { Closed = true, Error = ex.GetBaseException().Message });
                }
                finally { Open = false; }
            });
        }

        async Task RecvLoop()
        {
            var buf = new byte[64 * 1024];
            var ms = new MemoryStream();
            while (!_cts.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                var r = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token);
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    Push(new Inbound { Closed = true, Error = r.CloseStatusDescription ?? "" });
                    return;
                }
                ms.Write(buf, 0, r.Count);
                if (!r.EndOfMessage) continue;
                var data = ms.ToArray();
                ms.SetLength(0);
                if (r.MessageType == WebSocketMessageType.Text)
                {
                    var s = Encoding.UTF8.GetString(data);
                    if (s != "pong") Push(new Inbound { IsText = true, Text = s });
                }
                else if (data.Length >= 8)
                {
                    ulong from = BitConverter.ToUInt64(data, 0);
                    var body = new byte[data.Length - 8];
                    Buffer.BlockCopy(data, 8, body, 0, body.Length);
                    Push(new Inbound { From = from, Data = body });
                }
            }
            Push(new Inbound { Closed = true, Error = "" });
        }

        async Task SendLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                await _outSignal.WaitAsync(_cts.Token);
                KeyValuePair<WebSocketMessageType, byte[]> item;
                lock (_out) { if (_out.Count == 0) continue; item = _out.Dequeue(); }
                if (_ws.State != WebSocketState.Open) return;
                await _ws.SendAsync(new ArraySegment<byte>(item.Value), item.Key, true, _cts.Token);
            }
        }

        void Push(Inbound m) { lock (_in) _in.Enqueue(m); }

        public bool TryDequeue(out Inbound m)
        {
            lock (_in)
            {
                if (_in.Count == 0) { m = default; return false; }
                m = _in.Dequeue(); return true;
            }
        }

        public void Send(ulong to, byte[] data)
        {
            if (_closed) return;
            var frame = new byte[data.Length + 8];
            BitConverter.GetBytes(to).CopyTo(frame, 0);      // Windows = 리틀엔디언
            Buffer.BlockCopy(data, 0, frame, 8, data.Length);
            lock (_out) _out.Enqueue(new KeyValuePair<WebSocketMessageType, byte[]>(WebSocketMessageType.Binary, frame));
            _outSignal.Release();
        }

        public void SendText(string s)
        {
            if (_closed) return;
            lock (_out) _out.Enqueue(new KeyValuePair<WebSocketMessageType, byte[]>(WebSocketMessageType.Text, Encoding.UTF8.GetBytes(s)));
            _outSignal.Release();
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            Task.Run(async () =>
            {
                try { if (_ws.State == WebSocketState.Open) await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
                try { _cts.Cancel(); } catch { }
            });
        }
    }
}
