// 이상현상 변형·결과 표·정답 분기

using System;
using System.Collections.Generic;
using System.Linq;

namespace Napolitan.Core
{
    public enum AnomalyState
    {
        Dormant,
        Omen,
        Active,
        Resolved,
        Violated,
    }

    public enum OutcomeType
    {
        /// <summary>무사 — 이상현상 해결</summary>
        Resolve,
        /// <summary>아무 일 없음 — Active 유지</summary>
        Nothing,
        /// <summary>즉시 피해 (결손 부여)</summary>
        ImmediateHarm,
        /// <summary>지연 — 그 자리에선 아무 일 없고 표식만 남음</summary>
        ApplyMark,
        /// <summary>즉사 — 목숨 차감</summary>
        Fatal,
    }

    /// <summary>현장 행동 id. 대응 키워드 id를 그대로 쓰고, 키워드가 아닌 행동만 여기 정의</summary>
    public static class PlayerActions
    {
        /// <summary>Active 후 일정 시간 아무 대응도 안 함</summary>
        public const string Timeout = "timeout";
        /// <summary>등을 보이고 멀어짐 — "등을 보이지 말고 뒷걸음질"의 오답</summary>
        public const string TurnAway = "turn_away";
        /// <summary>결과 표에서 "아무 행동"</summary>
        public const string Any = "*";
    }

    [Serializable]
    public class OutcomeResult
    {
        public OutcomeType Type;
        /// <summary>ImmediateHarm일 때 결손 id</summary>
        public string Impairment;
        /// <summary>ApplyMark일 때 표식 id</summary>
        public string Mark;
        /// <summary>이 결과를 겪으면 해금되는 키워드</summary>
        public List<string> UnlockKeywords = new List<string>();
        /// <summary>저널 현장 기록 문장 (겪은 것). 표식처럼 숨은 결과는 드러내지 않게 쓸 것. 없으면 기록 안 함</summary>
        public string Record;

        public static OutcomeResult Nothing() => new OutcomeResult { Type = OutcomeType.Nothing };
    }

    /// <summary>결과 표 한 줄. 위에서부터 첫 번째로 일치하는 줄이 적용됨 (같은 상태·같은 행동 → 같은 결과)</summary>
    [Serializable]
    public class OutcomeRule
    {
        /// <summary>행동 id (대응 키워드 id / "timeout" / "*")</summary>
        public string Action;
        public List<string> RequireMarks = new List<string>();
        public List<string> ForbidMarks = new List<string>();
        public OutcomeResult Result = new OutcomeResult();

        public bool Matches(string action, PlayerStatus status)
        {
            if (Action != PlayerActions.Any && Action != action) return false;
            if (!RequireMarks.All(status.HasMark)) return false;
            return !ForbidMarks.Any(status.HasMark);
        }
    }

    /// <summary>정산 채점 단위 하나 (숨은 조건별 정답)</summary>
    [Serializable]
    public class AnswerBranch
    {
        public string Id;
        /// <summary>항상 수칙 조건 칸에 있어야 하는 숨은 조건 키워드 (직급 무관)</summary>
        public List<string> HiddenConditions = new List<string>();
        public List<string> Conditions = new List<string>();
        public List<string> Anomalies = new List<string>();
        public List<string> CorrectResponses = new List<string>();
        /// <summary>이 분기를 놓쳤을 때 인턴에게 보여줄 힌트</summary>
        public string Hint;
    }

    [Serializable]
    public class AnomalyVariant
    {
        public string Id;
        /// <summary>파견 시작 시 변형 선택 가중치</summary>
        public int Weight = 1;
        /// <summary>이 표식이 모두 있어야 발동 (조건화)</summary>
        public List<string> RequireMarksToAwaken = new List<string>();
        /// <summary>전조를 겪으면 해금 (예: 조명이 붉어짐 → 조건 키워드)</summary>
        public List<string> UnlockOnOmen = new List<string>();
        /// <summary>현상을 겪으면 해금 (예: 동전 소리 → 이상현상 키워드)</summary>
        public List<string> UnlockOnActive = new List<string>();
        /// <summary>전조·현상을 겪었을 때 저널 현장 기록 문장</summary>
        public string OmenRecord;
        public string ActiveRecord;
        public List<OutcomeRule> Outcomes = new List<OutcomeRule>();
        public List<AnswerBranch> Answers = new List<AnswerBranch>();

        /// <summary>결과 표 조회. 일치하는 줄이 없으면 "아무 일 없음"</summary>
        public OutcomeResult Evaluate(string action, PlayerStatus status)
        {
            var rule = Outcomes.FirstOrDefault(r => r.Matches(action, status));
            return rule != null ? rule.Result : OutcomeResult.Nothing();
        }
    }

    /// <summary>Judge가 채점하는 대상 = 이상현상 × 정답 분기</summary>
    public class AnswerTarget
    {
        public string Id;
        public string AnomalyId;
        public List<string> HiddenConditions = new List<string>();
        public List<string> Conditions = new List<string>();
        public List<string> Anomalies = new List<string>();
        public List<string> CorrectResponses = new List<string>();
        public string Hint;

        /// <summary>변형 없이 AnomalyDef 필드 그대로 (분기 1개)</summary>
        public static AnswerTarget FromAnomaly(AnomalyDef def) => new AnswerTarget
        {
            Id = def.Id,
            AnomalyId = def.Id,
            Conditions = def.Conditions,
            Anomalies = def.Anomalies,
            CorrectResponses = def.CorrectResponses,
        };

        /// <summary>선택된 변형의 정답 분기들. 분기가 없으면 AnomalyDef 필드로 대체</summary>
        public static List<AnswerTarget> FromVariant(AnomalyDef def, AnomalyVariant variant)
        {
            if (variant == null || variant.Answers.Count == 0) return new List<AnswerTarget> { FromAnomaly(def) };
            return variant.Answers.Select(b => new AnswerTarget
            {
                Id = def.Id + "/" + b.Id,
                AnomalyId = def.Id,
                HiddenConditions = b.HiddenConditions,
                Conditions = b.Conditions,
                Anomalies = b.Anomalies,
                CorrectResponses = b.CorrectResponses,
                Hint = b.Hint,
            }).ToList();
        }
    }
}
