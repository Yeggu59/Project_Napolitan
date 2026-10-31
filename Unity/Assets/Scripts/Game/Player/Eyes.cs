// 눈 감기 (행동 판정 close_eyes). 키를 누르고 있는 동안 감음 — 화면 암전은 HarmPresenter

using UnityEngine;
using UnityEngine.InputSystem;

namespace Napolitan.Game.Player
{
    [RequireComponent(typeof(FirstPersonController))]
    public class Eyes : MonoBehaviour
    {
        [SerializeField] string bindingPath = "<Keyboard>/c";

        FirstPersonController controller;
        InputAction close;

        /// <summary>지금 눈을 감고 있는지 (조작 불가 상태면 false)</summary>
        public bool Closed => controller.HasControl && close.IsPressed();

        void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            close = new InputAction("CloseEyes", InputActionType.Button, bindingPath);
        }

        void OnEnable() => close.Enable();
        void OnDisable() => close.Disable();
        void OnDestroy() => close.Dispose();
    }
}
