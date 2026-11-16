// 임시 HUD: 조준점 + "[E] 대상" 표시. 분위기 작업(11/13) 때 UI로 교체

using UnityEngine;

namespace Napolitan.Game.Interaction
{
    [RequireComponent(typeof(Inspector))]
    public class InspectHud : MonoBehaviour
    {
        Inspector inspector;
        GUIStyle promptStyle;

        Napolitan.Game.Player.FirstPersonController controller;

        void Awake()
        {
            inspector = GetComponent<Inspector>();
            controller = GetComponent<Napolitan.Game.Player.FirstPersonController>();
        }

        void OnGUI()
        {
            if (controller != null && controller.InputLocked) return;

            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            GUI.DrawTexture(new Rect(center.x - 2f, center.y - 2f, 4f, 4f), Texture2D.whiteTexture);

            if (inspector.Current == null) return;
            promptStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 18 };
            GUI.Label(new Rect(center.x - 150f, center.y + 20f, 300f, 30f), $"[E] {inspector.Current.Prompt}", promptStyle);
        }
    }
}
