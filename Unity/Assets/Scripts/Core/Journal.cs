// 저널: 겪은 것(현장 기록)과 조사한 단서가 겪은 순서대로 쌓이고,
// 해금된 키워드를 칸별로 보여주며, 플레이어가 키워드에 "의심됨" 체크로 정리한다.
// 순수 로직 — DispatchSession 이벤트만 구독. 시각은 호출 쪽 시계(파견 시작 후 초)를 받는다.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Napolitan.Core
{
    public enum JournalEntryKind
    {
        /// <summary>조사로 얻은 기록물 (CCTV 로그, 진술서 등)</summary>
        Clue,
        /// <summary>현장에서 직접 겪은 것 (전조·현상·행동 결과)</summary>
        Field,
    }

    public class JournalEntry
    {
        public int Index;
        public JournalEntryKind Kind;
        /// <summary>clue id 또는 이상현상 id</summary>
        public string SourceId;
        /// <summary>파견 시작 후 초</summary>
        public float Time;
        /// <summary>원문. 정산·검증은 이걸 기준으로</summary>
        public string Text;
        /// <summary>화면에 보이는 문장. 지금은 원문과 같음 — 본편의 기록 변조(인지 왜곡)는 이 값만 바꾸면 되게 분리</summary>
        public string DisplayText;
    }

    public class JournalKeyword
    {
        public Keyword Keyword;
        public float Time;
        public bool Suspected;
    }

    public class Journal
    {
        readonly Func<float> clock;
        readonly List<JournalEntry> entries = new List<JournalEntry>();
        readonly List<JournalKeyword> keywords = new List<JournalKeyword>();
        readonly Dictionary<string, Keyword> keywordDefs;

        public IReadOnlyList<JournalEntry> Entries => entries;

        /// <summary>항목이 추가되거나 체크가 바뀜</summary>
        public event Action Changed;

        public Journal(DispatchSession session, Func<float> clock)
        {
            this.clock = clock;
            keywordDefs = session.Site.Keywords.ToDictionary(k => k.Id);

            // 저널이 늦게 만들어져도 이미 겪은 것은 시각 0으로 채움
            foreach (var id in session.UnlockedKeywords) AddKeyword(id, 0f);

            session.ClueAdded += clue => AddEntry(JournalEntryKind.Clue, clue.Id, clue.Text);
            session.KeywordUnlocked += id => AddKeyword(id, clock());
            session.OutcomeApplied += (anomalyId, result) => AddEntry(JournalEntryKind.Field, anomalyId, result.Record);
            foreach (var runtime in session.Anomalies.Values)
            {
                var r = runtime;
                r.StateChanged += state =>
                {
                    if (state == AnomalyState.Omen) AddEntry(JournalEntryKind.Field, r.Def.Id, r.Variant.OmenRecord);
                    else if (state == AnomalyState.Active) AddEntry(JournalEntryKind.Field, r.Def.Id, r.Variant.ActiveRecord);
                };
            }
        }

        /// <summary>칸별 해금 키워드 (해금 순서)</summary>
        public IReadOnlyList<JournalKeyword> Keywords(Slot slot) => keywords.Where(k => k.Keyword.Slot == slot).ToList();

        public IEnumerable<string> SuspectedKeywordIds => keywords.Where(k => k.Suspected).Select(k => k.Keyword.Id);

        public bool IsSuspected(string keywordId) => keywords.Any(k => k.Keyword.Id == keywordId && k.Suspected);

        /// <summary>"의심됨" 체크. 해금 안 된 키워드면 무시</summary>
        public void SetSuspected(string keywordId, bool suspected)
        {
            var k = keywords.FirstOrDefault(x => x.Keyword.Id == keywordId);
            if (k == null || k.Suspected == suspected) return;
            k.Suspected = suspected;
            Changed?.Invoke();
        }

        void AddEntry(JournalEntryKind kind, string sourceId, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // 같은 전조를 재발동마다 다시 겪으면 그때마다 기록 (겪은 순서 그대로)
            entries.Add(new JournalEntry
            {
                Index = entries.Count,
                Kind = kind,
                SourceId = sourceId,
                Time = clock(),
                Text = text,
                DisplayText = text,
            });
            Changed?.Invoke();
        }

        void AddKeyword(string id, float time)
        {
            if (!keywordDefs.TryGetValue(id, out var def)) return;
            if (keywords.Any(k => k.Keyword.Id == id)) return;
            keywords.Add(new JournalKeyword { Keyword = def, Time = time });
            Changed?.Invoke();
        }
    }
}
