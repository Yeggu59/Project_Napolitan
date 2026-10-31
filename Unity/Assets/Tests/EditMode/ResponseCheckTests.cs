// 행동 판정 (ResponseCheck) — 거리·시선·눈 감기 입력 → 대응 키워드 id

using NUnit.Framework;
using Napolitan.Core;

namespace Napolitan.Core.Tests
{
    public class ResponseCheckTests
    {
        const float Dt = 0.1f;
        const float Start = 6f;

        static ResponseCheck NewCheck(ResponseCheckSettings settings = null)
        {
            var check = new ResponseCheck(settings);
            check.Begin(Start);
            return check;
        }

        /// <summary>같은 입력으로 seconds 동안 진행, 처음 나온 행동 반환</summary>
        static string Run(ResponseCheck check, float seconds, float distance = Start, bool gazing = false, bool eyes = false)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += Dt)
            {
                var action = check.Tick(Dt, distance, gazing, eyes);
                if (action != null) return action;
            }
            return null;
        }

        [Test]
        public void RC_01_2초_응시하면_stare()
        {
            var check = NewCheck();
            Assert.That(Run(check, 1.8f, gazing: true), Is.Null);
            Assert.That(Run(check, 0.3f, gazing: true), Is.EqualTo(ResponseActions.Stare));
        }

        [Test]
        public void RC_02_3초_시선을_떼고_제자리면_look_away()
        {
            var check = NewCheck();
            Assert.That(Run(check, 2.8f), Is.Null);
            Assert.That(Run(check, 0.3f), Is.EqualTo(ResponseActions.LookAway));
        }

        [Test]
        public void RC_03_가까워지면_approach()
        {
            Assert.That(Run(NewCheck(), 0.1f, distance: Start - 1f, gazing: true), Is.EqualTo(ResponseActions.Approach));
        }

        [Test]
        public void RC_04_멀어지면_시선과_무관하게_back_away()
        {
            Assert.That(Run(NewCheck(), 0.1f, distance: Start + 2f, gazing: true), Is.EqualTo(ResponseActions.BackAway));
            Assert.That(Run(NewCheck(), 0.1f, distance: Start + 2f), Is.EqualTo(ResponseActions.BackAway));
        }

        [Test]
        public void RC_05_눈을_4초_감으면_close_eyes_감는동안_응시판정_없음()
        {
            var check = NewCheck();
            Assert.That(Run(check, 3.8f, gazing: true, eyes: true), Is.Null);
            Assert.That(Run(check, 0.3f, gazing: true, eyes: true), Is.EqualTo(ResponseActions.CloseEyes));
        }

        [Test]
        public void RC_06_눈감기는_떴다가_다시_감아야_재판정()
        {
            var check = NewCheck(new ResponseCheckSettings { TimeoutSeconds = 100f });
            Assert.That(Run(check, 4.1f, eyes: true), Is.EqualTo(ResponseActions.CloseEyes));
            check.Continue(Start); // 표식 없음 → 아무 일 없음
            Assert.That(Run(check, 2f, eyes: true), Is.Null, "계속 감고 있으면 다시 보고하지 않음");
            check.Tick(Dt, Start, false, false);
            Assert.That(Run(check, 4.1f, eyes: true), Is.EqualTo(ResponseActions.CloseEyes));
        }

        [Test]
        public void RC_07_응시와_외면을_번갈아하면_8초후_timeout_한번만()
        {
            var check = NewCheck();
            string action = null;
            for (int i = 0; i < 6 && action == null; i++)
                action = Run(check, 1.5f, gazing: i % 2 == 0);
            Assert.That(action, Is.EqualTo(PlayerActions.Timeout));
            check.Continue(Start);
            Assert.That(check.Tick(Dt, Start, true, false), Is.Null);
        }

        [Test]
        public void RC_08_같은_입력이면_같은_결과()
        {
            Assert.That(Run(NewCheck(), 5f, gazing: true), Is.EqualTo(Run(NewCheck(), 5f, gazing: true)));
        }
    }
}
