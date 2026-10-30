// 수칙 시스템 데이터 구조
// DOM 의존 금지 (C# 이식 대상)

export type Slot = "condition" | "anomaly" | "response";

export interface Keyword {
  id: string;
  slot: Slot;
  label: string;
  /** 규칙성 축 (light, sound, gaze, movement, time, touch ...) */
  category: string;
  /** 없으면 공용 키워드 */
  siteId?: string;
}

export interface Clue {
  id: string;
  text: string;
  image?: string;
  revealsKeywords: string[];
}

/** 이상현상 = 정답 규칙 */
export interface AnomalyDef {
  id: string;
  siteId: string;
  name: string;
  conditions: string[];
  anomalies: string[];
  correctResponses: string[];
  clues: Clue[];
}

/** 구역 하나의 데이터 (src/data/*.json) */
export interface SiteData {
  id: string;
  name: string;
  keywords: Keyword[];
  anomalies: AnomalyDef[];
  /** 이상현상과 무관한 일상 기록 — 오답 키워드를 해금하는 노이즈 */
  noiseClues: Clue[];
}

/** 플레이어가 조립한 수칙 */
export interface Rule {
  condition: string[];
  anomaly: string;
  response: string;
}

export type Manual = Rule[];

export type Rank = "Intern" | "AssistantManager" | "GeneralManager";

/** 직급별 판정 파라미터 */
export interface RankConfig {
  rank: Rank;
  requireAllConditions: boolean;
  allowMultipleCorrectResponses: boolean;
  keywordVisibility: "unlockByClue" | "allVisible";
  partialCredit: boolean;
  revealWrongSlot: boolean;
  pastManualFeedback: "markWrong" | "countOnly" | "none";
  /** 제출 한도 = 이상현상 수 + submissionLimitExtra */
  submissionLimitExtra: number;
  score: { correct: number; partial: number; wrong: number };
}

export type Verdict = "correct" | "partial" | "wrong" | "duplicate";

export interface RuleJudgment {
  rule: Rule;
  verdict: Verdict;
  /** 매칭된 이상현상 (wrong이면 없음) */
  anomalyId?: string;
  /** revealWrongSlot인 직급의 partial에서만 채워짐 */
  wrongSlots?: Slot[];
  score: number;
}

export interface ManualJudgment {
  rules: RuleJudgment[];
  /** 미기재 이상현상 id */
  missing: string[];
  totalScore: number;
}
