// 수칙 판정 로직. Prototype/web/src/core/judge.ts 이식
// 동작은 TS 버전과 동일해야 함 (테스트 T1~T13 공유)

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

        public static int SubmissionLimit(int anomalyCount, RankConfig config)
        {
            return anomalyCount + config.SubmissionLimitExtra;
        }

        /// <summary>UI용: 이 개수로 제출 가능한지 (예외 없이 확인). 제출 버튼 활성화 판단에 사용</summary>
        public static bool CanSubmit(int ruleCount, IEnumerable<AnomalyDef> anomalies, RankConfig config)
        {
            return ruleCount <= SubmissionLimit(AnomalyPoolForRank(anomalies, config).Count, config);
        }

        static bool ConditionMatches(Rule rule, AnomalyDef anomaly, RankConfig config)
        {
            if (rule.Condition == null || rule.Condition.Count == 0) return false;
            // 정답에 없는 조건이 하나라도 섞이면 틀림
            if (!rule.Condition.All(c => anomaly.Conditions.Contains(c))) return false;
            if (!config.RequireAllConditions) return true;
            return anomaly.Conditions.All(c => rule.Condition.Contains(c));
        }

        static bool ResponseMatches(Rule rule, AnomalyDef anomaly, RankConfig config)
        {
            if (config.AllowMultipleCorrectResponses)
                return anomaly.CorrectResponses.Contains(rule.Response);
            return anomaly.CorrectResponses.Count > 0 && anomaly.CorrectResponses[0] == rule.Response;
        }

        static List<Slot> WrongSlotsAgainst(Rule rule, AnomalyDef anomaly, RankConfig config)
        {
            var wrong = new List<Slot>();
            if (!ConditionMatches(rule, anomaly, config)) wrong.Add(Slot.Condition);
            if (!anomaly.Anomalies.Contains(rule.Anomaly)) wrong.Add(Slot.Anomaly);
            if (!ResponseMatches(rule, anomaly, config)) wrong.Add(Slot.Response);
            return wrong;
        }

        /// <summary>수칙 하나를 가장 잘 맞는 이상현상 기준으로 판정 (중복 처리 전). 동점이면 앞의 것</summary>
        public static RuleJudgment JudgeRule(Rule rule, IReadOnlyList<AnomalyDef> pool, RankConfig config)
        {
            AnomalyDef bestAnomaly = null;
            List<Slot> bestWrong = null;
            foreach (var anomaly in pool)
            {
                var wrong = WrongSlotsAgainst(rule, anomaly, config);
                if (bestWrong == null || wrong.Count < bestWrong.Count)
                {
                    bestAnomaly = anomaly;
                    bestWrong = wrong;
                }
            }

            if (bestAnomaly != null && bestWrong.Count == 0)
            {
                return new RuleJudgment
                {
                    Rule = rule,
                    Verdict = Verdict.Correct,
                    AnomalyId = bestAnomaly.Id,
                    Score = config.Score.Correct,
                };
            }

            int matched = bestAnomaly != null ? SlotCount - bestWrong.Count : 0;
            if (bestAnomaly != null && config.PartialCredit && matched >= PartialMinMatchedSlots)
            {
                return new RuleJudgment
                {
                    Rule = rule,
                    Verdict = Verdict.Partial,
                    AnomalyId = bestAnomaly.Id,
                    WrongSlots = config.RevealWrongSlot ? bestWrong : null,
                    Score = config.Score.Partial,
                };
            }

            return new RuleJudgment { Rule = rule, Verdict = Verdict.Wrong, Score = config.Score.Wrong };
        }

        /// <summary>
        /// 매뉴얼 전체 판정 (정산 시점에만 호출 — R9).
        /// 같은 이상현상에 수칙이 여러 개면 가장 좋은 것 하나만 점수를 받고,
        /// 나머지 비오답은 Duplicate(0점), 오답은 각각 감점.
        /// </summary>
        /// <exception cref="SubmissionLimitExceededException">제출 한도 초과</exception>
        public static ManualJudgment JudgeManual(IReadOnlyList<Rule> manual, IEnumerable<AnomalyDef> anomalies, RankConfig config)
        {
            var pool = AnomalyPoolForRank(anomalies, config);
            int limit = SubmissionLimit(pool.Count, config);
            if (manual.Count > limit) throw new SubmissionLimitExceededException(manual.Count, limit);

            var rules = manual.Select(r => JudgeRule(r, pool, config)).ToList();

            // 이상현상별 최고 점수 수칙 (동점이면 먼저 제출한 것)
            var bestIndexByAnomaly = new Dictionary<string, int>();
            for (int i = 0; i < rules.Count; i++)
            {
                var id = rules[i].AnomalyId;
                if (id == null) continue;
                if (!bestIndexByAnomaly.TryGetValue(id, out int prev) || rules[i].Score > rules[prev].Score)
                    bestIndexByAnomaly[id] = i;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                var id = rules[i].AnomalyId;
                if (id != null && bestIndexByAnomaly[id] != i)
                {
                    var dup = rules[i].Clone();
                    dup.Verdict = Verdict.Duplicate;
                    dup.WrongSlots = null;
                    dup.Score = 0;
                    rules[i] = dup;
                }
            }

            return new ManualJudgment
            {
                Rules = rules,
                Missing = pool.Where(a => !bestIndexByAnomaly.ContainsKey(a.Id)).Select(a => a.Id).ToList(),
                TotalScore = rules.Sum(r => r.Score),
            };
        }
    }
}
