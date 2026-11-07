using System;
using System.Collections.Generic;
using UnityEngine;

namespace ADOFAIMulti
{
    /// <summary>
    /// 글꼴 없이 쓰는 작은 선 아이콘 (64×64 흰색, GUI.color 로 색칠).
    /// 거리 함수(선분·원·호)로 그려서 어떤 크기에서도 부드럽게 보임.
    /// 이름: user crown lock unlock check close gear exit copy refresh search heart play
    ///       flag download pause users music clock star key globe trophy back down chevron plus
    /// </summary>
    public static class Icons
    {
        const int N = 64;
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.TryGetValue(name, out var t)) return t;
            Func<float, float, float> sdf = Shape(name);
            if (sdf == null) { _cache[name] = null; return null; }
            t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // 좌표: 0~24 (위가 0), 텍스처는 아래가 0이라 뒤집음
                    float u = (x + 0.5f) / N * 24f, v = (N - 1 - y + 0.5f) / N * 24f;
                    float d = sdf(u, v);
                    float a = Mathf.Clamp01(0.5f - d * (N / 24f));
                    px[y * N + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px); t.Apply();
            _cache[name] = t;
            return t;
        }

        /// <summary>r 안에 아이콘을 색 c로 그림</summary>
        public static void Draw(Rect r, string name, Color c)
        {
            var tex = Get(name);
            if (tex == null || Event.current.type != EventType.Repaint) return;
            var o = GUI.color; GUI.color = new Color(c.r, c.g, c.b, c.a * o.a);
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
            GUI.color = o;
        }

        // ---------------- 거리 함수 ----------------
        const float W = 1.0f;   // 선 두께의 절반 (24 격자 기준 → 2px 선)

        static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float h = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float ex = px - ax - dx * h, ey = py - ay - dy * h;
            return Mathf.Sqrt(ex * ex + ey * ey);
        }
        static float Line(float px, float py, float ax, float ay, float bx, float by) => Seg(px, py, ax, ay, bx, by) - W;
        static float Circle(float px, float py, float cx, float cy, float r) => Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - r;
        static float Ring(float px, float py, float cx, float cy, float r) => Mathf.Abs(Circle(px, py, cx, cy, r)) - W;
        /// <summary>호: 각도 a0~a1 (도, 0=오른쪽, 시계방향)</summary>
        static float Arc(float px, float py, float cx, float cy, float r, float a0, float a1)
        {
            float ang = Mathf.Atan2(py - cy, px - cx) * Mathf.Rad2Deg; if (ang < 0) ang += 360;
            bool inside = a0 <= a1 ? ang >= a0 && ang <= a1 : ang >= a0 || ang <= a1;
            if (inside) return Ring(px, py, cx, cy, r);
            float d0 = Mathf.Sqrt(Sq(px - cx - r * Mathf.Cos(a0 * Mathf.Deg2Rad)) + Sq(py - cy - r * Mathf.Sin(a0 * Mathf.Deg2Rad)));
            float d1 = Mathf.Sqrt(Sq(px - cx - r * Mathf.Cos(a1 * Mathf.Deg2Rad)) + Sq(py - cy - r * Mathf.Sin(a1 * Mathf.Deg2Rad)));
            return Mathf.Min(d0, d1) - W;
        }
        static float Box(float px, float py, float x0, float y0, float x1, float y1, float rad)
        {
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, hx = (x1 - x0) / 2 - rad, hy = (y1 - y0) / 2 - rad;
            float qx = Mathf.Abs(px - cx) - hx, qy = Mathf.Abs(py - cy) - hy;
            return Mathf.Sqrt(Sq(Mathf.Max(qx, 0)) + Sq(Mathf.Max(qy, 0))) + Mathf.Min(Mathf.Max(qx, qy), 0) - rad;
        }
        static float BoxLine(float px, float py, float x0, float y0, float x1, float y1, float rad) => Mathf.Abs(Box(px, py, x0, y0, x1, y1, rad)) - W;
        /// <summary>채운 삼각형</summary>
        static float Tri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float e0 = Edge(px, py, ax, ay, bx, by), e1 = Edge(px, py, bx, by, cx, cy), e2 = Edge(px, py, cx, cy, ax, ay);
            bool inside = (e0 >= 0 && e1 >= 0 && e2 >= 0) || (e0 <= 0 && e1 <= 0 && e2 <= 0);
            float d = Mathf.Min(Seg(px, py, ax, ay, bx, by), Mathf.Min(Seg(px, py, bx, by, cx, cy), Seg(px, py, cx, cy, ax, ay)));
            return inside ? -d : d;
        }
        static float Edge(float px, float py, float ax, float ay, float bx, float by) => (bx - ax) * (py - ay) - (by - ay) * (px - ax);
        static float Sq(float v) => v * v;
        static float U(params float[] d) { float m = d[0]; for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]); return m; }

        static Func<float, float, float> Shape(string name)
        {
            switch (name)
            {
                case "user": return (x, y) => U(Ring(x, y, 12, 8, 4), Arc(x, y, 12, 22, 8, 180, 360));
                case "users": return (x, y) => U(Ring(x, y, 9, 8.5f, 3.5f), Arc(x, y, 9, 21, 7, 180, 360), Arc(x, y, 16.5f, 8.5f, 3.2f, 280, 120), Arc(x, y, 17, 21, 5.5f, 272, 360));
                case "crown": return (x, y) => U(Line(x, y, 4, 18, 20, 18), Line(x, y, 4, 18, 3, 7), Line(x, y, 3, 7, 8.5f, 12), Line(x, y, 8.5f, 12, 12, 5), Line(x, y, 12, 5, 15.5f, 12), Line(x, y, 15.5f, 12, 21, 7), Line(x, y, 21, 7, 20, 18));
                case "lock": return (x, y) => U(BoxLine(x, y, 5, 11, 19, 21, 2), Arc(x, y, 12, 9, 4, 180, 360), Line(x, y, 8, 9, 8, 11), Line(x, y, 16, 9, 16, 11), Circle(x, y, 12, 16, 1.4f));
                case "unlock": return (x, y) => U(BoxLine(x, y, 5, 11, 19, 21, 2), Arc(x, y, 12, 7, 4, 180, 360), Line(x, y, 8, 7, 8, 11));
                case "check": return (x, y) => U(Line(x, y, 5, 12.5f, 10, 17.5f), Line(x, y, 10, 17.5f, 19, 7));
                case "close": return (x, y) => U(Line(x, y, 6, 6, 18, 18), Line(x, y, 18, 6, 6, 18));
                case "plus": return (x, y) => U(Line(x, y, 12, 5, 12, 19), Line(x, y, 5, 12, 19, 12));
                case "chevron": return (x, y) => U(Line(x, y, 9, 6, 15, 12), Line(x, y, 15, 12, 9, 18));
                case "back": return (x, y) => U(Line(x, y, 15, 6, 9, 12), Line(x, y, 9, 12, 15, 18));
                case "down": return (x, y) => U(Line(x, y, 6, 9, 12, 15), Line(x, y, 12, 15, 18, 9));
                case "gear":
                    return (x, y) =>
                    {
                        float d = U(Ring(x, y, 12, 12, 3), Ring(x, y, 12, 12, 7));
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            d = Mathf.Min(d, Line(x, y, 12 + Mathf.Cos(a) * 8, 12 + Mathf.Sin(a) * 8, 12 + Mathf.Cos(a) * 10, 12 + Mathf.Sin(a) * 10));
                        }
                        return d;
                    };
                case "exit": return (x, y) => U(Line(x, y, 13, 4, 5, 4), Line(x, y, 5, 4, 5, 20), Line(x, y, 5, 20, 13, 20), Line(x, y, 10, 12, 20, 12), Line(x, y, 16, 8, 20, 12), Line(x, y, 16, 16, 20, 12));
                case "copy": return (x, y) => U(BoxLine(x, y, 9, 9, 20, 20, 2), Line(x, y, 15, 4.5f, 6, 4.5f), Line(x, y, 4.5f, 6, 4.5f, 15));
                case "refresh": return (x, y) => U(Arc(x, y, 12, 12, 7.5f, 300, 230), Tri(x, y, 13.5f, 1.5f, 20.5f, 5.5f, 14.5f, 10.5f));
                case "search": return (x, y) => U(Ring(x, y, 10.5f, 10.5f, 6), Line(x, y, 15, 15, 20, 20));
                case "heart":
                    return (x, y) =>
                    {
                        float d = U(Circle(x, y, 8.5f, 9.5f, 4.5f), Circle(x, y, 15.5f, 9.5f, 4.5f), Tri(x, y, 4.2f, 11.5f, 19.8f, 11.5f, 12, 20.5f));
                        return d;
                    };
                case "play": return (x, y) => Tri(x, y, 7, 4.5f, 7, 19.5f, 19.5f, 12);
                case "pause": return (x, y) => U(Box(x, y, 6, 5, 10, 19, 1), Box(x, y, 14, 5, 18, 19, 1));
                case "flag": return (x, y) => U(Line(x, y, 6, 3.5f, 6, 21), Line(x, y, 6, 5, 18, 5), Line(x, y, 18, 5, 15, 9), Line(x, y, 15, 9, 18, 13), Line(x, y, 18, 13, 6, 13));
                case "download": return (x, y) => U(Line(x, y, 12, 4, 12, 15), Line(x, y, 7.5f, 10.5f, 12, 15), Line(x, y, 16.5f, 10.5f, 12, 15), Line(x, y, 4.5f, 19.5f, 19.5f, 19.5f));
                case "music": return (x, y) => U(Circle(x, y, 7.5f, 17.5f, 3), Line(x, y, 10.5f, 17.5f, 10.5f, 4.5f), Line(x, y, 10.5f, 4.5f, 18, 7.5f));
                case "clock": return (x, y) => U(Ring(x, y, 12, 12, 8.5f), Line(x, y, 12, 7, 12, 12), Line(x, y, 12, 12, 15.5f, 14));
                case "star":
                    return (x, y) =>
                    {
                        // 5각 별 (꼭짓점 10개를 잇는 선)
                        float d = 99;
                        for (int i = 0; i < 10; i++)
                        {
                            float a0 = -Mathf.PI / 2 + i * Mathf.PI / 5, a1 = -Mathf.PI / 2 + (i + 1) * Mathf.PI / 5;
                            float r0 = i % 2 == 0 ? 9 : 4, r1 = i % 2 == 0 ? 4 : 9;
                            d = Mathf.Min(d, Line(x, y, 12 + Mathf.Cos(a0) * r0, 12.5f + Mathf.Sin(a0) * r0, 12 + Mathf.Cos(a1) * r1, 12.5f + Mathf.Sin(a1) * r1));
                        }
                        return d;
                    };
                case "key": return (x, y) => U(Ring(x, y, 8, 16, 4), Line(x, y, 11, 13, 20, 4), Line(x, y, 17, 7, 20, 10));
                case "globe": return (x, y) => U(Ring(x, y, 12, 12, 8.5f), Line(x, y, 3.5f, 12, 20.5f, 12), EllipseV(x, y));
                case "trophy": return (x, y) => U(Arc(x, y, 12, 6, 6, 0, 180), Line(x, y, 6, 6, 6, 4), Line(x, y, 18, 6, 18, 4), Line(x, y, 6, 4, 18, 4), Line(x, y, 12, 12, 12, 17), Line(x, y, 8, 20, 16, 20), Arc(x, y, 4.5f, 8, 2.5f, 90, 270), Arc(x, y, 19.5f, 8, 2.5f, 270, 90));
            }
            return null;
        }

        // 지구본 세로 타원
        static float EllipseV(float x, float y)
        {
            float dx = (x - 12) / 4f, dy = (y - 12) / 8.5f;
            float k = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Abs(k - 1) * 4f - W;
        }
    }
}
