// 판정 규칙 테스트 케이스 (T1~T13)
// Prototype/web/src/core/judge.test.ts 와 1:1. Window > General > Test Runner > EditMode 에서 실행

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Napolitan.Core;

namespace Napolitan.Core.Tests
{
    public class JudgeTests
    {
        // 기준 케이스: 붉은빛 + 기계음 + {뒷걸음질, 눈 감기}
        static readonly AnomalyDef Vending = new AnomalyDef
        {
            Id = "vending_machine",
            SiteId = "subway",
            Name = "붉은 자판기",
            Conditions = { "kw.cond.red_light" },
            Anomalies = { "kw.anom.mech_noise" },
            CorrectResponses = { "kw.resp.back_away", "kw.resp.close_eyes" },
        };

        // 다중 조건 + 정답 대응 1개 (부장 풀에도 남음)
        static readonly AnomalyDef Flicker = new AnomalyDef
        {
            Id = "flicker_corridor",
            SiteId = "subway",
            Name = "깜빡이는 통로",
            Conditions = { "kw.cond.night", "kw.cond.flicker" },
            Anomalies = { "kw.anom.footsteps" },
            CorrectResponses = { "kw.resp.stand_still" },
        };

        static Rule R(string[] condition, string anomaly, string response) => new Rule(condition, anomaly, response);

        static readonly Rule VendingCorrect = R(new[] { "kw.cond.red_light" }, "kw.anom.mech_noise", "kw.resp.back_away");
        static readonly Rule VendingWrongResponse = R(new[] { "kw.cond.red_light" }, "kw.anom.mech_noise", "kw.resp.press_button");
        static readonly Rule FlickerCorrect = R(new[] { "kw.cond.night", "kw.cond.flicker" }, "kw.anom.footsteps", "kw.resp.stand_still");

        static ManualJudgment JudgeOne(Rule rule, AnomalyDef anomaly, RankConfig config) =>
            Judge.JudgeManual(new[] { rule }, new[] { anomaly }, config);

        [TestCase(Rank.Intern)]
        [TestCase(Rank.AssistantManager)]
        public void T1_3칸일치_정답(Rank rank)
        {
            var r = JudgeOne(VendingCorrect, Vending, RankConfigs.Get(rank)).Rules[0];
            Assert.AreEqual(Verdict.Correct, r.Verdict);
            Assert.AreEqual(10, r.Score);
        }

        [Test]
        public void T1_부장_정답대응1개_3칸일치_정답()
        {
            var r = JudgeOne(FlickerCorrect, Flicker, RankConfigs.GeneralManager).Rules[0];
            Assert.AreEqual(Verdict.Correct, r.Verdict);
        }

        [Test]
        public void T2_인턴_대응만틀림_부분정답_대응칸표시()
        {
            var r = JudgeOne(VendingWrongResponse, Vending, RankConfigs.Intern).Rules[0];
            Assert.AreEqual(Verdict.Partial, r.Verdict);
            CollectionAssert.AreEqual(new[] { Slot.Response }, r.WrongSlots);
            Assert.AreEqual(3, r.Score);
        }

        [Test]
        public void T3_대리_대응만틀림_부분정답_틀린칸표시없음()
        {
            var r = JudgeOne(VendingWrongResponse, Vending, RankConfigs.AssistantManager).Rules[0];
            Assert.AreEqual(Verdict.Partial, r.Verdict);
            Assert.IsNull(r.WrongSlots);
        }

        [Test]
        public void T4_부장_대응만틀림_오답()
        {
            var rule = R(new[] { "kw.cond.night", "kw.cond.flicker" }, "kw.anom.footsteps", "kw.resp.run");
            var r = JudgeOne(rule, Flicker, RankConfigs.GeneralManager).Rules[0];
            Assert.AreEqual(Verdict.Wrong, r.Verdict);
            Assert.AreEqual(-6, r.Score);
        }

        [Test]
        public void T5_인턴_두번째정답대응도_정답()
        {
            var rule = R(new[] { "kw.cond.red_light" }, "kw.anom.mech_noise", "kw.resp.close_eyes");
            Assert.AreEqual(Verdict.Correct, JudgeOne(rule, Vending, RankConfigs.Intern).Rules[0].Verdict);
        }

        [Test]
        public void T6_부장_정답대응2개짜리는_풀에서제외()
        {
            var pool = Judge.AnomalyPoolForRank(new[] { Vending, Flicker }, RankConfigs.GeneralManager);
            CollectionAssert.AreEqual(new[] { "flicker_corridor" }, pool.Select(a => a.Id).ToList());
        }

        [TestCase(Rank.Intern)]
        [TestCase(Rank.AssistantManager)]
        [TestCase(Rank.GeneralManager)]
        public void T7_수칙없음_전부미기재(Rank rank)
        {
            var result = Judge.JudgeManual(new List<Rule>(), new[] { Vending, Flicker }, RankConfigs.Get(rank));
            var expected = rank == Rank.GeneralManager
                ? new[] { "flicker_corridor" }
                : new[] { "vending_machine", "flicker_corridor" };
            CollectionAssert.AreEqual(expected, result.Missing);
            Assert.AreEqual(0, result.TotalScore);
        }

        [Test]
        public void T8_인턴_같은이상현상에_정답과오답()
        {
            var wrong = R(new[] { "kw.cond.flicker" }, "kw.anom.mech_noise", "kw.resp.press_button");
            var result = Judge.JudgeManual(new[] { VendingCorrect, wrong }, new[] { Vending }, RankConfigs.Intern);
            CollectionAssert.AreEqual(new[] { Verdict.Correct, Verdict.Wrong }, result.Rules.Select(r => r.Verdict).ToList());
            Assert.AreEqual(8, result.TotalScore);
        }

        [Test]
        public void T9_다중조건중1개만_인턴정답_대리부분정답()
        {
            var rule = R(new[] { "kw.cond.night" }, "kw.anom.footsteps", "kw.resp.stand_still");
            Assert.AreEqual(Verdict.Correct, JudgeOne(rule, Flicker, RankConfigs.Intern).Rules[0].Verdict);
            Assert.AreEqual(Verdict.Partial, JudgeOne(rule, Flicker, RankConfigs.AssistantManager).Rules[0].Verdict);
        }

        [Test]
        public void T10_인턴_같은이상현상에_정답2개_하나만점수()
        {
            var second = R(new[] { "kw.cond.red_light" }, "kw.anom.mech_noise", "kw.resp.close_eyes");
            var result = Judge.JudgeManual(new[] { VendingCorrect, second }, new[] { Vending }, RankConfigs.Intern);
            CollectionAssert.AreEqual(new[] { Verdict.Correct, Verdict.Duplicate }, result.Rules.Select(r => r.Verdict).ToList());
            Assert.AreEqual(10, result.TotalScore);
        }

        [Test]
        public void T11_부장_제출한도초과_제출불가()
        {
            var manual = new[] { FlickerCorrect, FlickerCorrect };
            Assert.Throws<SubmissionLimitExceededException>(
                () => Judge.JudgeManual(manual, new[] { Flicker }, RankConfigs.GeneralManager));
            Assert.IsFalse(Judge.CanSubmit(manual.Length, new[] { Flicker }, RankConfigs.GeneralManager));
        }

        [Test]
        public void T12_인턴_1칸만일치_오답()
        {
            var rule = R(new[] { "kw.cond.red_light" }, "kw.anom.footsteps", "kw.resp.press_button");
            Assert.AreEqual(Verdict.Wrong, JudgeOne(rule, Vending, RankConfigs.Intern).Rules[0].Verdict);
        }

        [Test]
        public void T13_인턴_정답에없는조건섞임_조건칸틀림()
        {
            var rule = R(new[] { "kw.cond.red_light", "kw.cond.night" }, "kw.anom.mech_noise", "kw.resp.back_away");
            var r = JudgeOne(rule, Vending, RankConfigs.Intern).Rules[0];
            Assert.AreEqual(Verdict.Partial, r.Verdict);
            CollectionAssert.AreEqual(new[] { Slot.Condition }, r.WrongSlots);
        }
    }
}
