// 저널 화면. Tab으로 열고 닫음 — 관공서 "현장 관측 일지" 문서 톤 (서담시)
// 여는 동안 이동·시점은 막히지만 현장 시간과 이상현상 판정은 계속 흐름
// 화면 구조는 C#으로, 모양은 Journal.uss로

using System.Collections.Generic;
using System.Linq;
using Napolitan.Core;
using Napolitan.Game.Dispatch;
using Napolitan.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Napolitan.Game.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class JournalView : MonoBehaviour
    {
        [SerializeField] DispatchManager dispatch;
        [SerializeField] StyleSheet styleSheet;
        [Tooltip("OS에 설치된 한글 글꼴 (없으면 엔진 기본 + OS 대체 글꼴)")]
        [SerializeField] string fontFamily = "Malgun Gothic";

        static readonly (Slot slot, string title)[] SlotGroups =
        {
            (Slot.Condition, "조건 — 언제·어디서"),
            (Slot.Anomaly, "이상현상 — 무엇이 일어나면"),
            (Slot.Response, "대응 — 어떻게 할 것"),
        };

        InputAction toggle;
        VisualElement root, overlay, entryList, keywordList;
        Label meta, hint;
        Journal journal;
        FirstPersonController player;
        bool dirty = true, checking;
        int seenEntries, seenKeywords;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            toggle = new InputAction("Journal", InputActionType.Button, "<Keyboard>/tab");
        }

        void OnEnable() => toggle.Enable();
        void OnDisable() => toggle.Disable();
        void OnDestroy() => toggle.Dispose();

        void Start()
        {
            journal = dispatch.Journal;
            player = dispatch.Player;
            journal.Changed += () => { if (!checking) dirty = true; };
            Build();
            SetOpen(false);
        }

        void Update()
        {
            if (toggle.WasPressedThisFrame())
            {
                if (IsOpen) SetOpen(false);
                else if (player.HasControl && !dispatch.Dying) SetOpen(true);
            }
            if (IsOpen && dispatch.Dying) SetOpen(false);

            if (IsOpen)
            {
                meta.text = $"현장 시각 {dispatch.FormatClock(dispatch.Elapsed)}";
                if (dirty) Refresh();
            }
            else UpdateHint();
        }

        void SetOpen(bool open)
        {
            IsOpen = open;
            overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            hint.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            if (open)
            {
                player.LockInput(this);
                FirstPersonController.SetCursorLocked(false); // 의심됨 체크용 커서
                Refresh();
                seenEntries = journal.Entries.Count;
                seenKeywords = KeywordCount();
            }
            else
            {
                player.UnlockInput(this);
                if (!player.InputLocked) FirstPersonController.SetCursorLocked(true);
                UpdateHint();
            }
        }

        // ── 화면 구성 ──────────────────────────────

        void Build()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            ApplyFont(root);

            hint = new Label { name = "journal-hint" };
            hint.AddToClassList("journal-hint");
            root.Add(hint);

            overlay = new VisualElement();
            overlay.AddToClassList("journal-root");
            root.Add(overlay);

            var paper = new VisualElement();
            paper.AddToClassList("journal-paper");
            overlay.Add(paper);

            var header = new VisualElement();
            header.AddToClassList("journal-header");
            var titleBox = new VisualElement();
            titleBox.Add(Text("도시이상현상관리국  ·  현장대응과", "journal-org"));
            titleBox.Add(Text("현장 관측 일지", "journal-title"));
            header.Add(titleBox);
            var metaBox = new VisualElement();
            metaBox.AddToClassList("journal-meta-box");
            metaBox.Add(Text($"파견 구역  {dispatch.SiteName}", "journal-meta"));
            metaBox.Add(Text($"작성자 직급  {RankName(dispatch.Rank)}", "journal-meta"));
            meta = Text("", "journal-meta");
            metaBox.Add(meta);
            header.Add(metaBox);
            paper.Add(header);

            paper.Add(Text("※ 본 일지는 현장에서 관측·청취·조사한 사실만 기록함. 열람 중에도 현장 시간은 흐름.", "journal-notice"));

            var body = new VisualElement();
            body.AddToClassList("journal-body");
            paper.Add(body);

            var left = new VisualElement();
            left.AddToClassList("journal-col");
            left.AddToClassList("journal-col--entries");
            left.Add(Text("관측 기록", "journal-section"));
            var entryScroll = new ScrollView(ScrollViewMode.Vertical);
            entryScroll.AddToClassList("journal-scroll");
            entryList = entryScroll.contentContainer;
            left.Add(entryScroll);
            body.Add(left);

            var right = new VisualElement();
            right.AddToClassList("journal-col");
            right.AddToClassList("journal-col--keywords");
            right.Add(Text("수칙 작성용 키워드", "journal-section"));
            var kwScroll = new ScrollView(ScrollViewMode.Vertical);
            kwScroll.AddToClassList("journal-scroll");
            keywordList = kwScroll.contentContainer;
            right.Add(kwScroll);
            body.Add(right);

            paper.Add(Text("[Tab] 닫기    ·    키워드 오른쪽 칸 = 의심됨 표시", "journal-footer"));
        }

        void Refresh()
        {
            dirty = false;

            entryList.Clear();
            if (journal.Entries.Count == 0) entryList.Add(Text("기록 없음.", "journal-empty"));
            foreach (var e in journal.Entries)
            {
                var row = new VisualElement();
                row.AddToClassList("entry");
                row.Add(Text(dispatch.FormatClock(e.Time), "entry-time"));
                var kind = Text(e.Kind == JournalEntryKind.Field ? "현장" : "자료", "entry-kind");
                if (e.Kind == JournalEntryKind.Field) kind.AddToClassList("entry-kind--field");
                row.Add(kind);
                row.Add(Text(e.DisplayText, "entry-text"));
                entryList.Add(row);
            }

            keywordList.Clear();
            foreach (var (slot, title) in SlotGroups)
            {
                var group = new VisualElement();
                group.AddToClassList("kw-group");
                group.Add(Text(title, "kw-group-title"));
                var words = journal.Keywords(slot);
                if (words.Count == 0) group.Add(Text("아직 없음.", "journal-empty"));
                foreach (var k in words) group.Add(KeywordRow(k));
                keywordList.Add(group);
            }
        }

        VisualElement KeywordRow(JournalKeyword k)
        {
            var row = new VisualElement();
            row.AddToClassList("kw-row");
            row.EnableInClassList("kw-row--suspected", k.Suspected);
            row.Add(Text(k.Keyword.Label, "kw-label"));

            var check = new Toggle { value = k.Suspected };
            check.AddToClassList("kw-check");
            var id = k.Keyword.Id;
            check.RegisterValueChangedCallback(evt =>
            {
                row.EnableInClassList("kw-row--suspected", evt.newValue);
                checking = true; // 체크 하나 때문에 목록 전체를 다시 그리지 않음 (스크롤 유지)
                journal.SetSuspected(id, evt.newValue);
                checking = false;
            });
            row.Add(check);
            return row;
        }

        void UpdateHint()
        {
            int newEntries = journal.Entries.Count - seenEntries;
            int newKeywords = KeywordCount() - seenKeywords;
            hint.text = newEntries + newKeywords > 0
                ? $"[Tab] 관측 일지 — 새 기록 {newEntries}  ·  새 키워드 {newKeywords}"
                : "[Tab] 관측 일지";
        }

        int KeywordCount() => SlotGroups.Sum(g => journal.Keywords(g.slot).Count);

        static Label Text(string text, string cls)
        {
            var label = new Label(text);
            label.AddToClassList(cls);
            return label;
        }

        static string RankName(Rank rank)
        {
            switch (rank)
            {
                case Rank.Intern: return "인턴";
                case Rank.AssistantManager: return "대리";
                case Rank.GeneralManager: return "부장";
                default: return rank.ToString();
            }
        }

        void ApplyFont(VisualElement element)
        {
            if (string.IsNullOrEmpty(fontFamily)) return;
            var font = FontAsset.CreateFontAsset(fontFamily, "Regular");
            if (font == null)
            {
                Debug.LogWarning($"[저널] 글꼴 '{fontFamily}'을 찾지 못해 기본 글꼴 사용");
                return;
            }
            element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(font));
        }
    }
}
