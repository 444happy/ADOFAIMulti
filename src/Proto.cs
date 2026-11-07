using System;
using System.Collections.Generic;
using System.IO;

namespace ADOFAIMulti
{
    public enum Msg : byte
    {
        Hello = 1,      // C->H  name, modVer
        Welcome,        // H->C
        Reject,         // H->C  reason
        MapOffer,       // H->C  hash, size, relLevelFile, title
        MapHave,        // C->H  hash
        MapNeed,        // C->H  hash, offset   (다운 시작/재개)
        MapChunk,       // H->C  hash, offset, bytes
        MapAck,         // C->H  hash, nextOffset
        MapPause,       // C->H
        MapCancel,      // C->H
        LoadLevel,      // H->C  hash
        Loaded,         // C->H  (레벨 로딩 끝, 시작 대기 중)
        Ready,          // C->H  bool
        Go,             // H->C  delayMs
        Progress,       // C->H  Stats
        Board,          // H->C  List<Entry>
        Results,        // H->C  List<Entry>
        BackToLobby,    // H->C
        MapProgress,    // C->H  float (TUF에서 직접 받는 중 진행률)
    }

    public enum PState : byte { Lobby, Downloading, Paused, Cancelled, HasMap, Loading, Loaded, Ready, Playing, Finished }

    /// <summary>판정 세부 수치</summary>
    public class Stats
    {
        public float Acc, XAcc, Progress;
        public int TooEarly, VeryEarly, EarlyPerfect, Perfect, LatePerfect, VeryLate, TooLate, Miss, Overload;
        public bool Finished;

        public void Write(BinaryWriter w)
        {
            w.Write(Acc); w.Write(XAcc); w.Write(Progress);
            w.Write(TooEarly); w.Write(VeryEarly); w.Write(EarlyPerfect); w.Write(Perfect);
            w.Write(LatePerfect); w.Write(VeryLate); w.Write(TooLate); w.Write(Miss); w.Write(Overload);
            w.Write(Finished);
        }
        public static Stats Read(BinaryReader r)
        {
            return new Stats
            {
                Acc = r.ReadSingle(), XAcc = r.ReadSingle(), Progress = r.ReadSingle(),
                TooEarly = r.ReadInt32(), VeryEarly = r.ReadInt32(), EarlyPerfect = r.ReadInt32(), Perfect = r.ReadInt32(),
                LatePerfect = r.ReadInt32(), VeryLate = r.ReadInt32(), TooLate = r.ReadInt32(), Miss = r.ReadInt32(), Overload = r.ReadInt32(),
                Finished = r.ReadBoolean()
            };
        }
    }

    /// <summary>랭킹 한 줄</summary>
    public class Entry
    {
        public ulong Id;
        public string Name = "";
        public PState State;
        public float Download;   // 0~1
        public bool LobbyReady;  // 로비에서 준비 눌렀는지 (맵을 받은 뒤)
        public Stats Stats = new Stats();

        public void Write(BinaryWriter w)
        {
            w.Write(Id); w.Write(Name ?? ""); w.Write((byte)State); w.Write(Download); w.Write(LobbyReady); Stats.Write(w);
        }
        public static Entry Read(BinaryReader r)
        {
            return new Entry { Id = r.ReadUInt64(), Name = r.ReadString(), State = (PState)r.ReadByte(), Download = r.ReadSingle(), LobbyReady = r.ReadBoolean(), Stats = Stats.Read(r) };
        }

        /// <summary>정확도 → X정확도 → 미스 적은 순</summary>
        public static int Compare(Entry a, Entry b)
        {
            int c = b.Stats.Acc.CompareTo(a.Stats.Acc);
            if (c != 0) return c;
            c = b.Stats.XAcc.CompareTo(a.Stats.XAcc);
            if (c != 0) return c;
            return (a.Stats.Miss + a.Stats.TooEarly + a.Stats.TooLate).CompareTo(b.Stats.Miss + b.Stats.TooEarly + b.Stats.TooLate);
        }
    }

    public static class Packet
    {
        public static byte[] Build(Msg type, Action<BinaryWriter> body = null)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((byte)type);
                body?.Invoke(w);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static void WriteEntries(BinaryWriter w, List<Entry> list)
        {
            w.Write(list.Count);
            foreach (var e in list) e.Write(w);
        }

        public static List<Entry> ReadEntries(BinaryReader r)
        {
            int n = r.ReadInt32();
            var list = new List<Entry>(n);
            for (int i = 0; i < n; i++) list.Add(Entry.Read(r));
            return list;
        }
    }
}
