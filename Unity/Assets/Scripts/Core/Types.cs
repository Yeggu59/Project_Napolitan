// 수칙 시스템 데이터 구조
// Prototype/web/src/core/types.ts 이식. UnityEngine 의존 금지 (순수 C#, asmdef noEngineReferences)

using System;
using System.Collections.Generic;

namespace Napolitan.Core
{
    public enum Slot
    {
        Condition,
        Anomaly,
        Response,
    }

    [Serializable]
    public class Keyword
    {
        public string Id;
        public Slot Slot;
        public string Label;
        /// <summary>규칙성 축 (light, sound, gaze, movement, time, touch, smell ...)</summary>
        public string Category;
        /// <summary>null이면 공용 키워드</summary>
        public string SiteId;
    }

    [Serializable]
    public class Clue
    {
        public string Id;
        public string Text;
        public string Image;
        public List<string> RevealsKeywords = new List<string>();
    }

    /// <summary>이상현상 = 정답 규칙</summary>
    [Serializable]
    public class AnomalyDef
    {
        public string Id;
        public string SiteId;
        public string Name;
        public List<string> Conditions = new List<string>();
        public List<string> Anomalies = new List<string>();
        public List<string> CorrectResponses = new List<string>();
        public List<Clue> Clues = new List<Clue>();
        /// <summary>현장 동작용 변형. 비어 있으면 위 필드만으로 정산 채점</summary>
        public List<AnomalyVariant> Variants = new List<AnomalyVariant>();
    }

    /// <summary>표식 정의 — 위반이 남기는 숨은 상태</summary>
    [Serializable]
    public class MarkDef
    {
        public string Id;
        /// <summary>징후 설명 (예: "손등에 붉은 자국")</summary>
        public string Label;
    }

    /// <summary>구역 하나의 데이터</summary>
    [Serializable]
    public class SiteData
    {
        public string Id;
        public string Name;
        public List<Keyword> Keywords = new List<Keyword>();
        public List<AnomalyDef> Anomalies = new List<AnomalyDef>();
        /// <summary>이상현상과 무관한 일상 기록 — 오답 키워드를 해금하는 노이즈</summary>
        public List<Clue> NoiseClues = new List<Clue>();
        public List<MarkDef> Marks = new List<MarkDef>();
    }

    /// <summary>플레이어가 조립한 수칙</summary>
    [Serializable]
    public class Rule
    {
        public List<string> Condition = new List<string>();
        public string Anomaly;
        public string Response;

        public Rule() { }

        public Rule(IEnumerable<string> condition, string anomaly, string response)
        {
            Condition = new List<string>(condition);
            Anomaly = anomaly;
            Response = response;
        }
    }

    public enum Rank
    {
        Intern,
        AssistantManager,
        GeneralManager,
    }

    public enum KeywordVisibility
    {
        UnlockByClue,
        AllVisible,
    }

    public enum PastManualFeedback
    {
        MarkWrong,
        CountOnly,
        None,
    }

    /// <summary>숨은 조건이 있는 이상현상에서 숨은 조건을 빠뜨린 수칙 처리</summary>
    public enum HiddenConditionPolicy
    {
        /// <summary>표식 없는 분기의 정답으로 인정, 표식 분기는 미기재 (인턴·대리)</summary>
        AcceptAsBaseBranch,
        /// <summary>표식 없는 분기로도 인정하지 않고 오답 (부장)</summary>
        RejectAsWrong,
    }

    [Serializable]
    public struct ScoreTable
    {
        public int Correct;
        public int Partial;
        public int Wrong;

        public ScoreTable(int correct, int partial, int wrong)
        {
            Correct = correct;
            Partial = partial;
            Wrong = wrong;
        }
    }

    /// <summary>직급별 판정 파라미터</summary>
    [Serializable]
    public class RankConfig
    {
        public Rank Rank;
        public bool RequireAllConditions;
        public bool AllowMultipleCorrectResponses;
        public KeywordVisibility KeywordVisibility;
        public bool PartialCredit;
        public bool RevealWrongSlot;
        public PastManualFeedback PastManualFeedback;
        /// <summary>제출 한도 = 이상현상 수 + SubmissionLimitExtra</summary>
        public int SubmissionLimitExtra;
        public ScoreTable Score;
        public HiddenConditionPolicy HiddenConditionPolicy;
        /// <summary>정산에서 놓친 숨은 조건 분기의 힌트를 보여줄지</summary>
        public bool ShowHiddenConditionHint;
        /// <summary>파견당 목숨. 0 이하 = 무제한</summary>
        public int MaxLives;
    }

    public enum Verdict
    {
        Correct,
        Partial,
        Wrong,
        Duplicate,
    }

    public class RuleJudgment
    {
        public Rule Rule;
        public Verdict Verdict;
        /// <summary>매칭된 이상현상 (Wrong이면 null)</summary>
        public string AnomalyId;
        /// <summary>매칭된 채점 대상 (분기가 없으면 AnomalyId와 같음)</summary>
        public string TargetId;
        /// <summary>RevealWrongSlot인 직급의 Partial에서만 채워짐, 나머지는 null</summary>
        public List<Slot> WrongSlots;
        public int Score;

        public RuleJudgment Clone()
        {
            return (RuleJudgment)MemberwiseClone();
        }
    }

    public class ManualJudgment
    {
        public List<RuleJudgment> Rules = new List<RuleJudgment>();
        /// <summary>미기재 채점 대상 id (분기가 없으면 이상현상 id)</summary>
        public List<string> Missing = new List<string>();
        /// <summary>놓친 숨은 조건 분기의 힌트 (ShowHiddenConditionHint 직급만)</summary>
        public List<string> Hints = new List<string>();
        public int TotalScore;
    }

    /// <summary>제출 한도를 넘긴 매뉴얼을 판정하려 할 때</summary>
    public class SubmissionLimitExceededException : Exception
    {
        public int Count { get; }
        public int Limit { get; }

        public SubmissionLimitExceededException(int count, int limit)
            : base($"제출 한도 초과: {count} > {limit}")
        {
            Count = count;
            Limit = limit;
        }
    }
}
