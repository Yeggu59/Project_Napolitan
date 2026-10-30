// 1인칭 이동. 새 Input System, 액션은 코드에서 정의
// WASD 이동 · 마우스 시점 · Shift 달리기 · Esc 커서 잠금 해제/재잠금

using UnityEngine;
using UnityEngine.InputSystem;

namespace Napolitan.Game.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform cameraRoot;
        [Tooltip("호러 템포 — 기본 걸음은 느리게")]
        [SerializeField] float walkSpeed = 2.2f;
        [SerializeField] float runSpeed = 4f;
        [Tooltip("마우스 1픽셀당 회전 각도")]
        [SerializeField] float lookSensitivity = 0.08f;
        [SerializeField] float pitchLimit = 80f;
        [SerializeField] float gravity = -9.81f;

        CharacterController body;
        InputAction move, look, run, toggleCursor;
        float pitch;
        float verticalVelocity;

        /// <summary>태블릿·저널을 여는 동안 이동/시점 입력 차단</summary>
        public bool InputLocked { get; set; }

        /// <summary>이상현상 행동 판정용 (다가감·뒷걸음질 감지)</summary>
        public Vector3 Velocity => body.velocity;

        public Transform CameraRoot => cameraRoot;

        void Awake()
        {
            body = GetComponent<CharacterController>();

            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            run = new InputAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
            toggleCursor = new InputAction("ToggleCursor", InputActionType.Button, "<Keyboard>/escape");
        }

        void OnEnable()
        {
            move.Enable();
            look.Enable();
            run.Enable();
            toggleCursor.Enable();
            SetCursorLocked(true);
        }

        void OnDisable()
        {
            move.Disable();
            look.Disable();
            run.Disable();
            toggleCursor.Disable();
            SetCursorLocked(false);
        }

        void OnDestroy()
        {
            move.Dispose();
            look.Dispose();
            run.Dispose();
            toggleCursor.Dispose();
        }

        void Update()
        {
            if (toggleCursor.WasPressedThisFrame())
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            bool canControl = !InputLocked && Cursor.lockState == CursorLockMode.Locked;
            if (canControl) Look();
            Move(canControl ? move.ReadValue<Vector2>() : Vector2.zero);
        }

        void Look()
        {
            var delta = look.ReadValue<Vector2>() * lookSensitivity;
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            cameraRoot.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        void Move(Vector2 input)
        {
            float speed = run.IsPressed() ? runSpeed : walkSpeed;
            var horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

            if (body.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            body.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
