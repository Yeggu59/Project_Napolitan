// 수칙 판정 로직 (정산 문장 판정)
// 채점 단위는 AnswerTarget(이상현상 × 정답 분기). 분기 없는 이상현상은 분기 1개로 취급 → T1~T13 동작 유지

using System.Collections.Generic;
using System.Linq;

namespace Napolitan.Core
{
    public static class Judge
    {
        /// <summary>부분정답으로 인정하는 최소 일치 칸 수 (초기값)</summary>
        public const int PartialMinMatchedSlots = 2;

        const int SlotCount = 3;

        /// <summary>직급에 맞는 이상현상 풀. 부장은 정답 대응 1개짜리만 (R10)</summary>
        public static List<AnomalyDef> AnomalyPoolForRank(IEnumerable<AnomalyDef> anomalies, RankConfig config)
        {
            if (config.AllowMultipleCorrectResponses) return anomalies.ToList();
            return anomalies.Where(a => a.CorrectResponses.Count == 1).ToList();
        }

        /// <summary>채점 대상 풀. 부장은 분기 중 하나라도 정답 대응이 여러 개인 이상현상을 통째로 제외 (R10)</summary>
        public static List<AnswerTarget> TargetPoolForRank(IEnumerable<AnswerTarget> targets, RankConfig config)
        {
            var list = targets.ToList();
            if (config.AllowMultipleCorrectResponses) return list;
            var excluded = new HashSet<string>(list.Where(t => t.CorrectResponses.Count != 1).Select(t => t.AnomalyId));
            return list.Where(t => !excluded.Contains(t.AnomalyId)).ToList();
        }

        public static int SubmissionLimit(int anomalyCount, RankConfig config)
        {
            return anomalyCount + config.SubmissionLimitExtra;
        }

        /// <summary>UI용: 이 개수로 제출 가능한지 (예외 없이 확인). 제출 버튼 활성화 판단에 사용</summary>
        public static bool CanSubmit(int ruleCount, IEnumerable<AnomalyDef> anomalies, RankConfig config)
        {
            return ruleCount <= SubmissionLimit(AnomalyPoolForRank(anomalies, config).Count, config);
        }

        /// <summary>UI용 (변형 정산): 제출 한도는 분기 수가 아니라 이상현상 수 기준</summary>
        public static bool CanSubmit(int ruleCount, IEnumerable<AnswerTarget> targets, RankConfig config)
        {
            return ruleCount <= SubmissionLimit(CountAnomalies(TargetPoolForRank(targets, config)), config);
        }

        static int CountAnomalies(IEnumerable<AnswerTarget> targets) => targets.Select(t => t.AnomalyId).Distinct().Count();

        static bool ConditionMatches(Rule rule, AnswerTarget target, RankConfig config)
        {
            if (rule.Condition == null || rule.Condition.Count == 0) return false;
            // 숨은 조건은 직급과 무관하게 항상 있어야 함
            if (!target.HiddenConditions.All(h => rule.Condition.Contains(h))) return false;
            var visible = rule.Condition.Where(c => !target.HiddenConditions.Contains(c)).ToList();
            if (visible.Count == 0) return false;
            // 정답에 없는 조건이 하나라도 섞이면 틀림
            if (!visible.All(c => target.Conditions.Contains(c))) return false;
            if (!config.RequireAllConditions) return true;
            return target.Conditions.All(c => visible.Contains(c));
        }

        static bool ResponseMatches(Rule rule, AnswerTarget target, RankConfig config)
        {
            if (config.AllowMultipleCorrectResponses)
                return target.CorrectResponses.Contains(rule.Response);
            return target.CorrectResponses.Count > 0 && target.CorrectResponses[0] == rule.Response;
        }

        static List<Slot> WrongSlotsAgainst(Rule rule, AnswerTarget target, RankConfig config)
        {
            var wrong = new List<Slot>();
            if (!ConditionMatches(rule, target, config)) wrong.Add(Slot.Condition);
            if (!target.Anomalies.Contains(rule.Anomaly)) wrong.Add(Slot.Anomaly);
            if (!ResponseMatches(rule, target, config)) wrong.Add(Slot.Response);
            return wrong;
        }

        /// <summary>수칙 하나를 가장 잘 맞는 이상현상 기준으로 판정 (중복 처리 전). 동점이면 앞의 것</summary>
        public static RuleJudgment JudgeRule(Rule rule, IReadOnlyList<AnomalyDef> pool, RankConfig config)
        {
            return JudgeRule(rule, pool.Select(AnswerTarget.FromAnomaly).ToList(), config);
        }

        /// <summary>수칙 하나를 가장 잘 맞는 채점 대상 기준으로 판정 (중복 처리 전). 동점이면 앞의 것</summary>
        public static RuleJudgment JudgeRule(Rule rule, IReadOnlyList<AnswerTarget> pool, RankConfig config)
        {
            AnswerTarget best = null;
            List<Slot> bestWrong = null;
            foreach (var target in pool)
            {
                var wrong = WrongSlotsAgainst(rule, target, config);
                if (bestWrong == null || wrong.Count < bestWrong.Count)
                {
                    best = target;
                    bestWrong = wrong;
                }
            }

            if (best != null && bestWrong.Count == 0)
            {
                return new RuleJudgment
                {
                    Rule = rule,
                    Verdict = Verdict.Correct,
                    AnomalyId = best.AnomalyId,
                    TargetId = best.Id,
                    Score = config.Score.Correct,
                };
            }

            int matched = best != null ? SlotCount - bestWrong.Count : 0;
            if (best != null && config.PartialCredit && matched >= PartialMinMatchedSlots)
            {
                return new RuleJudgment
                {
                    Rule = rule,
                    Verdict = Verdict.Partial,
                    AnomalyId = best.AnomalyId,
                    TargetId = best.Id,
                    WrongSlots = config.RevealWrongSlot ? bestWrong : null,
                    Score = config.Score.Partial,
                };
            }

            return Wrong(rule, config);
        }

        static RuleJudgment Wrong(Rule rule, RankConfig config) =>
            new RuleJudgment { Rule = rule, Verdict = Verdict.Wrong, Score = config.Score.Wrong };

        /// <summary>
        /// 매뉴얼 전체 판정 (변형 없는 이상현상 목록). 정산 시점에만 호출.
        /// </summary>
        /// <exception cref="SubmissionLimitExceededException">제출 한도 초과</exception>
        public static ManualJudgment JudgeManual(IReadOnlyList<Rule> manual, IEnumerable<AnomalyDef> anomalies, RankConfig config)
        {
            var pool = AnomalyPoolForRank(anomalies, config);
            return JudgeTargets(manual, pool.Select(AnswerTarget.FromAnomaly).ToList(), config);
        }

        /// <summary>
        /// 매뉴얼 전체 판정 (채점 대상 목록 — DispatchSession.AnswerTargets()).
        /// 같은 대상에 수칙이 여러 개면 가장 좋은 것 하나만 점수를 받고,
        /// 나머지 비오답은 Duplicate(0점), 오답은 각각 감점.
        /// </summary>
        /// <exception cref="SubmissionLimitExceededException">제출 한도 초과 (이상현상 수 기준)</exception>
        public static ManualJudgment JudgeTargets(IReadOnlyList<Rule> manual, IEnumerable<AnswerTarget> targets, RankConfig config)
        {
            var pool = TargetPoolForRank(targets, config);
            int limit = SubmissionLimit(CountAnomalies(pool), config);
            if (manual.Count > limit) throw new SubmissionLimitExceededException(manual.Count, limit);

            var rules = manual.Select(r => JudgeRule(r, pool, config)).ToList();

            // 부장: 숨은 조건 분기가 있는 이상현상에서 숨은 조건 없이 맞춘 수칙은 오답 (Q-H3b)
            if (config.HiddenConditionPolicy == HiddenConditionPolicy.RejectAsWrong)
            {
                var hasHidden = new HashSet<string>(pool.Where(t => t.HiddenConditions.Count > 0).Select(t => t.AnomalyId));
                var targetById = pool.ToDictionary(t => t.Id);
                for (int i = 0; i < rules.Count; i++)
                {
                    var r = rules[i];
                    if (r.TargetId == null || !hasHidden.Contains(r.AnomalyId)) continue;
                    if (targetById[r.TargetId].HiddenConditions.Count == 0) rules[i] = Wrong(r.Rule, config);
                }
            }

            // 대상별 최고 점수 수칙 (동점이면 먼저 제출한 것)
            var bestIndexByTarget = new Dictionary<string, int>();
            for (int i = 0; i < rules.Count; i++)
            {
                var id = rules[i].TargetId;
                if (id == null) continue;
                if (!bestIndexByTarget.TryGetValue(id, out int prev) || rules[i].Score > rules[prev].Score)
                    bestIndexByTarget[id] = i;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                var id = rules[i].TargetId;
                if (id != null && bestIndexByTarget[id] != i)
                {
                    var dup = rules[i].Clone();
                    dup.Verdict = Verdict.Duplicate;
                    dup.WrongSlots = null;
                    dup.Score = 0;
                    rules[i] = dup;
                }
            }

            var missing = pool.Where(t => !bestIndexByTarget.ContainsKey(t.Id)).ToList();
            var hints = config.ShowHiddenConditionHint
                ? missing.Where(t => t.HiddenConditions.Count > 0 && !string.IsNullOrEmpty(t.Hint)).Select(t => t.Hint).ToList()
                : new List<string>();

            return new ManualJudgment
            {
                Rules = rules,
                Missing = missing.Select(t => t.Id).ToList(),
                Hints = hints,
                TotalScore = rules.Sum(r => r.Score),
            };
        }
    }
}
