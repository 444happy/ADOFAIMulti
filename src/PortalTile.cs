using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ADOFAIMulti
{
    /// <summary>
    /// 메인 섬에 "멀티 포탈" 타일을 하나 추가한다.
    ///
    /// 메인 섬은 자유 이동: 행성이 향한 칸에 "Floor" 레이어 충돌체가 있으면 그 칸으로 이동하고,
    /// 그 타일이 isportal이면 scrController.OnLandOnPortal(행성, 목적지, arguments)를 부른다.
    /// → 기존 포탈 타일(커스텀 레벨 포탈)을 복제해서 비어 있는 이웃 칸에 놓고 arguments = "ADOFAIMulti" 로 표시,
    ///   OnLandOnPortal 에서 그 인자를 가로채 멀티 섬으로 보낸다.
    /// </summary>
    public static class PortalTile
    {
        public const string Arg = "ADOFAIMulti";
        public static scrFloor Floor;          // 만들어진 포탈 (없으면 null)
        public static bool Failed;             // 놓을 자리를 못 찾음 → UI가 대체 버튼 표시
        static int _sceneHandle = -1;
        static float _sceneSeenAt;
        // 시작 화면(정가운데) 판정: 포탈을 만들 때의 카메라 위치 = 메뉴 시작 지점
        static float _unit = 1f;
        // 게임을 켜고 처음 메뉴에 왔을 때 행성이 서 있는 자리 = 메인 섬 정가운데 (한 번 정하면 계속 사용)
        static bool _centerKnown; static Vector2 _center;
        public static bool Hidden;

        /// <summary>Runner.Update에서 호출: 메인 섬이 새로 열릴 때마다 포탈을 한 번 만든다</summary>
        public static void Tick()
        {
            if (Island.InIsland || Island.Busy || Game.IsScnGame) return;
            bool menu = Game.IsMainMenu;
            if (!menu) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.handle != _sceneHandle)
            {
                _sceneHandle = scene.handle; _sceneSeenAt = Time.unscaledTime;
                Floor = null; Failed = false;
                return;
            }
            if (Floor != null) { UpdateVisibility(); return; }
            if (Failed) return;
            if (Time.unscaledTime - _sceneSeenAt < 0.8f) return;   // 메뉴 초기화(타일 생성) 기다림
            // 포탈은 행성이 정가운데에서 처음 벗어날 때 생성 (게임 시작 화면엔 아예 없음)
            var now = PlayerPos();
            if (now == null) return;
            if (!_centerKnown) { _centerKnown = true; _center = now.Value; Main.Log($"Portal: center = {_center}"); }
            if (Vector2.Distance(now.Value, _center) < 0.5f) return;
            try { Create(); }
            catch (Exception ex) { Failed = true; Main.Log("Portal create failed: " + ex); }
        }

        static void Create()
        {
            var floors = UnityEngine.Object.FindObjectsByType<scrFloor>(FindObjectsSortMode.InstanceID).Where(f => f != null && f.gameObject.activeInHierarchy).ToList();
            if (floors.Count < 3) return;   // 아직 메뉴가 덜 만들어짐 → 다음 프레임

            // 원본: 커스텀 레벨 포탈 → 없으면 아무 포탈
            var template = floors.FirstOrDefault(f => f.isportal && f.levelnumber == Portal.CustomLevelsScene)
                           ?? floors.FirstOrDefault(f => f.isportal);
            if (template == null) { Failed = true; Main.Log("Portal: no template portal in menu"); return; }

            // 칸 간격 = 타일 사이 최소 거리
            var pos = floors.Select(f => (Vector2)f.transform.position).ToList();
            float unit = float.MaxValue;
            for (int i = 0; i < pos.Count; i++)
                for (int j = i + 1; j < pos.Count; j++)
                {
                    float d = Vector2.Distance(pos[i], pos[j]);
                    if (d > 0.3f && d < unit) unit = d;
                }
            if (unit == float.MaxValue || unit > 5f) unit = 1.5f;

            int floorMask = 1 << LayerMask.NameToLayer("Floor");
            bool Occupied(Vector2 p, float r) =>
                Physics2D.OverlapCircleAll(p, r, floorMask).Length > 0 || pos.Any(q => Vector2.Distance(q, p) < r);

            // 후보: 착지 가능한 일반 타일의 4방향 빈 칸.
            // 조건: 그 칸과 주변(자기 부모 타일 제외)이 비어 있을 것 → 다른 길과 붙지 않는 막다른 칸
            // 점수: 커스텀 레벨 포탈과 가까울수록 좋음 (커스텀 레벨 옆에 "멀티"가 있는 게 자연스러움)
            var dirs = new[] { Vector2.right, Vector2.down, Vector2.left, Vector2.up };
            Vector2 tpos = template.transform.position;
            Vector2 best = Vector2.zero; float bestScore = float.MaxValue; scrFloor bestParent = null;
            foreach (var f in floors)
            {
                if (f.isportal || !f.isLandable) continue;
                Vector2 fp = f.transform.position;
                foreach (var d in dirs)
                {
                    var c = fp + d * unit;
                    if (Occupied(c, unit * 0.45f)) continue;
                    bool crowded = false;
                    foreach (var d2 in dirs)
                    {
                        var n = c + d2 * unit;
                        if (Vector2.Distance(n, fp) < 0.1f) continue;
                        if (Occupied(n, unit * 0.45f)) { crowded = true; break; }
                    }
                    if (crowded) continue;
                    float score = Vector2.Distance(c, tpos) + (d == Vector2.right || d == Vector2.down ? 0 : unit * 0.5f);
                    if (score < bestScore) { bestScore = score; best = c; bestParent = f; }
                }
            }
            if (bestParent == null) { Failed = true; Main.Log("Portal: no free cell found"); return; }

            var go = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = "ADOFAIMulti_Portal";
            go.transform.position = new Vector3(best.x, best.y, template.transform.position.z);
            // 원본 포탈의 글자(“커스텀 레벨” 등)는 숨김 — 우리 라벨은 UI에서 그린다
            foreach (var tm in go.GetComponentsInChildren<TextMesh>(true)) tm.gameObject.SetActive(false);
            foreach (var t in go.GetComponentsInChildren<UnityEngine.UI.Text>(true)) t.gameObject.SetActive(false);
            foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) c.enabled = true;

            var fl = go.GetComponent<scrFloor>();
            fl.isportal = true;
            fl.levelnumber = Portal.CustomLevelsScene;    // 아이콘 모양 유지용 (실제 이동은 가로챔)
            fl.arguments = Arg;
            fl.isLandable = true;
            Floor = fl;
            _unit = unit;
            AddVisual(go, template, unit);

            Hidden = false;
            UpdateVisibility(true);
            Main.Log($"Portal: placed at {best} (unit {unit:0.00}) next to {bestParent.name}");
        }

        static Camera Cam()
        {
            Camera cam = null;
            try { cam = ADOBase.controller?.camy?.camobj; } catch { }
            return cam != null ? cam : Camera.main;
        }

        /// <summary>
        /// 행성 중심축(지금 서 있는 쪽 행성)의 위치. 게임을 막 켰을 땐 currFloor 가 아직 비어 있을 수 있어서
        /// 행성 위치를 먼저 보고, 그것도 없으면 null (= 시작 화면으로 보고 숨김)
        /// </summary>
        static Vector2? PlayerPos()
        {
            try
            {
                var c = ADOBase.controller;
                if (c == null) return null;
                var p = c.chosenPlanet;
                if (p != null) return (Vector2)p.transform.position;
                var f = c.currFloor;
                if (f != null) return (Vector2)f.transform.position;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 정가운데 타일에 있을 때는 포탈을 숨긴다 (타이틀 로고와 겹침), 한 칸이라도 벗어나면 보인다.
        /// 얼불춤은 타일을 개별 Renderer 가 아니라 따로 모아서 그리기 때문에 렌더러를 꺼도 화면에 남는다
        /// → 포탈 오브젝트 자체를 비활성화한다.
        /// </summary>
        static void UpdateVisibility(bool force = false)
        {
            if (Floor == null) return;
            var pos = PlayerPos();
            bool hide = pos == null || (_centerKnown && Vector2.Distance(pos.Value, _center) < _unit * 0.6f);
            if (hide == Hidden && !force) return;
            Hidden = hide;
            var go = Floor.gameObject;
            if (go.activeSelf == hide) go.SetActive(!hide);
        }

        // ---------------- 포탈 모양을 모드가 직접 그리기 ----------------
        // 얼불춤은 메뉴 타일을 메뉴 생성 때 한꺼번에 등록해서 그리므로, 나중에 복제한 타일은 화면에 안 나온다.
        // → 일반 SpriteRenderer 로 타일 + 불/얼음 포탈 고리를 직접 붙인다.
        static Sprite _tileSprite, _ringSprite;

        static void AddVisual(GameObject go, scrFloor template, float unit)
        {
            if (_tileSprite == null)
            {
                _tileSprite = MakeSprite(128, (x, y) =>
                {
                    // 둥근 모서리 사각형, 다른 메뉴 타일처럼 연한 회색
                    float r = 10, n = 128;
                    float dx = Mathf.Max(0, Mathf.Max(r - x, x - (n - 1 - r))), dy = Mathf.Max(0, Mathf.Max(r - y, y - (n - 1 - r)));
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    float shade = 0.80f + 0.06f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                    return new Color(shade, shade, shade + 0.04f, a);
                });
                _ringSprite = MakeSprite(128, (x, y) =>
                {
                    var c = new Vector2(x - 63.5f, y - 63.5f);
                    float d = c.magnitude / 64f;
                    float ang = (Mathf.Atan2(c.y, c.x) / Mathf.PI + 1f) * 0.5f;           // 0~1 둘레 위치
                    var ring = Color.Lerp(new Color(1f, 0.42f, 0.30f), new Color(0.35f, 0.72f, 1f), Mathf.PingPong(ang * 2f, 1f));
                    float glow = Mathf.Clamp01(1f - Mathf.Abs(d - 0.62f) / 0.22f);           // 불/얼음 고리
                    float core = Mathf.Clamp01((0.42f - d) / 0.04f);                       // 흰 중심
                    var col = Color.Lerp(new Color(ring.r, ring.g, ring.b, glow * glow), Color.white, core);
                    col.a = Mathf.Max(glow * glow, core);
                    return col;
                });
            }

            // 템플릿 타일이 쓰는 정렬 레이어를 따라가서 배경 위·행성 아래에 그려지게
            int layerId = 0, order = 0;
            var refSr = template.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).OrderByDescending(r => r.sortingOrder).FirstOrDefault();
            if (refSr != null) { layerId = refSr.sortingLayerID; order = refSr.sortingOrder; }

            var holder = new GameObject("ADOFAIMulti_PortalVisual");
            holder.transform.SetParent(go.transform, false);
            holder.transform.position = go.transform.position;
            holder.transform.rotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;
            var lossy = go.transform.lossyScale;
            var fix = new Vector3(lossy.x != 0 ? 1f / lossy.x : 1f, lossy.y != 0 ? 1f / lossy.y : 1f, 1f);  // 부모 스케일 상쇄 → 월드 크기 = 1칸

            var tile = new GameObject("Tile").AddComponent<SpriteRenderer>();
            tile.transform.SetParent(holder.transform, false);
            tile.sprite = _tileSprite; tile.sortingLayerID = layerId; tile.sortingOrder = order + 1;
            tile.transform.localScale = Vector3.Scale(fix, Vector3.one * unit * 0.94f);

            var ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(holder.transform, false);
            ring.sprite = _ringSprite; ring.sortingLayerID = layerId; ring.sortingOrder = order + 2;
            ring.transform.localScale = Vector3.Scale(fix, Vector3.one * unit * 0.86f);
            holder.AddComponent<PortalSpin>().Ring = ring.transform;
        }

        static Sprite MakeSprite(int n, Func<int, int, Color> px)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) t.SetPixel(x, y, px(x, y));
            t.Apply();
            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1유닛 = 텍스처 전체
        }

        /// <summary>포탈의 화면 좌표 (라벨 그리기용). 안 보이면 false</summary>
        public static bool ScreenPos(out Vector2 p)
        {
            p = default;
            if (Floor == null || Hidden) return false;
            var cam = Cam();
            if (cam == null) return false;
            var sp = cam.WorldToScreenPoint(Floor.transform.position);
            if (sp.z < 0) return false;
            p = new Vector2(sp.x, Screen.height - sp.y);   // IMGUI 좌표 (y 뒤집기)
            return p.x > -100 && p.x < Screen.width + 100 && p.y > -100 && p.y < Screen.height + 100;
        }
    }

    // 멀티 포탈에 착지하면 원래 포탈 이동 대신 멀티 섬으로
    [HarmonyPatch(typeof(scrController), nameof(scrController.OnLandOnPortal))]
    static class Patch_MultiPortal
    {
        static bool Prefix(string portalArguments)
        {
            if (portalArguments != PortalTile.Arg) return true;
            if (Updater.UpdateRequired) return false;   // 구버전: 표지판에 '업데이트 필요' 표시, 입장 막음
            Island.Enter();
            return false;
        }
    }
}

namespace ADOFAIMulti
{
    /// <summary>포탈 고리를 천천히 돌리고 살짝 숨쉬게</summary>
    public class PortalSpin : MonoBehaviour
    {
        public Transform Ring;
        Vector3 _base;
        void Start() { if (Ring != null) _base = Ring.localScale; }
        void Update()
        {
            if (Ring == null) return;
            Ring.Rotate(0, 0, -40f * Time.unscaledDeltaTime);
            Ring.localScale = _base * (1f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.04f);
        }
    }
}
