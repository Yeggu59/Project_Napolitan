// 초기값. 플레이테스트 후 폴리싱
// Prototype/web/src/core/rankConfig.ts 이식

using System;

namespace Napolitan.Core
{
    public static class RankConfigs
    {
        public static readonly RankConfig Intern = new RankConfig
        {
            Rank = Rank.Intern,
            RequireAllConditions = false,
            AllowMultipleCorrectResponses = true,
            KeywordVisibility = KeywordVisibility.UnlockByClue,
            PartialCredit = true,
            RevealWrongSlot = true,
            PastManualFeedback = PastManualFeedback.MarkWrong,
            SubmissionLimitExtra = 2,
            Score = new ScoreTable(10, 3, -2),
        };

        public static readonly RankConfig AssistantManager = new RankConfig
        {
            Rank = Rank.AssistantManager,
            RequireAllConditions = true,
            AllowMultipleCorrectResponses = true,
            KeywordVisibility = KeywordVisibility.UnlockByClue,
            PartialCredit = true,
            RevealWrongSlot = false,
            PastManualFeedback = PastManualFeedback.CountOnly,
            SubmissionLimitExtra = 1,
            Score = new ScoreTable(10, 3, -4),
        };

        public static readonly RankConfig GeneralManager = new RankConfig
        {
            Rank = Rank.GeneralManager,
            RequireAllConditions = true,
            AllowMultipleCorrectResponses = false,
            KeywordVisibility = KeywordVisibility.AllVisible,
            PartialCredit = false,
            RevealWrongSlot = false,
            PastManualFeedback = PastManualFeedback.None,
            SubmissionLimitExtra = 0,
            Score = new ScoreTable(10, 0, -6),
        };

        public static readonly RankConfig[] All = { Intern, AssistantManager, GeneralManager };

        public static RankConfig Get(Rank rank)
        {
            switch (rank)
            {
                case Rank.Intern: return Intern;
                case Rank.AssistantManager: return AssistantManager;
                case Rank.GeneralManager: return GeneralManager;
                default: throw new ArgumentOutOfRangeException(nameof(rank), rank, null);
            }
        }
    }
}
