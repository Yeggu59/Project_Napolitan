// 이상현상 하나를 씬에 연결: 구역 진입/이탈 → 발동/재발동, 전조 → 현상 진행, 상태별 조명·소리
// 슬라이스: 붉은 자판기 — 전조 = 조명이 붉어짐, 현상 = 아무도 없는데 동전 떨어지는 소리

using System.Collections;
using Napolitan.Core;
using Napolitan.Game.Dispatch;
using Napolitan.Game.Presentation;
using UnityEngine;

namespace Napolitan.Game.Anomaly
{
    public class AnomalyController : MonoBehaviour
    {
        [SerializeField] DispatchManager dispatch;
        [SerializeField] string anomalyId = "anom.red_vending";
        [SerializeField] Transform player;
        [SerializeField] ResponseDetector detector;

        [Header("구역 (월드 축 기준 상자 — 벗어났다 들어오면 재발동)")]
        [SerializeField] Vector3 zoneCenter;
        [SerializeField] Vector3 zoneSize = new Vector3(4f, 3f, 16f);
        [Tooltip("전조(붉은 조명) 후 현상까지 걸리는 시간")]
        [SerializeField] float omenSeconds = 3f;

        [Header("연출")]
        [SerializeField] Light glow;
        [SerializeField] Renderer panel;
        [SerializeField] Color normalColor = new Color(0.85f, 0.92f, 1f);
        [SerializeField] Color omenColor = new Color(1f, 0.05f, 0.03f);
        [SerializeField] AudioSource coinSource;
        [SerializeField] float coinInterval = 2.5f;

        AnomalyRuntime runtime;
        Material panelMaterial;
        AudioClip coinClip;
        Coroutine coinLoop;
        bool inZone;
        float omenTimer;

        public AnomalyState State => runtime?.State ?? AnomalyState.Dormant;

        Bounds Zone => new Bounds(zoneCenter, zoneSize);

        void Start()
        {
            runtime = dispatch.Session.Anomalies[anomalyId];
            runtime.StateChanged += OnStateChanged;
            panelMaterial = panel.material; // 인스턴스
            coinClip = ProceduralAudio.CoinDrop();
            OnStateChanged(runtime.State);
        }

        void OnDestroy()
        {
            if (runtime != null) runtime.StateChanged -= OnStateChanged;
        }

        void Update()
        {
            bool now = Zone.Contains(player.position + Vector3.up * 0.9f);
            if (now && !inZone) runtime.TryAwaken(dispatch.Session.Player);
            else if (!now && inZone) runtime.Rearm();
            inZone = now;

            if (runtime.State == AnomalyState.Omen)
            {
                omenTimer += Time.deltaTime;
                if (omenTimer >= omenSeconds) runtime.Escalate();
            }
        }

        void OnStateChanged(AnomalyState state)
        {
            Debug.Log($"[이상현상] {anomalyId}: {state}");
            bool red = state == AnomalyState.Omen || state == AnomalyState.Active;
            SetColor(red ? omenColor : normalColor);

            if (state == AnomalyState.Omen) omenTimer = 0f;

            if (state == AnomalyState.Active)
            {
                coinLoop = StartCoroutine(CoinLoop());
                detector.Begin();
            }
            else
            {
                if (coinLoop != null) StopCoroutine(coinLoop);
                coinLoop = null;
                detector.Stop();
            }
        }

        IEnumerator CoinLoop()
        {
            var wait = new WaitForSeconds(coinInterval);
            while (true)
            {
                coinSource.PlayOneShot(coinClip);
                yield return wait;
            }
        }

        void SetColor(Color color)
        {
            glow.color = color;
            panelMaterial.SetColor("_BaseColor", color * 0.6f);
            panelMaterial.SetColor("_EmissionColor", color * 1.5f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.3f);
            Gizmos.DrawWireCube(zoneCenter, zoneSize);
        }
    }
}
