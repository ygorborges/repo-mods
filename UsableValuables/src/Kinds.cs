using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace UsableValuables
{
    internal enum KindId : byte
    {
        Flashlight = 1,
        Boombox = 2,
        Candle = 3,
        StarWand = 4,
        WizardStaff = 5,
        Camera = 6,
        LevitationPotion = 7,
        IceSaw = 8,
        Blender = 9,
        Jackhammer = 10,
        ScreamDoll = 11,
        Flamethrower = 12,
        FireExtinguisher = 13,
    }

    // The valuables the key works on: what each one is, what its prompt says, and what pressing the key does to it.
    //
    // Three families:
    //  - switches (flashlight, boombox, candle, ice saw, blender, jackhammer, scream doll): they run by themselves while
    //    held, and the key turns that off and on;
    //  - triggers (flamethrower, fire extinguisher): in the game they are fired by grabbing their trigger and burn fuel until
    //    empty; the key pulls and lets go of that trigger, using the game's own methods;
    //  - actions (star wand, wizard staff, camera, levitation potion): the key runs the same method the game runs when the
    //    valuable is hit, so the effect (and its networking) is exactly the game's own.
    internal static class Kinds
    {
        // Pressing the key on a switch twice in a row can't flicker it faster than this.
        internal const float SwitchDelay = 0.3f;

        internal sealed class Info
        {
            public KindId Id;
            public string Name;
            public bool Switch;
            public bool Trigger;             // flamethrower / extinguisher: the key starts and stops the flames
            public string PromptOn;          // shown while a switch is on (or for an action, or a trigger not yet pulled): what the key will do
            public string PromptOff;         // shown while a switch is off (or a trigger is firing)
            public string PromptEmpty;       // triggers only: shown when the fuel is gone
            public ConfigEntry<bool> Enabled;
            public ConfigEntry<float> Cooldown;
            public ConfigEntry<float> Reactivate;    // trap-like switches only: chance (%) per second to switch itself back on
        }

        private static readonly Dictionary<KindId, Info> all = new Dictionary<KindId, Info>();

        internal static Info Get(KindId id)
        {
            return all[id];
        }

        internal static void Bind(ConfigFile config)
        {
            AddSwitch(config, KindId.Flashlight, "Flashlight",
                "Press [interact] to turn off", "Press [interact] to turn on",
                "The flashlight valuable lights up while you hold it; the key switches it off and on.");
            AddSwitch(config, KindId.Boombox, "Boombox",
                "Press [interact] to mute", "Press [interact] to play",
                "The boombox valuable plays music (and makes you dance) while you hold it; the key mutes it and starts it again.", 50f);
            AddSwitch(config, KindId.Candle, "Candle",
                "Press [interact] to blow out the candle", "Press [interact] to light the candle",
                "The key blows the candle's flame out and lights it again.");
            AddSwitch(config, KindId.IceSaw, "IceSaw",
                "Press [interact] to switch the saw off", "Press [interact] to switch the saw on",
                "The ice saw spins up as soon as you grab it; the key switches it off and on.", 50f);
            AddSwitch(config, KindId.Blender, "Blender",
                "Press [interact] to switch the blender off", "Press [interact] to switch the blender on",
                "The blender starts as soon as you grab it; the key switches it off and on.", 50f);
            AddSwitch(config, KindId.Jackhammer, "Jackhammer",
                "Press [interact] to switch the jackhammer off", "Press [interact] to switch the jackhammer on",
                "The jackhammer starts as soon as you grab it; the key switches it off and on.", 50f);
            AddSwitch(config, KindId.ScreamDoll, "ScreamDoll",
                "Press [interact] to silence the doll", "Press [interact] to wake the doll",
                "The scream doll starts as soon as you grab it; the key silences it and wakes it again.", 50f);

            AddTrigger(config, KindId.Flamethrower, "Flamethrower",
                "Press [interact] to fire", "Press [interact] to stop", "Out of fuel",
                "The flamethrower valuable is fired by its trigger and burns fuel until it is empty; the key starts and stops the flames "
                + "(they also stop if you let go of it).");
            AddTrigger(config, KindId.FireExtinguisher, "FireExtinguisher",
                "Press [interact] to spray", "Press [interact] to stop", "Empty",
                "The fire extinguisher valuable is fired by its trigger and runs out of fuel; the key starts and stops the spray "
                + "(it also stops if you let go of it).");

            AddAction(config, KindId.StarWand, "StarWand", "Press [interact] to cast a spell", 1.5f,
                "The key casts the wand's spell, as if you had swung it. Like a swing, every cast costs the wand part of its value.");
            AddAction(config, KindId.WizardStaff, "WizardStaff", "Press [interact] to fire the staff", 3f,
                "The key fires the staff's laser.");
            AddAction(config, KindId.Camera, "Camera", "Press [interact] to use the flash", 6f,
                "The key sets off the camera's flash (the stunning burst it makes when it takes damage).");
            AddAction(config, KindId.LevitationPotion, "LevitationPotion", "Press [interact] to use the potion", 30f,
                "The key releases the potion's levitation sphere, as if it had been smashed (the potion stays intact).");
        }

        // reactivatePercent >= 0 makes it a trap-like switch: while you keep holding it switched off, each second it has
        // that chance (in percent) to switch itself back on. The lights and the candle are not traps and don't do that.
        private static Info AddSwitch(ConfigFile config, KindId id, string section, string promptOn, string promptOff, string description,
            float reactivatePercent = -1f)
        {
            Info info = new Info { Id = id, Name = section, Switch = true, PromptOn = promptOn, PromptOff = promptOff };
            info.Enabled = config.Bind(section, "Enabled", true, description + " Only the host's setting is used.");
            if (reactivatePercent >= 0f)
            {
                info.Reactivate = config.Bind(section, "ReactivateChancePerSecond", reactivatePercent,
                    new ConfigDescription("Chance, in percent, that this valuable switches itself back on each second you keep holding it "
                        + "while it is switched off, the way the game's traps go off by themselves. 0 = never. Only the host's setting is used.",
                        new AcceptableValueRange<float>(0f, 100f)));
            }
            all[id] = info;
            return info;
        }

        private static Info AddTrigger(ConfigFile config, KindId id, string section, string promptStart, string promptStop, string promptEmpty,
            string description)
        {
            Info info = new Info { Id = id, Name = section, Trigger = true, PromptOn = promptStart, PromptOff = promptStop, PromptEmpty = promptEmpty };
            info.Enabled = config.Bind(section, "Enabled", true, description + " Only the host's setting is used.");
            all[id] = info;
            return info;
        }

        private static Info AddAction(ConfigFile config, KindId id, string section, string prompt, float cooldown, string description)
        {
            Info info = new Info { Id = id, Name = section, Switch = false, PromptOn = prompt, PromptOff = prompt };
            info.Enabled = config.Bind(section, "Enabled", true, description + " Only the host's setting is used.");
            info.Cooldown = config.Bind(section, "CooldownSeconds", cooldown,
                new ConfigDescription("Seconds before the same valuable can be used again. Only the host's setting is used.",
                    new AcceptableValueRange<float>(0.5f, 600f)));
            all[id] = info;
            return info;
        }

        // Which supported valuable, if any, is this physics body?
        internal static bool TryResolve(PhysGrabObject body, out KindId id, out Component component)
        {
            id = 0;
            component = null;
            if (body == null)
            {
                return false;
            }

            GameObject go = body.gameObject;
            return Match<ValuableFlashlight>(go, KindId.Flashlight, ref id, ref component)
                || Match<ValuableBoombox>(go, KindId.Boombox, ref id, ref component)
                || Match<ValuableForeverCandle>(go, KindId.Candle, ref id, ref component)
                || Match<IceSawValuable>(go, KindId.IceSaw, ref id, ref component)
                || Match<BlenderValuable>(go, KindId.Blender, ref id, ref component)
                || Match<JackhammerValuable>(go, KindId.Jackhammer, ref id, ref component)
                || Match<ScreamDollValuable>(go, KindId.ScreamDoll, ref id, ref component)
                || Match<FlamethrowerValuable>(go, KindId.Flamethrower, ref id, ref component)
                || Match<FireExtinguisherValuable>(go, KindId.FireExtinguisher, ref id, ref component)
                || Match<ValuableStarWand>(go, KindId.StarWand, ref id, ref component)
                || Match<ValuableWizardStaff>(go, KindId.WizardStaff, ref id, ref component)
                || Match<ValuableCamera>(go, KindId.Camera, ref id, ref component)
                || Match<ValuableLevitationPotion>(go, KindId.LevitationPotion, ref id, ref component);
        }

        private static bool Match<T>(GameObject go, KindId kind, ref KindId id, ref Component component) where T : Component
        {
            // The valuable's script may sit on the physics body itself or on a child of it.
            T found = go.GetComponentInChildren<T>(true);
            if (found == null)
            {
                return false;
            }
            id = kind;
            component = found;
            return true;
        }

        // What the hint says for this valuable right now.
        internal static string Prompt(KindId id, Component component)
        {
            Info info = all[id];
            if (info.Trigger)
            {
                if (TriggerEmpty(component))
                {
                    return info.PromptEmpty;
                }
                return TriggerFiring(component) ? info.PromptOff : info.PromptOn;
            }
            return info.Switch && State.IsOff(component) ? info.PromptOff : info.PromptOn;
        }

        // Whether a flamethrower / extinguisher has its flames (or spray) on right now.
        internal static bool TriggerFiring(Component component)
        {
            FlamethrowerValuable flamethrower = component as FlamethrowerValuable;
            if (flamethrower != null)
            {
                return Refs.FlamethrowerFiring(flamethrower);
            }
            FireExtinguisherValuable extinguisher = component as FireExtinguisherValuable;
            return extinguisher != null && Refs.ExtinguisherFiring(extinguisher);
        }

        private static bool TriggerEmpty(Component component)
        {
            FlamethrowerValuable flamethrower = component as FlamethrowerValuable;
            if (flamethrower != null)
            {
                return Refs.FlamethrowerState(flamethrower) == FlamethrowerValuable.States.Empty;
            }
            FireExtinguisherValuable extinguisher = component as FireExtinguisherValuable;
            return extinguisher != null && Refs.ExtinguisherState(extinguisher) == FireExtinguisherValuable.States.Empty;
        }

        // Runs on every client when the host says a valuable was used.
        internal static void Apply(KindId id, Component component, bool off)
        {
            try
            {
                switch (id)
                {
                    case KindId.Flashlight:
                    case KindId.Boombox:
                    case KindId.IceSaw:
                    case KindId.Blender:
                    case KindId.Jackhammer:
                    case KindId.ScreamDoll:
                        // Nothing to poke: the patches on the valuable's own Update make it behave as if let go while it is off.
                        State.SetOff(component, off, id);
                        break;
                    case KindId.Candle:
                        State.SetOff(component, off, id);
                        SetCandle((ValuableForeverCandle)component, !off);
                        break;
                    case KindId.Flamethrower:
                    {
                        // GrabTrigger / ReleaseTrigger only act on the host, which then tells everybody (the game's own networking).
                        FlamethrowerValuable flamethrower = (FlamethrowerValuable)component;
                        if (off)
                        {
                            flamethrower.ReleaseTrigger();
                            State.Unwatch(component);
                        }
                        else
                        {
                            flamethrower.GrabTrigger();
                            State.Watch(component, id);
                        }
                        break;
                    }
                    case KindId.FireExtinguisher:
                    {
                        FireExtinguisherValuable extinguisher = (FireExtinguisherValuable)component;
                        if (off)
                        {
                            extinguisher.ReleaseTrigger();
                            State.Unwatch(component);
                        }
                        else
                        {
                            extinguisher.GrabTrigger();
                            State.Watch(component, id);
                        }
                        break;
                    }
                    case KindId.StarWand:
                        CastWand((ValuableStarWand)component);
                        break;
                    case KindId.WizardStaff:
                        ((ValuableWizardStaff)component).StaffLaser();
                        break;
                    case KindId.Camera:
                        ((ValuableCamera)component).Explosion();
                        break;
                    case KindId.LevitationPotion:
                        ((ValuableLevitationPotion)component).ActivateSphere();
                        break;
                }
                Info used = all[id];
                Plugin.Log.LogDebug("Used " + id + (used.Switch ? (off ? " (now off)" : " (now on)") : (used.Trigger ? (off ? " (stopped)" : " (started)") : "")));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not use " + id + ": " + ex);
            }
        }

        // The same steps the game runs after a swing (see ValuableStarWand's debug cast): cast, then restart the cooldown.
        private static void CastWand(ValuableStarWand wand)
        {
            Refs.WandCastSpell.Invoke(wand, new object[] { true });
            Refs.WandShootTimer(wand) = 0f;
            Refs.WandReadyToShoot(wand) = false;
            Refs.WandCanShoot(wand) = false;
        }

        // Blowing a candle out: the flame object goes away and its light is faded to nothing. The light manager fades lights
        // towards PropLight.originalIntensity, so that is set to zero too (and put back when the candle is lit).
        private static void SetCandle(ValuableForeverCandle candle, bool lit)
        {
            // Look for the flames from the whole physics body, in case they are not below the candle's own script.
            PhysGrabObject body = candle.GetComponentInParent<PhysGrabObject>();
            Transform root = body != null ? body.transform : candle.transform;
            foreach (CandleFlame flame in root.GetComponentsInChildren<CandleFlame>(true))
            {
                PropLight light = flame.propLight;
                if (light != null)
                {
                    Light lamp = Refs.LightComponent(light);
                    if (!lit)
                    {
                        State.RememberCandleIntensity(light, Refs.OriginalIntensity(light));
                        Refs.OriginalIntensity(light) = 0f;
                        if (lamp != null)
                        {
                            lamp.intensity = 0f;
                        }
                    }
                    else
                    {
                        float original;
                        if (State.TryTakeCandleIntensity(light, out original))
                        {
                            Refs.OriginalIntensity(light) = original;
                            if (lamp != null)
                            {
                                lamp.intensity = original;
                            }
                        }
                    }
                }
                // Never switch off an object that carries the whole valuable (the physics body or the candle script itself).
                if (flame.gameObject != candle.gameObject && (body == null || flame.gameObject != body.gameObject))
                {
                    flame.gameObject.SetActive(lit);
                }
            }

            AssetManager assets = AssetManager.instance;
            Sound click = assets == null ? null : (lit ? assets.soundDeviceTurnOn : assets.soundDeviceTurnOff);
            if (click != null)
            {
                click.Play(candle.transform.position);
            }
        }
    }
}
