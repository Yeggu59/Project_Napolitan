// 피해·신체 상태 연출
// - 붉은 시야 결손: URP 비네팅 + 붉은 색조 (파견 끝까지 유지)
// - 표식: 화면 오른쪽 아래 손등에 붉은 자국 (인턴이라 뚜렷하게, 생긴 직후가 아니라 조금 뒤에 드러남)
// - 눈 감기 암전, 즉사 연출(손등 자국을 들어 보여준 뒤 암전 → 밴 재시작)
// 손·암전은 OnGUI 임시 그림 — 분위기 작업(11/13) 때 교체

using System;
using System.Collections;
using Napolitan.Game.Dispatch;
using Napolitan.Game.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Napolitan.Game.Presentation
{
    public class HarmPresenter : MonoBehaviour
    {
        [SerializeField] DispatchManager dispatch;
        [SerializeField] Eyes eyes;
        [SerializeField] Camera viewCamera;
        [SerializeField] string visionImpairmentId = "impair.vision_red";
        [SerializeField] string handMarkId = "mark.red_hand";

        [Header("붉은 시야")]
        [SerializeField] Color vignetteColor = new Color(0.45f, 0f, 0f);
        [SerializeField, Range(0f, 1f)] float vignetteIntensity = 0.55f;
        [SerializeField] Color visionTint = new Color(1f, 0.55f, 0.55f);

        [Header("표식")]
        [Tooltip("표식이 생기고 손등 자국이 보이기 시작할 때까지 (그 자리에선 아무 일 없음)")]
        [SerializeField] float markRevealDelay = 4f;
        [SerializeField] float markFadeIn = 2f;

        [Header("즉사")]
        [SerializeField] float deathHandSeconds = 1.8f;
        [SerializeField] float deathFadeSeconds = 0.8f;
        [SerializeField] float deathHoldSeconds = 0.6f;
        [SerializeField] float respawnFadeSeconds = 1f;

        Vignette vignette;
        ColorAdjustments colorAdjust;
        float vision, flash, eyesDark, fade, markAlpha, markTimer, handLift;
        bool deathHand;

        static Texture2D softCircle;

        void Start()
        {
            viewCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
            vignette.color.Override(vignetteColor);
            vignette.intensity.Override(0f);
            vignette.smoothness.Override(0.7f);
            colorAdjust = profile.Add<ColorAdjustments>(true);
            colorAdjust.colorFilter.Override(Color.white);

            var volume = new GameObject("HarmVolume").AddComponent<Volume>();
            volume.transform.SetParent(transform, false);
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        void Update()
        {
            var status = dispatch.Session.Player;
            float dt = Time.deltaTime;

            vision = Mathf.MoveTowards(vision, status.HasImpairment(visionImpairmentId) ? 1f : 0f, dt * 0.5f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 1.5f);
            vignette.intensity.value = Mathf.Clamp01(vision * vignetteIntensity + flash * 0.35f);
            colorAdjust.colorFilter.value = Color.Lerp(Color.white, visionTint, Mathf.Clamp01(vision * 0.7f + flash));

            eyesDark = Mathf.MoveTowards(eyesDark, eyes.Closed ? 1f : 0f, dt * 6f);

            if (status.HasMark(handMarkId))
            {
                markTimer += dt;
                markAlpha = Mathf.Clamp01((markTimer - markRevealDelay) / markFadeIn);
            }
            else if (!deathHand)
            {
                markTimer = 0f;
                markAlpha = 0f;
            }
        }

        /// <summary>즉시 피해 순간의 붉은 번쩍임</summary>
        public void Flash() => flash = 1f;

        /// <summary>즉사 연출. 화면이 완전히 검을 때 atBlack(재시작 위치 이동) 호출</summary>
        public IEnumerator PlayDeath(bool showMark, Action atBlack)
        {
            flash = 1f;
            if (showMark)
            {
                deathHand = true;
                markAlpha = 1f; // 늦게 드러나기 전이어도 죽기 직전엔 반드시 보여줌 (신호)
                for (float t = 0f; t < deathHandSeconds; t += Time.deltaTime)
                {
                    handLift = Mathf.SmoothStep(0f, 1f, t / (deathHandSeconds * 0.6f));
                    yield return null;
                }
            }

            for (float t = 0f; t < deathFadeSeconds; t += Time.deltaTime)
            {
                fade = t / deathFadeSeconds;
                yield return null;
            }
            fade = 1f;
            atBlack();
            deathHand = false;
            handLift = 0f;
            yield return new WaitForSeconds(deathHoldSeconds);

            for (float t = 0f; t < respawnFadeSeconds; t += Time.deltaTime)
            {
                fade = 1f - t / respawnFadeSeconds;
                yield return null;
            }
            fade = 0f;
        }

        void OnGUI()
        {
            GUI.depth = -10;
            if (markAlpha > 0f) DrawHand(markAlpha, handLift);

            float dark = Mathf.Max(eyesDark, fade);
            if (dark > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, dark);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        /// <summary>손등(임시 도형). lift 0 = 오른쪽 아래 걸침, 1 = 화면 가운데로 들어 올림</summary>
        static void DrawHand(float alpha, float lift)
        {
            float w = Screen.width, h = Screen.height;
            float scale = Mathf.Lerp(h * 0.0022f, h * 0.0045f, lift);
            var origin = Vector2.Lerp(new Vector2(w * 0.78f, h * 0.86f), new Vector2(w * 0.5f, h * 0.55f), lift);

            var skin = new Color(0.82f, 0.66f, 0.56f, alpha);
            GUI.color = skin;
            Rect R(float x, float y, float rw, float rh) => new Rect(origin.x + x * scale, origin.y + y * scale, rw * scale, rh * scale);
            GUI.DrawTexture(R(-60f, -40f, 120f, 110f), Texture2D.whiteTexture); // 손등
            for (int i = 0; i < 4; i++)
                GUI.DrawTexture(R(-58f + i * 30f, -120f + Mathf.Abs(i - 1.5f) * 12f, 24f, 90f), Texture2D.whiteTexture); // 손가락
            GUI.DrawTexture(R(-95f, -10f, 40f, 24f), Texture2D.whiteTexture); // 엄지

            GUI.color = new Color(0.7f, 0f, 0.02f, alpha * 0.9f);
            GUI.DrawTexture(R(-35f, -25f, 70f, 55f), SoftCircle()); // 붉은 자국
            GUI.color = Color.white;
        }

        static Texture2D SoftCircle()
        {
            if (softCircle != null) return softCircle;
            const int size = 64;
            softCircle = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                softCircle.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
            softCircle.Apply();
            return softCircle;
        }
    }
}
