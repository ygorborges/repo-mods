using System;
using UnityEngine;

namespace MoonControl
{
    // Places the moon button at the fixed spot configured in Plugin (ButtonPositionX/Y/Z). Normally once per visit to the
    // truck; while Plugin.LiveTuneSeconds is above 0 it keeps re-placing it on that interval instead, so a position/scale
    // tweak made through REPOConfig shows up on its own without leaving and re-entering the truck to see it.
    internal sealed class MoonButtonSpawner : MonoBehaviour
    {
        // RunIsLobby() turns true the moment the run's level variable changes, which is well before the truck scene itself
        // is up: a button placed in that window is thrown away along with the old scene, and with live tuning off nothing
        // ever placed another one (which is exactly why it only ever showed up with tuning switched on). So placement waits
        // for the game to actually be running the level, and simply keeps an eye on the button from then on - if it is not
        // there, it gets placed, whatever the reason it went missing.
        private const float SettleSeconds = 1f;
        private const float RetrySeconds = 1f;

        private float sinceReady;
        private float sinceSpawn;
        private bool placedThisVisit;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (!Plugin.Enabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            bool inTruck = SemiFunc.RunIsLobby() && GameDirector.instance != null
                && GameDirector.instance.currentState == GameDirector.gameState.Main;
            if (!inTruck)
            {
                sinceReady = 0f;
                sinceSpawn = 0f;
                placedThisVisit = false;
                return;
            }

            sinceReady += Time.deltaTime;
            sinceSpawn += Time.deltaTime;
            if (sinceReady < SettleSeconds || sinceSpawn < RetrySeconds)
            {
                return;
            }

            bool missing = !MoonButton.Placed;
            float interval = Plugin.LiveTuneSeconds.Value;
            bool retune = interval > 0f && sinceSpawn >= interval;
            if (!missing && !retune)
            {
                return;
            }

            sinceSpawn = 0f;
            try
            {
                MoonButton.SpawnFixed(!placedThisVisit);
                placedThisVisit = true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not place the moon button: " + ex);
            }
        }
    }
}
