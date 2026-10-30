// 현장 런타임: 플레이어 상태 · 이상현상 상태머신 · 파견 세션
// 순수 로직 — 랜덤은 DispatchSession의 변형 선택에서만 사용

using System;
using System.Collections.Generic;
using System.Linq;

namespace Napolitan.Core
{
    public class PlayerStatus
    {
        readonly HashSet<string> marks = new HashSet<string>();
        readonly HashSet<string> impairments = new HashSet<string>();

        /// <summary>남은 목숨. MaxLives가 무제한이면 의미 없음</summary>
        public int Lives;
        public bool UnlimitedLives;
        public int Deaths { get; private set; }

        public IReadOnlyCollection<string> Marks => marks;
        public IReadOnlyCollection<string> Impairments => impairments;

        public bool HasMark(string id) => marks.Contains(id);
        public bool HasImpairment(string id) => impairments.Contains(id);
        public void AddMark(string id) => marks.Add(id);
        public void AddImpairment(string id) => impairments.Add(id);

        public bool OutOfLives => !UnlimitedLives && Lives <= 0;

        /// <summary>즉사 처리: 목숨 차감, 표식 제거, 결손 유지</summary>
        public void Die()
        {
            Deaths++;
            if (!UnlimitedLives) Lives--;
            marks.Clear();
        }
    }

    /// <summary>이상현상 하나의 상태머신: Dormant → Omen → Active → Resolved / Violated</summary>
    public class AnomalyRuntime
    {
        public AnomalyDef Def { get; }
        public AnomalyVariant Variant { get; }
        public AnomalyState State { get; private set; } = AnomalyState.Dormant;

        public event Action<AnomalyState> StateChanged;

        public AnomalyRuntime(AnomalyDef def, AnomalyVariant variant)
        {
            Def = def;
            Variant = variant;
        }

        /// <summary>플레이어가 구역에 들어옴: Dormant이고 발동 조건(표식)을 만족하면 Omen</summary>
        public bool TryAwaken(PlayerStatus status)
        {
            if (State != AnomalyState.Dormant) return false;
            if (!Variant.RequireMarksToAwaken.All(status.HasMark)) return false;
            SetState(AnomalyState.Omen);
            return true;
        }

        /// <summary>전조 → 현상</summary>
        public bool Escalate()
        {
            if (State != AnomalyState.Omen) return false;
            SetState(AnomalyState.Active);
            return true;
        }

        /// <summary>현상 중 플레이어 행동 판정. Active가 아니면 null</summary>
        public OutcomeResult Respond(string action, PlayerStatus status)
        {
            if (State != AnomalyState.Active) return null;
            var result = Variant.Evaluate(action, status);
            switch (result.Type)
            {
                case OutcomeType.Nothing:
                    break;
                case OutcomeType.Resolve:
                    SetState(AnomalyState.Resolved);
                    break;
                default:
                    SetState(AnomalyState.Violated);
                    break;
            }
            return result;
        }

        /// <summary>플레이어가 구역을 벗어남: 끝났거나 전조 단계면 Dormant로 (재발동 대기). Active는 유지</summary>
        public void Rearm()
        {
            if (State == AnomalyState.Active || State == AnomalyState.Dormant) return;
            SetState(AnomalyState.Dormant);
        }

        void SetState(AnomalyState next)
        {
            State = next;
            StateChanged?.Invoke(next);
        }
    }

    /// <summary>파견 1회의 상태. 게임 쪽은 이 객체에 이벤트를 넘기고 결과만 연출한다</summary>
    public class DispatchSession
    {
        readonly Dictionary<string, AnomalyRuntime> anomalies = new Dictionary<string, AnomalyRuntime>();
        readonly HashSet<string> unlockedKeywords = new HashSet<string>();
        readonly HashSet<string> foundClues = new HashSet<string>();
        readonly Dictionary<string, Clue> cluesById;

        public SiteData Site { get; }
        public RankConfig Config { get; }
        public PlayerStatus Player { get; }

        public IReadOnlyDictionary<string, AnomalyRuntime> Anomalies => anomalies;
        public IReadOnlyCollection<string> UnlockedKeywords => unlockedKeywords;
        public IReadOnlyCollection<string> FoundClues => foundClues;

        public event Action<string> KeywordUnlocked;
        public event Action<string, OutcomeResult> OutcomeApplied;

        /// <param name="anomalyIds">이번 파견에 등장하는 이상현상 (변형이 있어야 함)</param>
        /// <param name="seed">변형 선택 시드 — 같은 시드면 같은 변형</param>
        public DispatchSession(SiteData site, RankConfig config, IEnumerable<string> anomalyIds, int seed)
        {
            Site = site;
            Config = config;
            Player = new PlayerStatus { Lives = config.MaxLives, UnlimitedLives = config.MaxLives <= 0 };
            cluesById = site.Anomalies.SelectMany(a => a.Clues).Concat(site.NoiseClues).ToDictionary(c => c.Id);

            var rng = new Random(seed);
            foreach (var id in anomalyIds)
            {
                var def = site.Anomalies.First(a => a.Id == id);
                if (def.Variants.Count == 0) throw new ArgumentException($"{id}: 변형이 없는 이상현상은 현장에 배치할 수 없음");
                var runtime = new AnomalyRuntime(def, PickVariant(def.Variants, rng));
                runtime.StateChanged += state => OnStateChanged(runtime, state);
                anomalies.Add(id, runtime);
            }
        }

        static AnomalyVariant PickVariant(List<AnomalyVariant> variants, Random rng)
        {
            int total = variants.Sum(v => Math.Max(v.Weight, 0));
            if (total <= 0) return variants[0];
            int roll = rng.Next(total);
            foreach (var v in variants)
            {
                roll -= Math.Max(v.Weight, 0);
                if (roll < 0) return v;
            }
            return variants[variants.Count - 1];
        }

        /// <summary>조사로 얻은 단서 기록 + 키워드 해금</summary>
        public void AddClue(string clueId)
        {
            if (!foundClues.Add(clueId)) return;
            if (cluesById.TryGetValue(clueId, out var clue)) Unlock(clue.RevealsKeywords);
        }

        /// <summary>현장 행동 판정 + 결과를 플레이어 상태에 반영. 이상현상이 Active가 아니면 null</summary>
        public OutcomeResult Respond(string anomalyId, string action)
        {
            var runtime = anomalies[anomalyId];
            var result = runtime.Respond(action, Player);
            if (result == null) return null;

            switch (result.Type)
            {
                case OutcomeType.ImmediateHarm:
                    Player.AddImpairment(result.Impairment);
                    break;
                case OutcomeType.ApplyMark:
                    Player.AddMark(result.Mark);
                    break;
                case OutcomeType.Fatal:
                    Player.Die();
                    break;
            }
            Unlock(result.UnlockKeywords);
            OutcomeApplied?.Invoke(anomalyId, result);
            return result;
        }

        /// <summary>정산 채점 대상: 이번 파견에서 선택된 변형의 정답 분기들</summary>
        public List<AnswerTarget> AnswerTargets() =>
            anomalies.Values.SelectMany(r => AnswerTarget.FromVariant(r.Def, r.Variant)).ToList();

        /// <summary>정산 (문장 판정)</summary>
        public ManualJudgment Settle(IReadOnlyList<Rule> manual) => Judge.JudgeTargets(manual, AnswerTargets(), Config);

        void OnStateChanged(AnomalyRuntime runtime, AnomalyState state)
        {
            if (state == AnomalyState.Omen) Unlock(runtime.Variant.UnlockOnOmen);
            else if (state == AnomalyState.Active) Unlock(runtime.Variant.UnlockOnActive);
        }

        void Unlock(IEnumerable<string> keywordIds)
        {
            foreach (var id in keywordIds)
                if (unlockedKeywords.Add(id)) KeywordUnlocked?.Invoke(id);
        }
    }
}
