// 현상(Active) 중 플레이어의 거리·시선·눈 감기를 재서 ResponseCheck에 넘기고, 확정된 행동을 판정에 보낸다

using Napolitan.Core;
using Napolitan.Game.Dispatch;
using Napolitan.Game.Player;
using UnityEngine;

namespace Napolitan.Game.Anomaly
{
    public class ResponseDetector : MonoBehaviour
    {
        [SerializeField] DispatchManager dispatch;
        [SerializeField] string anomalyId = "anom.red_vending";
        [SerializeField] FirstPersonController player;
        [SerializeField] Eyes eyes;
        [SerializeField] Transform viewCamera;
        [Tooltip("시선 판정 기준점 (자판기 중심)")]
        [SerializeField] Transform gazeTarget;
        [Tooltip("화면 중앙에서 이 각도 안에 있으면 쳐다보는 것")]
        [SerializeField] float gazeAngle = 20f;
        [SerializeField] float gazeRange = 15f;

        [Header("감지 임계값 (초기값)")]
        [SerializeField] float stareSeconds = 2f;
        [SerializeField] float lookAwaySeconds = 3f;
        [SerializeField] float approachDistance = 1f;
        [SerializeField] float backAwayDistance = 2f;
        [SerializeField] float closeEyesSeconds = 4f;
        [SerializeField] float timeoutSeconds = 8f;

        ResponseCheck check;
        bool running;

        /// <summary>디버그 표시용</summary>
        public bool Gazing { get; private set; }
        public bool Running => running;

        public void Begin()
        {
            check = new ResponseCheck(new ResponseCheckSettings
            {
                StareSeconds = stareSeconds,
                LookAwaySeconds = lookAwaySeconds,
                ApproachDistance = approachDistance,
                BackAwayDistance = backAwayDistance,
                CloseEyesSeconds = closeEyesSeconds,
                TimeoutSeconds = timeoutSeconds,
            });
            check.Begin(Distance());
            running = true;
        }

        public void Stop() => running = false;

        void Update()
        {
            Gazing = IsGazing();
            // 커서가 풀려 조작을 멈춘 동안(일시정지)과 사망 연출 중엔 시간을 재지 않음
            if (!running || dispatch.Dying || !player.HasControl) return;

            var action = check.Tick(Time.deltaTime, Distance(), Gazing, eyes.Closed);
            if (action == null) return;

            var result = dispatch.Respond(anomalyId, action);
            if (result != null && result.Type == OutcomeType.Nothing) check.Continue(Distance());
        }

        float Distance()
        {
            var delta = gazeTarget.position - viewCamera.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        bool IsGazing()
        {
            var toTarget = gazeTarget.position - viewCamera.position;
            return toTarget.magnitude <= gazeRange && Vector3.Angle(viewCamera.forward, toTarget) <= gazeAngle;
        }
    }
}
