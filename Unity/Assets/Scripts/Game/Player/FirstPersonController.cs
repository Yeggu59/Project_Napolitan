// 1인칭 이동. 새 Input System, 액션은 코드에서 정의
// WASD 이동 · 마우스 시점 · Shift 달리기 · Esc 커서 해제 · 좌클릭/포커스 복귀 시 재잠금

using UnityEngine;
using UnityEngine.InputSystem;

namespace Napolitan.Game.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform cameraRoot;
        [Tooltip("초기값 — 유저 피드백으로 2배 (2026-10-31)")]
        [SerializeField] float walkSpeed = 4.4f;
        [SerializeField] float runSpeed = 8f;
        [Tooltip("마우스 1픽셀당 회전 각도 — Play 중에도 인스펙터에서 조절 가능")]
        [SerializeField] float lookSensitivity = 0.08f;
        [SerializeField] float pitchLimit = 80f;
        [SerializeField] float gravity = -9.81f;

        CharacterController body;
        InputAction move, look, run, releaseCursor, relockCursor;
        float pitch;
        float verticalVelocity;
        GUIStyle resumeStyle;

        /// <summary>태블릿·저널·사망 연출 동안 이동/시점 입력 차단 (커서 자동 재잠금도 안 함)</summary>
        public bool InputLocked { get; set; }

        /// <summary>지금 플레이어가 조작 중인지 (입력 차단 없음 + 커서 잠김)</summary>
        public bool HasControl => !InputLocked && Cursor.lockState == CursorLockMode.Locked;

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
            releaseCursor = new InputAction("ReleaseCursor", InputActionType.Button, "<Keyboard>/escape");
            relockCursor = new InputAction("RelockCursor", InputActionType.Button, "<Mouse>/leftButton");
        }

        void OnEnable()
        {
            move.Enable();
            look.Enable();
            run.Enable();
            releaseCursor.Enable();
            relockCursor.Enable();
            SetCursorLocked(true);
        }

        void OnDisable()
        {
            move.Disable();
            look.Disable();
            run.Disable();
            releaseCursor.Disable();
            relockCursor.Disable();
            SetCursorLocked(false);
        }

        void OnDestroy()
        {
            move.Dispose();
            look.Dispose();
            run.Dispose();
            releaseCursor.Dispose();
            relockCursor.Dispose();
        }

        // 에디터는 Game 뷰가 포커스를 잃으면 잠금을 직접 푼다 → 돌아오면 다시 건다
        void OnApplicationFocus(bool focused)
        {
            if (focused && !InputLocked) SetCursorLocked(true);
        }

        void Update()
        {
            // Esc는 해제만 (에디터의 Esc 해제와 토글이 겹쳐 꼬이지 않게)
            if (releaseCursor.WasPressedThisFrame()) SetCursorLocked(false);
            else if (relockCursor.WasPressedThisFrame() && !InputLocked && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);

            bool canControl = HasControl;
            if (canControl) Look();
            Move(canControl ? move.ReadValue<Vector2>() : Vector2.zero);
        }

        void OnGUI()
        {
            if (InputLocked || Cursor.lockState == CursorLockMode.Locked) return;
            GUI.depth = -20;
            resumeStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
            GUI.Label(new Rect(0f, Screen.height / 2f - 20f, Screen.width, 40f), "클릭해서 계속", resumeStyle);
        }

        /// <summary>밴 재시작 등 순간이동. 시점은 정면(yaw)으로 초기화</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            body.enabled = true;
            pitch = 0f;
            cameraRoot.localEulerAngles = Vector3.zero;
            verticalVelocity = 0f;
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
