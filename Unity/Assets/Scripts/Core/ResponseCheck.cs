// 행동 판정 (ResponseCheck): 현상(Active) 중 플레이어 행동을 대응 키워드 id로 바꾼다
// 순수 로직 — 게임 쪽은 매 프레임 거리·시선·몸 방향·눈 감기만 넘긴다

namespace Napolitan.Core
{
    /// <summary>감지 임계값 (초기값 — 플레이테스트 후 조정)</summary>
    public class ResponseCheckSettings
    {
        /// <summary>현상 시작 후 이 시간 동안은 판정하지 않음 (소리를 듣고 반응할 시간). 끝나는 순간 위치가 기준 거리</summary>
        public float ReactionSeconds = 1f;
        public float StareSeconds = 2f;
        public float LookAwaySeconds = 3f;
        /// <summary>기준 위치보다 이만큼 가까워지면 다가감 (m, 수평)</summary>
        public float ApproachDistance = 1f;
        /// <summary>기준 위치보다 이만큼 멀어지면 뒷걸음질(자판기를 향한 채) / 등 돌려 벗어남 (m, 수평)</summary>
        public float BackAwayDistance = 2f;
        public float CloseEyesSeconds = 4f;
        public float TimeoutSeconds = 8f;
    }

    public static class ResponseActions
    {
        public const string Stare = "kw.resp.stare";
        public const string Approach = "kw.resp.approach";
        public const string LookAway = "kw.resp.dont_look_vending";
        public const string BackAway = "kw.resp.back_away";
        public const string CloseEyes = "kw.resp.close_eyes";
        /// <summary>등을 보이고 멀어짐 (키워드 아님)</summary>
        public const string TurnAway = PlayerActions.TurnAway;
    }

    public class ResponseCheck
    {
        readonly ResponseCheckSettings settings;
        float referenceDistance;
        float closedTime;
        bool eyesLatched, timeoutReported;

        public ResponseCheck(ResponseCheckSettings settings = null)
        {
            this.settings = settings ?? new ResponseCheckSettings();
        }

        // 디버그 표시용
        public float Elapsed { get; private set; }
        public float StareTime { get; private set; }
        public float AwayTime { get; private set; }
        public float ClosedTime => closedTime;
        public float DistanceChange { get; private set; }
        public bool Reacting => Elapsed < settings.ReactionSeconds;

        /// <summary>현상 시작: 기준 거리와 타이머 초기화</summary>
        public void Begin(float distance)
        {
            Elapsed = 0f;
            timeoutReported = false;
            eyesLatched = false;
            closedTime = 0f;
            Continue(distance);
        }

        /// <summary>판정 결과가 "아무 일 없음"이라 현상이 계속될 때: 기준 거리·시선 타이머만 다시 잡음 (무반응 타이머는 유지)</summary>
        public void Continue(float distance)
        {
            referenceDistance = distance;
            DistanceChange = 0f;
            StareTime = 0f;
            AwayTime = 0f;
        }

        /// <summary>한 프레임 진행. 행동이 확정되면 키워드 id(또는 timeout·turn_away), 아니면 null</summary>
        /// <param name="facing">몸(카메라 수평 방향)이 대상 쪽 ±90° 이내</param>
        public string Tick(float dt, float distance, bool gazing, bool facing, bool eyesClosed)
        {
            Elapsed += dt;
            if (Reacting)
            {
                referenceDistance = distance; // 반응 시간이 끝나는 순간의 위치가 기준
                return null;
            }

            if (eyesClosed)
            {
                StareTime = 0f;
                AwayTime = 0f;
                if (!eyesLatched)
                {
                    closedTime += dt;
                    if (closedTime >= settings.CloseEyesSeconds)
                    {
                        eyesLatched = true; // 눈을 떴다 다시 감아야 다시 판정
                        return ResponseActions.CloseEyes;
                    }
                }
            }
            else
            {
                closedTime = 0f;
                eyesLatched = false;
            }

            // 이동이 시선보다 우선 (뒷걸음질 중에 자판기가 보여도 응시가 아님)
            DistanceChange = distance - referenceDistance;
            if (-DistanceChange >= settings.ApproachDistance) return ResponseActions.Approach;
            if (DistanceChange >= settings.BackAwayDistance) return facing ? ResponseActions.BackAway : ResponseActions.TurnAway;

            if (!eyesClosed)
            {
                if (gazing)
                {
                    AwayTime = 0f;
                    StareTime += dt;
                    if (StareTime >= settings.StareSeconds) return ResponseActions.Stare;
                }
                else
                {
                    StareTime = 0f;
                    AwayTime += dt;
                    if (AwayTime >= settings.LookAwaySeconds) return ResponseActions.LookAway;
                }
            }

            if (!timeoutReported && Elapsed >= settings.TimeoutSeconds)
            {
                timeoutReported = true;
                return PlayerActions.Timeout;
            }
            return null;
        }
    }
}
