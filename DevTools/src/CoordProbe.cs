using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace DevTools
{
    // A live on-screen readout of where the camera is looking: the world XYZ of whatever the crosshair is aimed at. Built
    // for tuning MoonControl's ButtonPositionX/Y/Z (a fixed spot in the truck) without guessing values and reloading the
    // level to check - aim at the floor spot you want and read the numbers straight off. A capture key writes the same
    // numbers to the BepInEx log in a single grep-able line, so they can be read back from LogOutput.log and applied to a
    // .cfg without having to be typed in by hand.
    internal sealed class CoordProbe : MonoBehaviour
    {
        private const float MaxDistance = 15f;
        internal const string CaptureTag = "[DevTools coords]";

        internal bool Visible { get; set; }
        internal ManualLogSource Log { get; set; }

        private string text = "";
        private Vector3 point;
        private float capturedFlashUntil;

        private void Update()
        {
            if (!Visible)
            {
                return;
            }
            text = Build();
        }

        private string Build()
        {
            PlayerController player = PlayerController.instance;
            if (player == null || player.playerAvatarScript == null || player.playerAvatarScript.localCamera == null)
            {
                return "DevTools coord probe: no player camera yet.";
            }
            Transform view = player.playerAvatarScript.localCamera.GetOverrideTransform();

            string surface;
            RaycastHit hit;
            if (Physics.Raycast(view.position, view.forward, out hit, MaxDistance, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                surface = hit.collider != null ? hit.collider.name : "?";
            }
            else
            {
                point = view.position + view.forward * MaxDistance;
                surface = "(nothing hit within " + MaxDistance.ToString("F0") + " m - aim at a floor or wall)";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("You: ").Append(view.position.ToString("F2")).Append('\n');
            sb.Append("World XYZ: ").Append(point.ToString("F2")).Append('\n');
            sb.Append("Looking at: ").Append(surface);
            sb.Append("\n(press the capture key to write \"World XYZ\" to the log)");
            if (Time.unscaledTime < capturedFlashUntil)
            {
                sb.Append("\n\nCaptured.");
            }
            return sb.ToString();
        }

        // Writes the currently displayed position to the log in one line, exact float precision, so it can be read back
        // from LogOutput.log and applied to a .cfg without anyone having to read it off the screen.
        internal void Capture()
        {
            if (!Visible)
            {
                return;
            }
            if (Log != null)
            {
                Log.LogInfo(CaptureTag + " X=" + point.x.ToString("R") + " Y=" + point.y.ToString("R") + " Z=" + point.z.ToString("R"));
            }
            capturedFlashUntil = Time.unscaledTime + 1.5f;
        }

        private void OnGUI()
        {
            if (!Visible || string.IsNullOrEmpty(text))
            {
                return;
            }
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                wordWrap = false,
            };
            style.normal.textColor = Color.white;
            Vector2 size = style.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(16f, 16f, size.x + 20f, size.y + 20f), text, style);
        }
    }
}
