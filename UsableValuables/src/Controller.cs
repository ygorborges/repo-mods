using UnityEngine;

namespace UsableValuables
{
    // Every frame: if the local player holds a valuable the key works on, say what the key does and react to it.
    internal static class Controller
    {
        // What was resolved last time, so the components aren't looked up again every frame.
        private static PhysGrabObject cachedBody;
        private static bool cachedSupported;
        private static KindId cachedId;
        private static Component cachedComponent;

        internal static void Tick()
        {
            PhysGrabber grabber = PhysGrabber.instance;
            if (grabber == null || !grabber.grabbed)
            {
                return;
            }
            PhysGrabObject held = Refs.Grabbed(grabber);
            if (held == null)
            {
                return;
            }

            if (held != cachedBody)
            {
                cachedBody = held;
                cachedSupported = Kinds.TryResolve(held, out cachedId, out cachedComponent);
            }
            if (!cachedSupported || cachedComponent == null)
            {
                return;
            }
            Kinds.Info info = Kinds.Get(cachedId);
            if (!info.Enabled.Value)
            {
                return;
            }

            // Switches and triggers only have the short anti-spam delay; the actions show how long until they are ready again.
            float wait = info.Switch || info.Trigger ? 0f : State.Remaining(cachedComponent);
            if (Plugin.ShowPrompt.Value)
            {
                ShowPrompt(wait, Kinds.Prompt(cachedId, cachedComponent));
            }

            // The same conditions the game's own item toggle uses: not while a menu or the chat has taken the input.
            PlayerController player = PlayerController.instance;
            if (player == null || Refs.InputDisableTimer(player) > 0f || !SemiFunc.NoTextInputsActive())
            {
                return;
            }
            if (wait > 0f || !SemiFunc.InputDown(InputKey.Interact))
            {
                return;
            }
            Net.Use(held, cachedId);
        }

        private static void ShowPrompt(float wait, string prompt)
        {
            string text;
            if (wait > 0.05f)
            {
                text = "<color=#9a9a9a>Ready in " + Mathf.CeilToInt(wait) + " s</color>";
            }
            else
            {
                InputManager input = InputManager.instance;
                text = input != null ? input.InputDisplayReplaceTags(prompt, "<color=#fff><u><b>", "</b></u></color>") : prompt;
            }
            if (ItemInfoUI.instance != null)
            {
                ItemInfoUI.instance.ItemInfoText(null, text);
            }
        }
    }
}
