// 화면 중앙 레이캐스트 + E키 조사
// 결과는 ClueFound 이벤트로만 내보낸다 — 저널은 이 이벤트를 구독

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Napolitan.Game.Interaction
{
    public class Inspector : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] float range = 2.5f;
        [SerializeField] LayerMask mask = ~0;

        InputAction inspect;

        /// <summary>조사로 Clue를 얻었을 때 (clue id)</summary>
        public event Action<string> ClueFound;

        /// <summary>지금 조준 중인 대상 (없으면 null)</summary>
        public IInspectable Current { get; private set; }

        void Awake()
        {
            inspect = new InputAction("Inspect", InputActionType.Button, "<Keyboard>/e");
        }

        void OnEnable() => inspect.Enable();
        void OnDisable() => inspect.Disable();
        void OnDestroy() => inspect.Dispose();

        void Update()
        {
            Current = FindTarget();
            if (Current == null || !inspect.WasPressedThisFrame()) return;

            var clues = Current.Inspect();
            if (clues.Count == 0) Debug.Log($"[조사] {Current.Prompt} → 단서 없음");
            foreach (var clueId in clues)
            {
                Debug.Log($"[조사] {Current.Prompt} → {clueId}");
                ClueFound?.Invoke(clueId);
            }
        }

        IInspectable FindTarget()
        {
            var ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out var hit, range, mask, QueryTriggerInteraction.Collide)) return null;
            return hit.collider.GetComponentInParent<IInspectable>();
        }
    }
}
