import type { Rank, RankConfig } from "./types";

// 초기값. 플레이테스트 후 폴리싱
export const RANK_CONFIGS: Record<Rank, RankConfig> = {
  Intern: {
    rank: "Intern",
    requireAllConditions: false,
    allowMultipleCorrectResponses: true,
    keywordVisibility: "unlockByClue",
    partialCredit: true,
    revealWrongSlot: true,
    pastManualFeedback: "markWrong",
    submissionLimitExtra: 2,
    score: { correct: 10, partial: 3, wrong: -2 },
  },
  AssistantManager: {
    rank: "AssistantManager",
    requireAllConditions: true,
    allowMultipleCorrectResponses: true,
    keywordVisibility: "unlockByClue",
    partialCredit: true,
    revealWrongSlot: false,
    pastManualFeedback: "countOnly",
    submissionLimitExtra: 1,
    score: { correct: 10, partial: 3, wrong: -4 },
  },
  GeneralManager: {
    rank: "GeneralManager",
    requireAllConditions: true,
    allowMultipleCorrectResponses: false,
    keywordVisibility: "allVisible",
    partialCredit: false,
    revealWrongSlot: false,
    pastManualFeedback: "none",
    submissionLimitExtra: 0,
    score: { correct: 10, partial: 0, wrong: -6 },
  },
};
