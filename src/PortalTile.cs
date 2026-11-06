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
        static Vector2 _spawn; static float _unit = 1f;
        // 게임을 켜고 처음 메뉴에 왔을 때 행성이 서 있는 자리 = 메인 섬 정가운데 (한 번 정하면 계속 사용)
        static bool _centerKnown; static Vector2 _center;
        static float _logUntil;
        static readonly HashSet<Renderer> _offByUs = new HashSet<Renderer>();
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
            try { Create(); }
            catch (Exception ex) { Failed = true; Main.Log("Portal create failed: " + ex); }
        }

        static void Create()
        {
            var floors = UnityEngine.Object.FindObjectsOfType<scrFloor>().Where(f => f != null && f.gameObject.activeInHierarchy).ToList();
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
            _offByUs.Clear();
            // 시작 화면 중심: 메뉴의 원점 타일(타이틀 아래 3x3 가운데). 원점에 타일이 없으면 행성 위치.
            // (레벨에서 나와 메뉴가 다른 곳에서 시작해도 기준이 흔들리지 않게 원점 우선)
            _spawn = pos.Any(q => q.magnitude < unit * 0.5f) ? Vector2.zero : (PlayerPos() ?? Vector2.zero);
            _logUntil = Time.unscaledTime + 6f;
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
        /// 메뉴 시작 화면(정가운데)에 있을 때는 포탈을 숨긴다 — 타이틀 로고 위에 겹쳐 보이기 때문.
        /// 행성이 정가운데 타일에 있을 때만 숨기고, 한 칸이라도 벗어나면 보인다.
        /// 메뉴 등장 연출 중 게임이 렌더러를 나중에 켜기도 해서, 숨김 상태면 매 프레임 전부 다시 끈다.
        /// </summary>
        static void UpdateVisibility(bool force = false)
        {
            if (Floor == null) return;
            var pos = PlayerPos();
            if (!_centerKnown && pos != null) { _centerKnown = true; _center = pos.Value; Main.Log($"Portal: center = {_center} (origin guess {_spawn})"); }
            // 정가운데 타일 위에 있을 때만 숨김 (처음 서 있던 자리, 또는 원점 타일)
            bool hide = pos == null
                        || (_centerKnown && Vector2.Distance(pos.Value, _center) < _unit * 0.6f)
                        || Vector2.Distance(pos.Value, _spawn) < _unit * 0.6f;
            if (Time.unscaledTime < _logUntil && Time.frameCount % 60 == 0)
                Main.Log($"Portal vis: pos={(pos.HasValue ? pos.Value.ToString() : "null")} center={_center} spawn={_spawn} hide={hide}");
            var go = Floor.gameObject;
            if (hide)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    if (r.enabled) { r.enabled = false; _offByUs.Add(r); }
                foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
            }
            else if (Hidden || force)
            {
                foreach (var r in _offByUs) if (r != null) r.enabled = true;
                _offByUs.Clear();
                foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
            }
            Hidden = hide;
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
