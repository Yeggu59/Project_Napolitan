// 구역 데이터 무결성 검사 — 데이터를 고치면 여기서 먼저 깨진다
// Prototype/web/src/data/sites.test.ts 이식

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Napolitan.Core;
using Napolitan.Game.Data;
using UnityEngine;

namespace Napolitan.Core.Tests
{
    [TestFixture("subway_transfer")]
    public class SiteDataTests
    {
        readonly SiteData site;
        readonly Dictionary<string, Keyword> byId;
        readonly List<Clue> allClues;
        readonly HashSet<string> revealed;

        public SiteDataTests(string siteFile)
        {
            var json = File.ReadAllText(Path.Combine(Application.dataPath, "Data", siteFile + ".json"));
            site = SiteLoader.Parse(json);
            byId = site.Keywords.ToDictionary(k => k.Id);
            allClues = site.Anomalies.SelectMany(a => a.Clues).Concat(site.NoiseClues).ToList();
            revealed = new HashSet<string>(allClues.SelectMany(c => c.RevealsKeywords));
        }

        void ExpectSlot(IEnumerable<string> ids, Slot slot)
        {
            foreach (var id in ids)
            {
                Assert.That(byId.ContainsKey(id), id + " 없음");
                Assert.That(byId[id].Slot, Is.EqualTo(slot), id);
            }
        }

        [Test]
        public void 로드되고_enum이_문자열에서_변환됨()
        {
            Assert.That(site.Keywords, Is.Not.Empty);
            Assert.That(site.Keywords.Select(k => k.Slot).Distinct(), Is.EquivalentTo(new[] { Slot.Condition, Slot.Anomaly, Slot.Response }));
        }

        [Test]
        public void id_중복없음()
        {
            var ids = site.Keywords.Select(k => k.Id)
                .Concat(site.Anomalies.Select(a => a.Id))
                .Concat(allClues.Select(c => c.Id))
                .ToList();
            Assert.That(ids, Is.Unique);
        }

        [Test]
        public void 이상현상이_참조하는_키워드가_존재하고_칸이_맞음()
        {
            foreach (var a in site.Anomalies)
            {
                ExpectSlot(a.Conditions, Slot.Condition);
                ExpectSlot(a.Anomalies, Slot.Anomaly);
                ExpectSlot(a.CorrectResponses, Slot.Response);
            }
        }

        [Test]
        public void 단서가_해금하는_키워드가_존재함()
        {
            foreach (var id in revealed) Assert.That(byId.ContainsKey(id), id);
        }

        [Test]
        public void 모든_키워드는_어떤_단서로든_해금가능()
        {
            var unreachable = site.Keywords.Where(k => !revealed.Contains(k.Id)).Select(k => k.Id);
            Assert.That(unreachable, Is.Empty);
        }

        [Test]
        public void 정답_키워드는_자기_단서로_해금됨_억까방지()
        {
            foreach (var a in site.Anomalies)
            {
                var own = new HashSet<string>(a.Clues.SelectMany(c => c.RevealsKeywords));
                foreach (var id in a.Conditions.Concat(a.Anomalies).Concat(a.CorrectResponses))
                    Assert.That(own.Contains(id), a.Id + " → " + id);
            }
        }

        [Test]
        public void 부장_풀이_비어있지_않음()
        {
            Assert.That(Judge.AnomalyPoolForRank(site.Anomalies, RankConfigs.GeneralManager), Is.Not.Empty);
        }

        [Test]
        public void 정답_수칙을_모두_내면_직급마다_만점()
        {
            foreach (var config in RankConfigs.All)
            {
                var pool = Judge.AnomalyPoolForRank(site.Anomalies, config);
                var manual = pool.Select(a => new Rule(a.Conditions, a.Anomalies[0], a.CorrectResponses[0])).ToList();
                var result = Judge.JudgeManual(manual, site.Anomalies, config);
                Assert.That(result.Missing, Is.Empty, config.Rank.ToString());
                Assert.That(result.TotalScore, Is.EqualTo(pool.Count * config.Score.Correct), config.Rank.ToString());
            }
        }
    }
}
