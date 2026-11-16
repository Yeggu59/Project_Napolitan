// 플레이 확인용 디버그 표시 (F1 토글). 저널·정산 UI가 생기기 전까지 상태를 눈으로 보기 위함

using System.Linq;
using Napolitan.Game.Anomaly;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Napolitan.Game.Dispatch
{
    public class DispatchDebugHud : MonoBehaviour
    {
        [SerializeField] DispatchManager dispatch;
        [SerializeField] AnomalyController anomaly;
        [SerializeField] ResponseDetector detector;
        [SerializeField] bool visible = true;

        GUIStyle style;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) visible = !visible;
        }

        /// <summary>현상 중 판정 진행 상황 (어느 행동에 얼마나 가까운지)</summary>
        string CheckLine()
        {
            var c = detector.Check;
            if (!detector.Running || c == null) return "";
            if (c.Reacting) return $"  반응 시간 {c.Elapsed:0.0}s\n";
            return $"  응시 {c.StareTime:0.0}/2  외면 {c.AwayTime:0.0}/3  눈감기 {c.ClosedTime:0.0}/4  거리변화 {c.DistanceChange:+0.0;-0.0}m  경과 {c.Elapsed:0.0}/8\n";
        }

        void OnGUI()
        {
            if (!visible || dispatch.Session == null) return;
            var s = dispatch.Session;
            var p = s.Player;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 13 };

            string lives = p.UnlimitedLives ? "무제한" : p.Lives.ToString();
            string text =
                $"[F1] 디버그   눈 감기 [C]\n" +
                $"자판기: {anomaly.State}   구역 {(anomaly.InZone ? "안" : "밖")}   감지 {(detector.Running ? "중" : "-")}   응시 {(detector.Gazing ? "O" : "X")}\n" +
                CheckLine() +
                $"마지막: {dispatch.LastOutcome}\n" +
                $"표식: {(p.Marks.Count == 0 ? "-" : string.Join(", ", p.Marks))}   결손: {(p.Impairments.Count == 0 ? "-" : string.Join(", ", p.Impairments))}\n" +
                $"목숨: {lives}   사망 {p.Deaths}   단서 {s.FoundClues.Count}   키워드 {s.UnlockedKeywords.Count}\n" +
                string.Join("\n", s.UnlockedKeywords.Select(id => "  · " + id));

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(8f, 8f, 420f, style.CalcHeight(new GUIContent(text), 400f) + 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(16f, 14f, 400f, 2000f), text, style);
        }
    }
}
