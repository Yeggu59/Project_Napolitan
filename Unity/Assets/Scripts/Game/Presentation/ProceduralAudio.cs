// 임시 효과음 합성 — 사운드 에셋이 들어오는 분위기 작업(11/13) 전까지 사용

using UnityEngine;

namespace Napolitan.Game.Presentation
{
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;

        /// <summary>동전이 바닥에 떨어져 몇 번 튀는 소리 (금속 비조화 배음 + 빠른 감쇠)</summary>
        public static AudioClip CoinDrop()
        {
            float[] bounces = { 0f, 0.26f, 0.45f, 0.58f, 0.67f, 0.73f, 0.77f };
            float[] partials = { 2350f, 3720f, 5180f, 6930f };
            float length = 1.3f;
            var data = new float[(int)(SampleRate * length)];

            for (int b = 0; b < bounces.Length; b++)
            {
                float amp = 0.5f * Mathf.Pow(0.72f, b);
                int start = (int)(bounces[b] * SampleRate);
                int ring = (int)((b == 0 ? 0.45f : 0.2f) * SampleRate);
                for (int i = 0; i < ring && start + i < data.Length; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Exp(-t * (b == 0 ? 9f : 22f));
                    float s = 0f;
                    for (int p = 0; p < partials.Length; p++)
                        s += Mathf.Sin(2f * Mathf.PI * partials[p] * t) / (p + 1);
                    data[start + i] += s * env * amp;
                }
            }

            var clip = AudioClip.Create("CoinDrop", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
