// 행동 판정 (ResponseCheck): 현상(Active) 중 플레이어 행동을 대응 키워드 id로 바꾼다
// 순수 로직 — 게임 쪽은 매 프레임 거리·시선·눈 감기만 넘긴다

namespace Napolitan.Core
{
    /// <summary>감지 임계값 (초기값 — 플레이테스트 후 조정)</summary>
    public class ResponseCheckSettings
    {
        public float StareSeconds = 2f;
        public float LookAwaySeconds = 3f;
        /// <summary>현상 시작 위치보다 이만큼 가까워지면 다가감 (m, 수평)</summary>
        public float ApproachDistance = 1f;
        /// <summary>현상 시작 위치보다 이만큼 멀어지면 뒷걸음질 (m, 수평, 시선 무관)</summary>
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
    }

    public class ResponseCheck
    {
        readonly ResponseCheckSettings settings;
        float referenceDistance;
        float elapsed, stareTime, awayTime, closedTime;
        bool eyesLatched, timeoutReported;

        public ResponseCheck(ResponseCheckSettings settings = null)
        {
            this.settings = settings ?? new ResponseCheckSettings();
        }

        /// <summary>현상 시작: 기준 거리와 타이머 초기화</summary>
        public void Begin(float distance)
        {
            elapsed = 0f;
            timeoutReported = false;
            eyesLatched = false;
            closedTime = 0f;
            Continue(distance);
        }

        /// <summary>판정 결과가 "아무 일 없음"이라 현상이 계속될 때: 기준 거리·시선 타이머만 다시 잡음 (무반응 타이머는 유지)</summary>
        public void Continue(float distance)
        {
            referenceDistance = distance;
            stareTime = 0f;
            awayTime = 0f;
        }

        /// <summary>한 프레임 진행. 행동이 확정되면 키워드 id(또는 timeout), 아니면 null</summary>
        public string Tick(float dt, float distance, bool gazing, bool eyesClosed)
        {
            elapsed += dt;

            if (eyesClosed)
            {
                stareTime = 0f;
                awayTime = 0f;
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

            float closer = referenceDistance - distance;
            if (closer >= settings.ApproachDistance) return ResponseActions.Approach;
            if (-closer >= settings.BackAwayDistance) return ResponseActions.BackAway;

            if (!eyesClosed)
            {
                if (gazing)
                {
                    awayTime = 0f;
                    stareTime += dt;
                    if (stareTime >= settings.StareSeconds) return ResponseActions.Stare;
                }
                else
                {
                    stareTime = 0f;
                    awayTime += dt;
                    if (awayTime >= settings.LookAwaySeconds) return ResponseActions.LookAway;
                }
            }

            if (!timeoutReported && elapsed >= settings.TimeoutSeconds)
            {
                timeoutReported = true;
                return PlayerActions.Timeout;
            }
            return null;
        }
    }
}
