using System;
using UnityEngine;

namespace UsableValuables
{
    // The distortion the game puts on the screen of whoever holds a trap when it goes off, the one you get when the TV turns
    // on: the game's Trap class runs CameraGlitch.PlayLong for the local player when a trap goes off in their hands (the glitch
    // animation, a camera shake and its sound). This is the same call, for the valuables this mod gives behaviours to.
    internal static class ScreenGlitch
    {
        private static bool errorLogged;

        // On this machine only: CameraGlitch is the local player's camera.
        internal static void Play()
        {
            try
            {
                CameraGlitch glitch = CameraGlitch.Instance;
                if (glitch != null)
                {
                    glitch.PlayLong();
                }
            }
            catch (Exception ex)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Plugin.Log.LogWarning("The screen glitch could not be played (logged once): " + ex.Message);
                }
            }
        }

        // For a valuable somebody is holding: the glitch goes to whoever holds it on this machine.
        internal static void PlayIfHeldHere(PhysGrabObject body)
        {
            if (body != null && body.grabbedLocal)
            {
                Play();
            }
        }

        // For something that happens around a place: the glitch goes to this machine's player if they are within range.
        internal static void PlayIfNear(Vector3 position, float range)
        {
            PlayerController player = PlayerController.instance;
            if (player != null && Vector3.Distance(player.transform.position, position) <= range)
            {
                Play();
            }
        }
    }
}
