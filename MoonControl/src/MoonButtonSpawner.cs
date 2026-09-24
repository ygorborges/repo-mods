using System;
using UnityEngine;

namespace MoonControl
{
    // Places the moon button at the fixed spot configured in Plugin (ButtonPositionX/Y/Z). Normally once per visit to the
    // truck; while Plugin.LiveTuneSeconds is above 0 it keeps re-placing it on that interval instead, so a position/scale
    // tweak made through REPOConfig shows up on its own without leaving and re-entering the truck to see it.
    internal sealed class MoonButtonSpawner : MonoBehaviour
    {
        private bool spawnedForThisVisit;
        private float sinceSpawn;

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
            if (!SemiFunc.RunIsLobby())
            {
                spawnedForThisVisit = false;
                sinceSpawn = 0f;
                return;
            }

            if (spawnedForThisVisit)
            {
                float interval = Plugin.LiveTuneSeconds.Value;
                if (interval <= 0f)
                {
                    return;
                }
                sinceSpawn += Time.deltaTime;
                if (sinceSpawn < interval)
                {
                    return;
                }
            }

            spawnedForThisVisit = true;
            sinceSpawn = 0f;
            try
            {
                MoonButton.SpawnFixed();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not place the moon button: " + ex);
            }
        }
    }
}
