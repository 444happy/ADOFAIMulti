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
        static Vector2 _centerCam; static float _unit = 1f;
        static Renderer[] _renderers; static Collider2D[] _colliders;
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
            _renderers = go.GetComponentsInChildren<Renderer>(true).Where(x => x.enabled).ToArray();
            _colliders = go.GetComponentsInChildren<Collider2D>(true);
            _unit = unit;
            var cam = Cam();
            _centerCam = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
            Hidden = false;
            UpdateVisibility();
            Main.Log($"Portal: placed at {best} (unit {unit:0.00}) next to {bestParent.name}");
        }

        static Camera Cam()
        {
            Camera cam = null;
            try { cam = ADOBase.controller?.camy?.camobj; } catch { }
            return cam != null ? cam : Camera.main;
        }

        /// <summary>
        /// 메뉴 시작 화면(정가운데)에 있을 때는 포탈을 숨긴다 — 타이틀 로고 위에 겹쳐 보이기 때문.
        /// 카메라가 시작 지점에서 2.5칸 넘게 벗어나면 다시 보인다.
        /// </summary>
        static void UpdateVisibility()
        {
            var cam = Cam();
            if (cam == null || Floor == null) return;
            bool hide = Vector2.Distance(cam.transform.position, _centerCam) < _unit * 2.5f;
            // 게임이 타일 연출 중에 렌더러를 다시 켤 수 있어서, 숨김 상태면 매 프레임 확인
            if (hide == Hidden && !(hide && _renderers != null && _renderers.Any(r => r != null && r.enabled))) return;
            Hidden = hide;
            if (_renderers != null) foreach (var r in _renderers) if (r != null) r.enabled = !hide;
            if (_colliders != null) foreach (var c in _colliders) if (c != null) c.enabled = !hide;
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
