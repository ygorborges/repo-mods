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

        // The prompt as it is shown, remembered too. Putting the key names into it (InputManager.InputDisplayReplaceTags) asks the input
        // system for the name of every one of the game's 22 key tags and builds new strings for each, which the game does once, when a
        // text changes. Done every frame it made holding any of these valuables stutter, so it is done once per text and again when
        // another valuable is picked up (which is also when a key rebound in the menu would be noticed).
        private static string displayedFor;
        private static string displayed;
        private static int shownWait = -1;
        private static string shownWaitText;

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
                displayedFor = null;
                displayed = null;
                shownWait = -1;
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

            // A one-shot trap that has already gone off has nothing left to press.
            string prompt = Kinds.Prompt(cachedId, cachedComponent);
            if (prompt == null)
            {
                return;
            }

            // Switches, triggers and one-shot traps only have the short anti-spam delay; the actions show how long until they are ready again.
            float wait = info.Switch || info.Trigger || info.OneShot ? 0f : State.Remaining(cachedComponent);
            if (Plugin.ShowPrompt.Value)
            {
                ShowPrompt(wait, prompt);
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
                int seconds = Mathf.CeilToInt(wait);
                if (seconds != shownWait || shownWaitText == null)
                {
                    shownWait = seconds;
                    shownWaitText = "<color=#9a9a9a>Ready in " + seconds + " s</color>";
                }
                text = shownWaitText;
            }
            else
            {
                if (displayed == null || displayedFor != prompt)
                {
                    InputManager input = InputManager.instance;
                    displayed = input != null ? input.InputDisplayReplaceTags(prompt, "<color=#fff><u><b>", "</b></u></color>") : prompt;
                    displayedFor = prompt;
                }
                text = displayed;
            }
            if (ItemInfoUI.instance != null)
            {
                ItemInfoUI.instance.ItemInfoText(null, text);
            }
        }
    }
}
