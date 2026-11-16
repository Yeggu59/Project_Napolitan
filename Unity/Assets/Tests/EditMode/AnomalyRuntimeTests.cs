// 현장 동작 테스트 — 붉은 자판기 슬라이스 데이터(Assets/Data/subway_transfer.json) 기준
// 케이스 id: H3-xx

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Napolitan.Core;
using Napolitan.Game.Data;
using UnityEngine;

namespace Napolitan.Core.Tests
{
    public class AnomalyRuntimeTests
    {
        const string Vending = "anom.red_vending";
        const string Mark = "mark.red_hand";
        const string VisionRed = "impair.vision_red";
        const string CloseEyes = "kw.resp.close_eyes";
        const string BackAway = "kw.resp.back_away";
        const string LookAway = "kw.resp.dont_look_vending";
        const string Stare = "kw.resp.stare";
        const string Approach = "kw.resp.approach";
        const string RedLight = "kw.cond.vending_red";
        const string RedHand = "kw.cond.red_hand";
        const string CoinDrop = "kw.anom.coin_drop";

        static SiteData site;

        [OneTimeSetUp]
        public void LoadSite()
        {
            site = SiteLoader.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "subway_transfer.json")));
        }

        static DispatchSession NewSession(RankConfig config = null, int seed = 1) =>
            new DispatchSession(site, config ?? RankConfigs.Intern, new[] { Vending }, seed);

        /// <summary>자판기를 Active까지 진행</summary>
        static void Activate(DispatchSession s)
        {
            var runtime = s.Anomalies[Vending];
            Assert.That(runtime.TryAwaken(s.Player), Is.True);
            Assert.That(runtime.Escalate(), Is.True);
        }

        static OutcomeResult ActAndRespond(DispatchSession s, string action)
        {
            Activate(s);
            return s.Respond(Vending, action);
        }

        static void GiveMark(DispatchSession s)
        {
            s.Player.AddMark(Mark);
        }

        // ── 결과 표 한 줄씩 ──────────────────────────────

        [Test]
        public void H3_01_표식있음_눈감기_해결()
        {
            var s = NewSession();
            GiveMark(s);
            Assert.That(ActAndRespond(s, CloseEyes).Type, Is.EqualTo(OutcomeType.Resolve));
            Assert.That(s.Anomalies[Vending].State, Is.EqualTo(AnomalyState.Resolved));
        }

        [TestCase(BackAway)]
        [TestCase(LookAway)]
        [TestCase(Stare)]
        [TestCase(Approach)]
        [TestCase(PlayerActions.TurnAway)]
        public void H3_02_표식있음_눈감기외_전부_즉사(string action)
        {
            var s = NewSession();
            GiveMark(s);
            Assert.That(ActAndRespond(s, action).Type, Is.EqualTo(OutcomeType.Fatal));
        }

        [TestCase(BackAway)]
        [TestCase(LookAway)]
        public void H3_03_표식없음_뒷걸음질_안보기_해결(string action)
        {
            var s = NewSession();
            Assert.That(ActAndRespond(s, action).Type, Is.EqualTo(OutcomeType.Resolve));
        }

        [TestCase(Stare)]
        [TestCase(PlayerActions.Timeout)]
        [TestCase(PlayerActions.TurnAway)]
        public void H3_04_표식없음_응시_무반응_붉은시야(string action)
        {
            var s = NewSession();
            var result = ActAndRespond(s, action);
            Assert.That(result.Type, Is.EqualTo(OutcomeType.ImmediateHarm));
            Assert.That(s.Player.HasImpairment(VisionRed), Is.True);
            Assert.That(s.Anomalies[Vending].State, Is.EqualTo(AnomalyState.Violated));
        }

        [Test]
        public void H3_05_표식없음_다가감_표식만_남음()
        {
            var s = NewSession();
            var result = ActAndRespond(s, Approach);
            Assert.That(result.Type, Is.EqualTo(OutcomeType.ApplyMark));
            Assert.That(s.Player.HasMark(Mark), Is.True);
            Assert.That(s.Player.Impairments, Is.Empty, "그 자리에선 아무 피해 없음");
        }

        [Test]
        public void H3_06_표식없음_눈감기_아무일없음_Active유지()
        {
            var s = NewSession();
            Assert.That(ActAndRespond(s, CloseEyes).Type, Is.EqualTo(OutcomeType.Nothing));
            Assert.That(s.Anomalies[Vending].State, Is.EqualTo(AnomalyState.Active));
        }

        // ── 원칙 ──────────────────────────────

        [Test]
        public void H3_07_같은상태_같은행동은_항상_같은결과()
        {
            foreach (var action in new[] { CloseEyes, BackAway, LookAway, Stare, Approach, PlayerActions.Timeout, PlayerActions.TurnAway })
            foreach (var marked in new[] { false, true })
            {
                var results = Enumerable.Range(0, 5).Select(seed =>
                {
                    var s = NewSession(seed: seed);
                    if (marked) GiveMark(s);
                    return ActAndRespond(s, action).Type;
                }).Distinct().ToList();
                Assert.That(results.Count, Is.EqualTo(1), $"{action} / 표식 {marked}");
            }
        }

        [Test]
        public void H3_08_즉사후_표식제거_결손유지_목숨차감_자국키워드해금()
        {
            var s = NewSession(RankConfigs.AssistantManager);
            int lives = s.Player.Lives;
            ActAndRespond(s, Stare); // 붉은 시야
            s.Anomalies[Vending].Rearm();
            GiveMark(s);
            Activate(s);

            Assert.That(s.Respond(Vending, BackAway).Type, Is.EqualTo(OutcomeType.Fatal));
            Assert.That(s.Player.HasMark(Mark), Is.False, "표식 제거");
            Assert.That(s.Player.HasImpairment(VisionRed), Is.True, "결손 유지");
            Assert.That(s.Player.Lives, Is.EqualTo(lives - 1));
            Assert.That(s.UnlockedKeywords, Does.Contain(RedHand));
        }

        [Test]
        public void H3_09_인턴은_목숨무제한()
        {
            var s = NewSession(RankConfigs.Intern);
            for (int i = 0; i < 3; i++)
            {
                GiveMark(s);
                ActAndRespond(s, BackAway);
                s.Anomalies[Vending].Rearm();
            }
            Assert.That(s.Player.Deaths, Is.EqualTo(3));
            Assert.That(s.Player.OutOfLives, Is.False);
        }

        [Test]
        public void H3_10_구역을_벗어났다_들어오면_재발동()
        {
            var s = NewSession();
            var runtime = s.Anomalies[Vending];
            ActAndRespond(s, BackAway);
            Assert.That(runtime.TryAwaken(s.Player), Is.False, "해결 직후엔 재발동 안 함");
            runtime.Rearm();
            Assert.That(runtime.State, Is.EqualTo(AnomalyState.Dormant));
            Assert.That(runtime.TryAwaken(s.Player), Is.True);
        }

        [Test]
        public void H3_11_전조_현상을_겪으면_조건_이상현상_키워드해금()
        {
            var s = NewSession();
            var runtime = s.Anomalies[Vending];
            runtime.TryAwaken(s.Player);
            Assert.That(s.UnlockedKeywords, Does.Contain(RedLight));
            runtime.Escalate();
            Assert.That(s.UnlockedKeywords, Does.Contain(CoinDrop));
        }

        [Test]
        public void H3_12_같은시드면_같은변형()
        {
            var a = NewSession(seed: 42).Anomalies[Vending].Variant.Id;
            var b = NewSession(seed: 42).Anomalies[Vending].Variant.Id;
            Assert.That(a, Is.EqualTo(b));
        }

        // ── 정산: 분기 A/B (Q-D5) ──────────────────────────────

        static Rule R(string response, params string[] conditions) => new Rule(conditions, CoinDrop, response);

        [Test]
        public void H3_13_인턴_숨은조건없는_정답은_분기A정답_분기B미기재_힌트()
        {
            var result = NewSession().Settle(new[] { R(BackAway, RedLight) });
            Assert.That(result.Rules[0].Verdict, Is.EqualTo(Verdict.Correct));
            Assert.That(result.Rules[0].TargetId, Is.EqualTo(Vending + "/A"));
            Assert.That(result.Missing, Is.EqualTo(new[] { Vending + "/B" }));
            Assert.That(result.Hints, Has.Count.EqualTo(1));
        }

        [Test]
        public void H3_14_인턴_숨은조건포함_눈감기는_분기B정답()
        {
            var result = NewSession().Settle(new[] { R(CloseEyes, RedLight, RedHand) });
            Assert.That(result.Rules[0].Verdict, Is.EqualTo(Verdict.Correct));
            Assert.That(result.Rules[0].TargetId, Is.EqualTo(Vending + "/B"));
        }

        [Test]
        public void H3_15_인턴_A와B_둘다쓰면_둘다정답_미기재없음()
        {
            var result = NewSession().Settle(new[] { R(LookAway, RedLight), R(CloseEyes, RedLight, RedHand) });
            Assert.That(result.Rules.Select(r => r.Verdict), Is.All.EqualTo(Verdict.Correct));
            Assert.That(result.Missing, Is.Empty);
            Assert.That(result.Hints, Is.Empty);
            Assert.That(result.TotalScore, Is.EqualTo(20));
        }

        [Test]
        public void H3_16_인턴_숨은조건_빼고_눈감기는_정답아님()
        {
            // 숨은 조건은 RequireAllConditions(인턴=false)와 무관하게 필수
            var result = NewSession().Settle(new[] { R(CloseEyes, RedLight) });
            Assert.That(result.Rules[0].Verdict, Is.Not.EqualTo(Verdict.Correct));
        }

        [Test]
        public void H3_17_대리_분기B_놓쳐도_힌트없음()
        {
            var result = NewSession(RankConfigs.AssistantManager).Settle(new[] { R(BackAway, RedLight) });
            Assert.That(result.Rules[0].Verdict, Is.EqualTo(Verdict.Correct));
            Assert.That(result.Hints, Is.Empty);
        }

        [Test]
        public void H3_18_부장정책_숨은조건빠진_수칙은_오답()
        {
            // 붉은 자판기는 분기 A가 복수 정답이라 부장 풀에서 빠지므로, 대응 1개짜리 분기로 정책만 검증
            var targets = new List<AnswerTarget>
            {
                new AnswerTarget { Id = "x/A", AnomalyId = "x", Conditions = { RedLight }, Anomalies = { CoinDrop }, CorrectResponses = { BackAway } },
                new AnswerTarget { Id = "x/B", AnomalyId = "x", HiddenConditions = { RedHand }, Conditions = { RedLight }, Anomalies = { CoinDrop }, CorrectResponses = { CloseEyes } },
            };
            var gm = RankConfigs.GeneralManager;
            var result = Judge.JudgeTargets(new[] { R(BackAway, RedLight) }, targets, gm);
            Assert.That(result.Rules[0].Verdict, Is.EqualTo(Verdict.Wrong));
            Assert.That(result.Rules[0].Score, Is.EqualTo(gm.Score.Wrong));
            Assert.That(result.Missing, Does.Contain("x/B"));

            var withHidden = Judge.JudgeTargets(new[] { R(CloseEyes, RedLight, RedHand) }, targets, gm);
            Assert.That(withHidden.Rules[0].Verdict, Is.EqualTo(Verdict.Correct));
        }

        [Test]
        public void H3_19_제출한도는_분기수가_아니라_이상현상수_기준()
        {
            // 인턴: 이상현상 1개 + 2 = 3개까지
            var s = NewSession();
            var three = new[] { R(BackAway, RedLight), R(CloseEyes, RedLight, RedHand), R(Stare, RedLight) };
            Assert.DoesNotThrow(() => s.Settle(three));
            Assert.Throws<SubmissionLimitExceededException>(() => s.Settle(three.Append(R(LookAway, RedLight)).ToList()));
        }
    }
}
