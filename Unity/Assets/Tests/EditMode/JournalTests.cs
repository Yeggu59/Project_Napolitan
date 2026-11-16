// 저널 — 겪은 순서·시각 기록, 칸별 키워드, 의심됨 체크, 숨은 결과 비노출

using System.IO;
using System.Linq;
using NUnit.Framework;
using Napolitan.Core;
using Napolitan.Game.Data;
using UnityEngine;

namespace Napolitan.Core.Tests
{
    public class JournalTests
    {
        const string Vending = "anom.red_vending";

        static SiteData site;
        float now;
        DispatchSession session;
        Journal journal;

        [OneTimeSetUp]
        public void LoadSite()
        {
            site = SiteLoader.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "subway_transfer.json")));
        }

        [SetUp]
        public void NewDispatch()
        {
            now = 0f;
            session = new DispatchSession(site, RankConfigs.Intern, new[] { Vending }, 1);
            journal = new Journal(session, () => now);
        }

        void Activate()
        {
            var runtime = session.Anomalies[Vending];
            now = 10f;
            runtime.TryAwaken(session.Player);
            now = 12f;
            runtime.Escalate();
        }

        [Test]
        public void J_01_조사한_단서는_원문_그대로_기록_키워드는_칸별로()
        {
            now = 5f;
            session.AddClue("clue.vending.3");
            var entry = journal.Entries.Single();
            Assert.That(entry.Kind, Is.EqualTo(JournalEntryKind.Clue));
            Assert.That(entry.Time, Is.EqualTo(5f));
            Assert.That(entry.DisplayText, Is.EqualTo(entry.Text));
            Assert.That(journal.Keywords(Slot.Response).Select(k => k.Keyword.Id),
                Is.EquivalentTo(new[] { "kw.resp.dont_look_vending", "kw.resp.check_source" }));
            Assert.That(journal.Keywords(Slot.Condition), Is.Empty);
        }

        [Test]
        public void J_02_같은_단서를_두번_조사해도_한번만()
        {
            session.AddClue("noise.2");
            session.AddClue("noise.2");
            Assert.That(journal.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public void J_03_전조_현상_결과를_겪은_순서와_시각으로_기록()
        {
            Activate();
            now = 15f;
            session.Respond(Vending, "kw.resp.stare");

            Assert.That(journal.Entries.Select(e => e.Kind), Is.All.EqualTo(JournalEntryKind.Field));
            Assert.That(journal.Entries.Select(e => e.Time), Is.EqualTo(new[] { 10f, 12f, 15f }));
            Assert.That(journal.Keywords(Slot.Condition).Single().Keyword.Id, Is.EqualTo("kw.cond.vending_red"));
            Assert.That(journal.Keywords(Slot.Anomaly).Single().Keyword.Id, Is.EqualTo("kw.anom.coin_drop"));
        }

        [Test]
        public void J_04_표식은_기록에_드러나지_않음()
        {
            Activate();
            session.Respond(Vending, "kw.resp.approach");
            var record = journal.Entries.Last().Text;
            Assert.That(record, Does.Not.Contain("자국").And.Not.Contain("표식").And.Not.Contain("손"));
        }

        [Test]
        public void J_05_즉사하면_손등자국_조건키워드가_저널에()
        {
            session.Player.AddMark("mark.red_hand");
            Activate();
            session.Respond(Vending, "kw.resp.back_away");
            Assert.That(journal.Keywords(Slot.Condition).Select(k => k.Keyword.Id), Does.Contain("kw.cond.red_hand"));
        }

        [Test]
        public void J_06_의심됨_체크는_해금된_키워드만()
        {
            session.AddClue("clue.vending.4");
            journal.SetSuspected("kw.resp.back_away", true);
            journal.SetSuspected("kw.resp.close_eyes", true); // 아직 해금 안 됨
            Assert.That(journal.SuspectedKeywordIds, Is.EqualTo(new[] { "kw.resp.back_away" }));
            journal.SetSuspected("kw.resp.back_away", false);
            Assert.That(journal.SuspectedKeywordIds, Is.Empty);
        }

        [Test]
        public void J_07_결과표_모든줄과_전조_현상에_기록문장이_있음()
        {
            foreach (var v in site.Anomalies.SelectMany(a => a.Variants))
            {
                Assert.That(v.OmenRecord, Is.Not.Empty, v.Id);
                Assert.That(v.ActiveRecord, Is.Not.Empty, v.Id);
                foreach (var o in v.Outcomes) Assert.That(o.Result.Record, Is.Not.Empty, $"{v.Id} {o.Action}");
            }
        }
    }
}
