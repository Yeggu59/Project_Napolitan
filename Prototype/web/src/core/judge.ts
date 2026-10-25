import type {
  AnomalyDef,
  Manual,
  ManualJudgment,
  RankConfig,
  Rule,
  RuleJudgment,
  Slot,
} from "./types";

/** 부분정답으로 인정하는 최소 일치 칸 수 (초기값) */
export const PARTIAL_MIN_MATCHED_SLOTS = 2;

/** 직급에 맞는 이상현상 풀. 부장은 정답 대응 1개짜리만 (R10) */
export function anomalyPoolForRank(
  anomalies: AnomalyDef[],
  config: RankConfig,
): AnomalyDef[] {
  if (config.allowMultipleCorrectResponses) return anomalies;
  return anomalies.filter((a) => a.correctResponses.length === 1);
}

export function submissionLimit(anomalyCount: number, config: RankConfig): number {
  return anomalyCount + config.submissionLimitExtra;
}

function conditionMatches(rule: Rule, anomaly: AnomalyDef, config: RankConfig): boolean {
  if (rule.condition.length === 0) return false;
  // 정답에 없는 조건이 하나라도 섞이면 틀림
  if (!rule.condition.every((c) => anomaly.conditions.includes(c))) return false;
  if (!config.requireAllConditions) return true;
  return anomaly.conditions.every((c) => rule.condition.includes(c));
}

function responseMatches(rule: Rule, anomaly: AnomalyDef, config: RankConfig): boolean {
  if (config.allowMultipleCorrectResponses) {
    return anomaly.correctResponses.includes(rule.response);
  }
  return anomaly.correctResponses[0] === rule.response;
}

function wrongSlotsAgainst(rule: Rule, anomaly: AnomalyDef, config: RankConfig): Slot[] {
  const wrong: Slot[] = [];
  if (!conditionMatches(rule, anomaly, config)) wrong.push("condition");
  if (!anomaly.anomalies.includes(rule.anomaly)) wrong.push("anomaly");
  if (!responseMatches(rule, anomaly, config)) wrong.push("response");
  return wrong;
}

/** 수칙 하나를 가장 잘 맞는 이상현상 기준으로 판정 (중복 처리 전) */
export function judgeRule(
  rule: Rule,
  pool: AnomalyDef[],
  config: RankConfig,
): RuleJudgment {
  let best: { anomaly: AnomalyDef; wrong: Slot[] } | undefined;
  for (const anomaly of pool) {
    const wrong = wrongSlotsAgainst(rule, anomaly, config);
    if (!best || wrong.length < best.wrong.length) best = { anomaly, wrong };
  }

  if (best && best.wrong.length === 0) {
    return { rule, verdict: "correct", anomalyId: best.anomaly.id, score: config.score.correct };
  }

  const matched = best ? 3 - best.wrong.length : 0;
  if (best && config.partialCredit && matched >= PARTIAL_MIN_MATCHED_SLOTS) {
    return {
      rule,
      verdict: "partial",
      anomalyId: best.anomaly.id,
      wrongSlots: config.revealWrongSlot ? best.wrong : undefined,
      score: config.score.partial,
    };
  }

  return { rule, verdict: "wrong", score: config.score.wrong };
}

/**
 * 매뉴얼 전체 판정 (정산 시점에만 호출 — R9).
 * 같은 이상현상에 수칙이 여러 개면 가장 좋은 것 하나만 점수를 받고,
 * 나머지 비오답은 duplicate(0점), 오답은 각각 감점.
 */
export function judgeManual(
  manual: Manual,
  anomalies: AnomalyDef[],
  config: RankConfig,
): ManualJudgment {
  const pool = anomalyPoolForRank(anomalies, config);
  const limit = submissionLimit(pool.length, config);
  if (manual.length > limit) {
    throw new Error(`제출 한도 초과: ${manual.length} > ${limit}`);
  }

  const rules = manual.map((rule) => judgeRule(rule, pool, config));

  const bestIndexByAnomaly = new Map<string, number>();
  rules.forEach((r, i) => {
    if (!r.anomalyId) return;
    const prev = bestIndexByAnomaly.get(r.anomalyId);
    if (prev === undefined || r.score > rules[prev].score) {
      bestIndexByAnomaly.set(r.anomalyId, i);
    }
  });
  rules.forEach((r, i) => {
    if (r.anomalyId && bestIndexByAnomaly.get(r.anomalyId) !== i) {
      rules[i] = { ...r, verdict: "duplicate", wrongSlots: undefined, score: 0 };
    }
  });

  const missing = pool.filter((a) => !bestIndexByAnomaly.has(a.id)).map((a) => a.id);
  const totalScore = rules.reduce((sum, r) => sum + r.score, 0);
  return { rules, missing, totalScore };
}
