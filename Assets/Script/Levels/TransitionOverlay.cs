using UnityEngine;

namespace GameClub.Player
{
    public sealed class TransitionOverlay : MonoBehaviour
    {
        public float Alpha { get; set; }

        private void OnGUI()
        {
            if (Alpha <= 0f) return;
            Color previous = GUI.color;
            int depth = GUI.depth;
            GUI.depth = -1000;
            GUI.color = new Color(0f, 0f, 0f, Alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.depth = depth;
        }
    }
}
