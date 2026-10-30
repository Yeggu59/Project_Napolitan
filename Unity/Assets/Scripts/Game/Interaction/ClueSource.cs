using System;
using System.Collections.Generic;
using UnityEngine;

namespace Napolitan.Game.Interaction
{
    /// <summary>조사하면 정해진 Clue id를 주는 오브젝트 (공지문, 기록물, 사물 등)</summary>
    public class ClueSource : MonoBehaviour, IInspectable
    {
        [SerializeField] string prompt = "조사";
        [Tooltip("Assets/Data 구역 JSON의 clue id")]
        [SerializeField] string[] clueIds = Array.Empty<string>();
        [Tooltip("한 번 조사하면 더 이상 단서를 주지 않음")]
        [SerializeField] bool once = true;

        bool used;

        public string Prompt => prompt;

        public IReadOnlyList<string> Inspect()
        {
            if (once && used) return Array.Empty<string>();
            used = true;
            return clueIds;
        }
    }
}
