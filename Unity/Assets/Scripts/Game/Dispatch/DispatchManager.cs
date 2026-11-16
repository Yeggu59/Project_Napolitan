// 파견 1회를 씬에 연결: DispatchSession 보유, 조사 → 단서, 행동 판정 결과 → 연출, 즉사 → 밴 재시작

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Napolitan.Core;
using Napolitan.Game.Data;
using Napolitan.Game.Interaction;
using Napolitan.Game.Player;
using Napolitan.Game.Presentation;
using UnityEngine;

namespace Napolitan.Game.Dispatch
{
    [DefaultExecutionOrder(-100)] // 다른 컴포넌트가 Start에서 Session을 쓰기 전에 생성
    public class DispatchManager : MonoBehaviour
    {
        [SerializeField] TextAsset siteJson;
        [SerializeField] Rank rank = Rank.Intern;
        [SerializeField] string[] anomalyIds = { "anom.red_vending" };
        [Tooltip("변형 선택 시드. 0이면 매번 다름")]
        [SerializeField] int seed;
        [SerializeField] Transform vanSpawn;
        [SerializeField] FirstPersonController player;
        [SerializeField] Inspector inspector;
        [SerializeField] HarmPresenter harm;

        [Tooltip("파견 시작 시 현장 시계 (저널 시각 표시용, 서담시 CCTV 타임스탬프 톤)")]
        [SerializeField] string startClock = "02:10:00";

        public DispatchSession Session { get; private set; }
        public Journal Journal { get; private set; }
        public FirstPersonController Player => player;
        public string SiteName { get; private set; }
        public Rank Rank => rank;

        float startTime;

        /// <summary>파견 시작 후 초</summary>
        public float Elapsed => Time.time - startTime;

        /// <summary>파견 시작 후 t초의 현장 시각 "HH:mm:ss"</summary>
        public string FormatClock(float t)
        {
            var start = System.TimeSpan.TryParse(startClock, out var s) ? s : System.TimeSpan.Zero;
            var at = start + System.TimeSpan.FromSeconds(t);
            return $"{(int)at.TotalHours % 24:00}:{at.Minutes:00}:{at.Seconds:00}";
        }

        /// <summary>사망 연출 중 (행동 판정 정지)</summary>
        public bool Dying { get; private set; }

        /// <summary>디버그 표시용: 마지막 행동과 결과</summary>
        public string LastOutcome { get; private set; } = "-";

        void Awake()
        {
            var site = SiteLoader.Parse(siteJson.text);
            var config = RankConfigs.All.First(c => c.Rank == rank);
            int actualSeed = seed != 0 ? seed : System.Environment.TickCount;
            Session = new DispatchSession(site, config, anomalyIds, actualSeed);
            SiteName = site.Name;
            startTime = Time.time;
            Journal = new Journal(Session, () => Elapsed);
            Session.KeywordUnlocked += id => Debug.Log($"[해금] {id}");
            Debug.Log($"[파견] {site.Name} / {rank} / 시드 {actualSeed} / " +
                      string.Join(", ", Session.Anomalies.Values.Select(r => $"{r.Def.Id}:{r.Variant.Id}")));
        }

        void OnEnable() => inspector.ClueFound += OnClueFound;
        void OnDisable() => inspector.ClueFound -= OnClueFound;

        void OnClueFound(string clueId) => Session.AddClue(clueId);

        /// <summary>현장 행동 판정. 이상현상이 Active가 아니거나 사망 연출 중이면 null</summary>
        public OutcomeResult Respond(string anomalyId, string action)
        {
            if (Dying) return null;
            var marksBefore = Session.Player.Marks.ToList(); // 즉사하면 표식이 지워지므로 연출용으로 미리 보관
            var result = Session.Respond(anomalyId, action);
            if (result == null) return null;

            LastOutcome = $"{action} → {result.Type}";
            Debug.Log($"[행동 판정] {anomalyId}: {LastOutcome}");

            switch (result.Type)
            {
                case OutcomeType.ImmediateHarm:
                    harm.Flash();
                    break;
                case OutcomeType.Fatal:
                    StartCoroutine(DieAndRespawn(marksBefore));
                    break;
            }
            return result;
        }

        IEnumerator DieAndRespawn(List<string> marksAtDeath)
        {
            Dying = true;
            player.LockInput(this);
            yield return harm.PlayDeath(marksAtDeath.Count > 0,
                () => player.Teleport(vanSpawn.position, vanSpawn.eulerAngles.y));
            player.UnlockInput(this);
            Dying = false;

            // 목숨 소진 시 강제 복귀는 정산 흐름(11/11)에서 연결
            if (Session.Player.OutOfLives) Debug.LogWarning("[파견] 목숨 소진 — 강제 복귀 (미구현)");
        }
    }
}
