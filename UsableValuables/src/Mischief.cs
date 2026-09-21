using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UsableValuables
{
    internal enum Quirk : byte
    {
        None = 0,
        BananaBow = 1,
        Handface = 2,
        Horse = 3,
        Piano = 4,
    }

    internal enum QuirkPhase : byte
    {
        Start = 1,      // banana bow: the fuse is lit; handface: it starts laughing; horse: it whinnies; piano: it starts its song
        Boom = 2,       // banana bow: it blows up
        Twitch = 3,     // handface, secondary behaviour (Creep): its creeping sound starts
        Settle = 4,     // ... and fades out (piano: its song stops)
        Launch = 5,     // ... and it is flung at the nearest player
        Warn = 6,       // banana bow: the "uh-oh" right before it blows up
    }

    // Two valuables that do nothing in the game get behaviours of their own, with no key involved.
    //
    //  - the banana bow, while somebody holds it: each second a small chance that it starts a bomb fuse burning, says "uh-oh"
    //    (the chance to react) and blows up, and is gone;
    //  - the handface, while somebody holds it: each second a chance that it starts laughing (loudly) and shaking like a
    //    cursed doll, which draws enemies for as long as it goes on; it stops a couple of seconds after it was let go of;
    //  - the handface, when nobody holds it (see Creep): it twitches and makes a creeping sound while a player moves near it,
    //    and if that player keeps moving until the sound is over it is flung at the nearest player.
    //
    // Only the host rolls the dice and runs the physics and the enemy alerts; the sounds and the explosion are played on
    // every client through one network event (see Net.AnnounceQuirk).
    internal static class Mischief
    {
        // How hard the shaking is, before the strength setting scales it (per unit of mass): the game's toy monkey twists with
        // a torque of 0.5, which was too gentle for a cursed doll. Every so often there is a short, harder spasm.
        private const float ShakeTorque = 4f;
        private const float ShakeForce = 8f;
        private const float SpasmBoost = 2.5f;

        private static ConfigEntry<bool> bananaEnabled;
        private static ConfigEntry<float> bananaChance;
        private static ConfigEntry<float> bananaWindup;
        private static ConfigEntry<float> bananaSize;
        private static ConfigEntry<int> bananaDamage;
        private static ConfigEntry<int> bananaEnemyDamage;
        private static ConfigEntry<float> bananaWarnVolume;
        private static ConfigEntry<float> bananaWarnFalloff;
        private static ConfigEntry<bool> bananaGlitch;

        private static ConfigEntry<bool> faceEnabled;
        private static ConfigEntry<bool> faceGlitch;
        private static ConfigEntry<float> faceChance;
        private static ConfigEntry<float> faceStopAfterDrop;
        private static ConfigEntry<float> faceShake;
        private static ConfigEntry<float> faceAlertRange;
        private static ConfigEntry<float> faceLaughVolume;
        private static ConfigEntry<float> faceLaughFalloff;

        // A banana bow that has started winding up (host only), keyed by the physics body's instance id.
        private sealed class Fuse
        {
            public PhysGrabObject Body;
            public float WarnAt;        // when the "uh-oh" plays
            public bool Warned;
            public float BoomAt;
            public bool Announced;
            public float DestroyAt;
        }

        // A handface that is laughing and shaking because it was held (host only).
        private sealed class Laughing
        {
            public PhysGrabObject Body;
            public PhysGrabObjectImpactDetector Impact;    // to tell when it is in the cart
            public float ReleasedAt = -1f;     // when it was let go of; -1 while somebody holds it
            public float NextAlert;
        }

        // On every client: the fuse that is burning, so it can be kept going and cut when the banana bow blows up.
        private sealed class Burning
        {
            public PhysGrabObject Body;
            public GameAudio.Fuse Sound;
            public float StartedAt;
        }

        private sealed class Spasm
        {
            public float NextAt;
            public float Until;
        }

        private static readonly Dictionary<int, Fuse> fuses = new Dictionary<int, Fuse>();
        private static readonly Dictionary<int, Laughing> laughing = new Dictionary<int, Laughing>();
        private static readonly Dictionary<int, Burning> burning = new Dictionary<int, Burning>();
        private static readonly Dictionary<int, Quirk> resolved = new Dictionary<int, Quirk>();
        private static readonly Dictionary<int, Spasm> spasms = new Dictionary<int, Spasm>();
        private static readonly HashSet<int> shaken = new HashSet<int>();

        private static float nextRoll;

        internal static float AlertRange { get { return faceAlertRange.Value; } }

        internal static bool FaceGlitch { get { return faceGlitch.Value; } }

        // How long the "uh-oh" lasts (the explosion comes as it ends).
        private static float WarningSeconds { get { return UserAudio.Warning != null ? UserAudio.Warning.length : 0.8f; } }

        internal static void Bind(ConfigFile config)
        {
            bananaEnabled = config.Bind("BananaBow", "Enabled", true,
                "The banana bow valuable does nothing in the game. With this on, each second somebody holds it there is a small chance a bomb "
                + "fuse starts burning and it blows up a moment later, and is gone. Only the host's setting is used.");
            bananaChance = config.Bind("BananaBow", "ExplodeChancePerSecond", 3f,
                new ConfigDescription("Chance, in percent, that a held banana bow lights its fuse, each second. 0 = never. "
                    + "Only the host's setting is used.", new AcceptableValueRange<float>(0f, 100f)));
            bananaWindup = config.Bind("BananaBow", "WindupSeconds", 2f,
                new ConfigDescription("Seconds the bomb fuse burns before the \"uh-oh\" warning; the explosion follows as the warning ends "
                    + "(so it goes off this many seconds plus about one after the fuse is lit), whether the banana bow is held or not. "
                    + "Only the host's setting is used.", new AcceptableValueRange<float>(0.5f, 10f)));
            bananaSize = config.Bind("BananaBow", "ExplosionSize", 1f,
                new ConfigDescription("Size of the explosion (the game's barrel uses 1, the propane tank 0.8, the clown 1.5). "
                    + "Only the host's setting is used.", new AcceptableValueRange<float>(0.25f, 3f)));
            bananaDamage = config.Bind("BananaBow", "PlayerDamage", 50,
                new ConfigDescription("Damage the explosion does to players (the game's barrel does 50). Only the host's setting is used.",
                    new AcceptableValueRange<int>(0, 200)));
            bananaEnemyDamage = config.Bind("BananaBow", "EnemyDamage", 100,
                new ConfigDescription("Damage the explosion does to enemies (the game's barrel does 100). Only the host's setting is used.",
                    new AcceptableValueRange<int>(0, 500)));

            bananaWarnVolume = config.Bind("BananaBow", "WarningVolume", 1f,
                new ConfigDescription("How loud the \"uh-oh\" is on your machine (1 = as loud as a sound can be played).",
                    new AcceptableValueRange<float>(0f, 1f)));
            bananaWarnFalloff = config.Bind("BananaBow", "WarningFalloff", 1.5f,
                new ConfigDescription("How far the \"uh-oh\" carries on your machine (1 = like the game's own sounds).",
                    new AcceptableValueRange<float>(1f, 5f)));

            bananaGlitch = config.Bind("BananaBow", "ScreenGlitch", true,
                "Whoever holds the banana bow when its fuse is lit gets the screen glitch the game shows when a trap goes off (the one you get "
                + "when the TV turns on). On your machine only.");

            faceGlitch = config.Bind("Handface", "ScreenGlitch", true,
                "Whoever holds the handface when it starts laughing, and a player within range when it starts creeping, gets the screen glitch the "
                + "game shows when a trap goes off (the one you get when the TV turns on). On your machine only.");

            faceEnabled = config.Bind("Handface", "Enabled", true,
                "The handface valuable does nothing in the game. With this on, each second somebody holds it there is a chance it starts laughing "
                + "loudly and shaking like a cursed doll, which draws enemies until it stops. Only the host's setting is used.");
            faceChance = config.Bind("Handface", "ActivateChancePerSecond", 15f,
                new ConfigDescription("Chance, in percent, that a held handface starts laughing and shaking, each second. 0 = never. "
                    + "Only the host's setting is used.", new AcceptableValueRange<float>(0f, 100f)));
            faceStopAfterDrop = config.Bind("Handface", "StopSecondsAfterDrop", 2f,
                new ConfigDescription("It keeps shaking for as long as somebody holds it, and stops this many seconds after it was let go of "
                    + "(picking it up again in time keeps it going). Only the host's setting is used.", new AcceptableValueRange<float>(0f, 30f)));
            faceShake = config.Bind("Handface", "ShakeStrength", 1f,
                new ConfigDescription("How hard it shakes: 1 = convulsing like a cursed doll, with short harder spasms (about eight times the game's "
                    + "toy monkey); 0.25 = a gentle tremble; 0 = it does not shake. Only the host's setting is used.",
                    new AcceptableValueRange<float>(0f, 5f)));
            faceAlertRange = config.Bind("Handface", "AlertRange", 35f,
                new ConfigDescription("Metres around it within which enemies are drawn to the noise, every second, for as long as it shakes "
                    + "or its creeping sound plays (the game's own traps use 35). 0 = it does not alert enemies. Only the host's setting is used.",
                    new AcceptableValueRange<float>(0f, 100f)));
            faceLaughVolume = config.Bind("Handface", "LaughVolume", 1f,
                new ConfigDescription("How loud the laugh is on your machine (1 = as loud as a sound can be played; the game's own sounds are "
                    + "about half that).", new AcceptableValueRange<float>(0f, 1f)));
            faceLaughFalloff = config.Bind("Handface", "LaughFalloff", 2f,
                new ConfigDescription("How far the laugh carries on your machine (1 = like the game's own sounds, 2 = twice as far).",
                    new AcceptableValueRange<float>(1f, 5f)));

            Creep.Bind(config);
            Haunts.Bind(config);
        }

        // Which of the two, if either, is this physics body? By the name of its prefab (they have no script of their own).
        // Names do not change, so the answer is remembered.
        internal static Quirk Resolve(PhysGrabObject body)
        {
            if (body == null)
            {
                return Quirk.None;
            }
            int id = body.GetInstanceID();
            Quirk quirk;
            if (resolved.TryGetValue(id, out quirk))
            {
                return quirk;
            }
            quirk = ResolveByName(body);
            if (resolved.Count > 4000)
            {
                resolved.Clear();
            }
            resolved[id] = quirk;
            return quirk;
        }

        private static Quirk ResolveByName(PhysGrabObject body)
        {
            if (body.GetComponentInParent<ValuableObject>() == null)
            {
                return Quirk.None;
            }
            string own = body.name;
            string root = body.transform.root.name;
            if (Has(own, "banana bow") || Has(root, "banana bow"))
            {
                return Quirk.BananaBow;
            }
            if (Has(own, "handface") || Has(root, "handface"))
            {
                return Quirk.Handface;
            }
            if (Has(own, "horse") || Has(root, "horse"))
            {
                return Quirk.Horse;
            }
            if (Has(own, "piano") || Has(root, "piano"))
            {
                return Quirk.Piano;
            }
            return Quirk.None;
        }

        private static bool Has(string name, string part)
        {
            return name != null && name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---- host: the dice, the timers, the alerts

        internal static void Tick()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            float now = Time.time;
            if (now >= nextRoll)
            {
                nextRoll = now + State.RollInterval;
                Roll(now);
            }
            TickFuses(now);
            TickFaces(now);
            Creep.Tick(now);
            Haunts.Tick(now);
        }

        private static void Roll(float now)
        {
            RoundDirector round = RoundDirector.instance;
            List<PhysGrabObject> bodies = round == null ? null : Refs.RoundBodies(round);
            if (bodies == null)
            {
                return;
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                PhysGrabObject body = bodies[i];
                Quirk quirk = Resolve(body);
                if (quirk == Quirk.None)
                {
                    continue;
                }
                if (quirk == Quirk.Handface)
                {
                    Creep.Register(body);
                }
                else if (quirk == Quirk.Horse)
                {
                    Haunts.RegisterHorse(body);
                }
                else if (quirk == Quirk.Piano)
                {
                    Haunts.RegisterPiano(body);
                }

                // The dice are only rolled while somebody holds it.
                if (!body.grabbed || body.dead)
                {
                    continue;
                }
                if (quirk == Quirk.BananaBow && bananaEnabled.Value && Chance(bananaChance.Value))
                {
                    LightFuse(body, now);
                }
                else if (quirk == Quirk.Handface && faceEnabled.Value && Chance(faceChance.Value))
                {
                    StartLaughing(body, now);
                }
                else if (quirk == Quirk.Piano)
                {
                    Haunts.RollPiano(body);
                }
            }
        }

        internal static bool Chance(float percent)
        {
            return percent > 0f && Random.value * 100f < percent;
        }

        private static void LightFuse(PhysGrabObject body, float now)
        {
            int id = body.GetInstanceID();
            if (fuses.ContainsKey(id))
            {
                return;
            }
            // Never start what cannot be finished: without the game's explosion the banana bow is left alone.
            if (!Explosions.Ready())
            {
                return;
            }
            Plugin.Log.LogDebug("A banana bow lit its fuse.");
            float warnAt = now + bananaWindup.Value;
            fuses[id] = new Fuse { Body = body, WarnAt = warnAt, BoomAt = warnAt + WarningSeconds + 0.1f };
            Net.AnnounceQuirk(body, Quirk.BananaBow, QuirkPhase.Start);
        }

        private static void TickFuses(float now)
        {
            if (fuses.Count == 0)
            {
                return;
            }

            List<int> done = null;
            foreach (KeyValuePair<int, Fuse> pair in fuses)
            {
                Fuse fuse = pair.Value;
                if (fuse.Body == null)
                {
                    if (done == null)
                    {
                        done = new List<int>();
                    }
                    done.Add(pair.Key);
                    continue;
                }

                if (!fuse.Warned && now >= fuse.WarnAt)
                {
                    fuse.Warned = true;
                    Net.AnnounceQuirk(fuse.Body, Quirk.BananaBow, QuirkPhase.Warn);
                }

                if (!fuse.Announced)
                {
                    if (now >= fuse.BoomAt)
                    {
                        fuse.Announced = true;
                        // The object goes a moment after the explosion is announced, so nobody loses it before they see it go off.
                        fuse.DestroyAt = now + 0.25f;
                        Net.AnnounceQuirk(fuse.Body, Quirk.BananaBow, QuirkPhase.Boom,
                            bananaSize.Value, bananaDamage.Value, bananaEnemyDamage.Value);
                    }
                }
                else if (now >= fuse.DestroyAt)
                {
                    PhysGrabObjectImpactDetector impact = fuse.Body.GetComponent<PhysGrabObjectImpactDetector>();
                    if (impact != null)
                    {
                        impact.DestroyObject();
                    }
                    if (done == null)
                    {
                        done = new List<int>();
                    }
                    done.Add(pair.Key);
                }
            }

            if (done != null)
            {
                foreach (int id in done)
                {
                    fuses.Remove(id);
                }
            }
        }

        private static void StartLaughing(PhysGrabObject body, float now)
        {
            int id = body.GetInstanceID();
            if (laughing.ContainsKey(id))
            {
                return;
            }
            // Not once it has been flung (it is done for good), not while it is creeping, and not in the rest after the creeping.
            if (!Creep.PrimaryAllowed(body, now))
            {
                return;
            }
            Plugin.Log.LogDebug("A handface started laughing.");
            laughing[id] = new Laughing { Body = body, Impact = body.GetComponent<PhysGrabObjectImpactDetector>() };
            Net.AnnounceQuirk(body, Quirk.Handface, QuirkPhase.Start);
        }

        // Is this handface laughing (and shaking) because somebody held it?
        internal static bool IsLaughing(PhysGrabObject body)
        {
            return body != null && laughing.ContainsKey(body.GetInstanceID());
        }

        private static void TickFaces(float now)
        {
            if (laughing.Count == 0)
            {
                return;
            }

            List<int> done = null;
            foreach (KeyValuePair<int, Laughing> pair in laughing)
            {
                Laughing face = pair.Value;
                bool over = face.Body == null || face.Body.dead;
                if (!over)
                {
                    if (face.Impact != null && face.Impact.inCart)
                    {
                        // Put in the cart: it stops at once, without waiting out the seconds after it was let go of.
                        over = true;
                    }
                    else if (face.Body.grabbed)
                    {
                        face.ReleasedAt = -1f;
                    }
                    else if (face.ReleasedAt < 0f)
                    {
                        face.ReleasedAt = now;
                    }
                    else if (now - face.ReleasedAt >= faceStopAfterDrop.Value)
                    {
                        over = true;
                    }
                }

                if (over)
                {
                    if (done == null)
                    {
                        done = new List<int>();
                    }
                    done.Add(pair.Key);
                    // The laughing is over: the handface has to rest before it can creep.
                    if (face.Body != null)
                    {
                        Creep.PrimaryEnded(face.Body, now);
                    }
                    continue;
                }

                // The noise it makes draws enemies, once a second, for as long as it goes on.
                if (now >= face.NextAlert)
                {
                    face.NextAlert = now + 1f;
                    Alert(face.Body);
                }
            }

            if (done != null)
            {
                foreach (int id in done)
                {
                    laughing.Remove(id);
                }
            }
        }

        // Draws the enemies within the alert range to where the body is (the game only acts on it on the host).
        internal static void Alert(PhysGrabObject body)
        {
            EnemyDirector director = EnemyDirector.instance;
            if (director != null && body != null && faceAlertRange.Value > 0f)
            {
                director.SetInvestigate(body.centerPoint, faceAlertRange.Value);
            }
        }

        // The shaking is physics, so it goes in FixedUpdate.
        internal static void FixedTick()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            shaken.Clear();
            foreach (Laughing face in laughing.Values)
            {
                Shake(face.Body);
            }
            Creep.FixedTick();
        }

        // One physics step of shaking: a random twist and a random push, scaled by the mass (so a heavier object is not shaken
        // less) and by the strength setting, and now and then a short spasm that hits harder. A body that both reasons ask to
        // shake in the same step is shaken once.
        internal static void Shake(PhysGrabObject body)
        {
            if (body == null || faceShake.Value <= 0f)
            {
                return;
            }
            Rigidbody rb = body.rb;
            if (rb == null || !shaken.Add(body.GetInstanceID()))
            {
                return;
            }

            float now = Time.time;
            Spasm spasm;
            int id = body.GetInstanceID();
            if (!spasms.TryGetValue(id, out spasm))
            {
                if (spasms.Count > 64)
                {
                    spasms.Clear();
                }
                spasm = new Spasm();
                spasms[id] = spasm;
            }
            if (now >= spasm.NextAt)
            {
                spasm.Until = now + Random.Range(0.08f, 0.2f);
                spasm.NextAt = now + Random.Range(0.35f, 1.1f);
            }

            float scale = faceShake.Value * (now < spasm.Until ? SpasmBoost : 1f) * Mathf.Max(1f, rb.mass);
            rb.AddTorque(Random.insideUnitSphere.normalized * (ShakeTorque * scale), ForceMode.Force);
            rb.AddForce(Random.insideUnitSphere * (ShakeForce * scale), ForceMode.Force);
        }

        // ---- every client: what the host announced

        internal static void OnEvent(EventData e)
        {
            // Only the host decides what happens; anything else is ignored.
            Player master = PhotonNetwork.MasterClient;
            if (master == null || e.Sender != master.ActorNumber)
            {
                return;
            }
            object[] content = e.CustomData as object[];
            if (content == null || content.Length < 9)
            {
                return;
            }

            PhotonView view = PhotonView.Find((int)content[0]);
            PhysGrabObject body = view == null ? null : view.GetComponent<PhysGrabObject>();
            Vector3 position = new Vector3((float)content[3], (float)content[4], (float)content[5]);
            float extra1 = content.Length > 9 ? (float)content[9] : 0f;
            float extra2 = content.Length > 10 ? (float)content[10] : 0f;
            Handle(body, (Quirk)(byte)content[1], (QuirkPhase)(byte)content[2], position, (float)content[6], (int)content[7], (int)content[8], extra1, extra2);
        }

        // The valuable can already be gone on a client by the time its explosion arrives, so what is needed to play it out is
        // the position; the body, when it is still there, only lets the sound follow it and names who to blame.
        internal static void Handle(PhysGrabObject body, Quirk quirk, QuirkPhase phase, Vector3 position, float size, int damage, int enemyDamage,
            float extra1, float extra2)
        {
            switch (quirk)
            {
                case Quirk.BananaBow:
                    if (phase == QuirkPhase.Start)
                    {
                        StartWindup(body, position);
                        if (bananaGlitch.Value)
                        {
                            ScreenGlitch.PlayIfHeldHere(body);
                        }
                    }
                    else if (phase == QuirkPhase.Warn)
                    {
                        GameAudio.PlayClip(UserAudio.Warning, body != null ? body.transform : null, position,
                            bananaWarnVolume.Value, bananaWarnFalloff.Value);
                    }
                    else if (phase == QuirkPhase.Boom)
                    {
                        Detonate(body, position, size, damage, enemyDamage);
                    }
                    break;
                case Quirk.Handface:
                    switch (phase)
                    {
                        case QuirkPhase.Start:
                            Laugh(body, position);
                            if (faceGlitch.Value)
                            {
                                ScreenGlitch.PlayIfHeldHere(body);
                            }
                            break;
                        case QuirkPhase.Twitch:
                            Creep.ClientTwitch(body, position, size);
                            break;
                        case QuirkPhase.Settle:
                            Creep.ClientSettle(body);
                            break;
                        case QuirkPhase.Launch:
                            Creep.ClientLaunch(body, position, size, damage, extra1, extra2);
                            break;
                    }
                    break;
                case Quirk.Horse:
                    if (phase == QuirkPhase.Start)
                    {
                        Haunts.ClientHorse(body, position);
                    }
                    break;
                case Quirk.Piano:
                    if (phase == QuirkPhase.Start)
                    {
                        Haunts.ClientPianoStart(body, position);
                    }
                    else if (phase == QuirkPhase.Settle)
                    {
                        Haunts.ClientPianoStop(body);
                    }
                    break;
            }
        }

        // The handface's laugh, at the loudness and reach set on this machine.
        internal static void Laugh(PhysGrabObject body, Vector3 position)
        {
            GameAudio.PlayClip(UserAudio.Laugh, body != null ? body.transform : null, position, faceLaughVolume.Value, faceLaughFalloff.Value);
        }

        private static void StartWindup(PhysGrabObject body, Vector3 position)
        {
            GameAudio.Fuse sound = GameAudio.StartFuse(body != null ? body.transform : null, position);
            if (sound == null || body == null)
            {
                return;
            }
            int id = body.GetInstanceID();
            Burning old;
            if (burning.TryGetValue(id, out old))
            {
                old.Sound.Stop();
            }
            burning[id] = new Burning { Body = body, Sound = sound, StartedAt = Time.time };
        }

        private static void Detonate(PhysGrabObject body, Vector3 position, float size, int damage, int enemyDamage)
        {
            if (body != null)
            {
                int id = body.GetInstanceID();
                Burning fuse;
                if (burning.TryGetValue(id, out fuse))
                {
                    fuse.Sound.Stop();
                    burning.Remove(id);
                }
            }
            Explosions.Spawn(body, position, size, damage, enemyDamage);
        }

        // Every frame, on every machine: keeps the fuses burning and the creeping sound fading, and looks for the flung hand.
        internal static void ClientTick()
        {
            if (burning.Count > 0)
            {
                List<int> done = null;
                foreach (KeyValuePair<int, Burning> pair in burning)
                {
                    Burning fuse = pair.Value;
                    // A fuse whose banana bow vanished without going off (or that has burned far too long) is put out.
                    if (fuse.Body == null || Time.time - fuse.StartedAt > bananaWindup.Value + WarningSeconds + 5f)
                    {
                        fuse.Sound.Stop();
                        if (done == null)
                        {
                            done = new List<int>();
                        }
                        done.Add(pair.Key);
                        continue;
                    }
                    fuse.Sound.Tick();
                }
                if (done != null)
                {
                    foreach (int id in done)
                    {
                        burning.Remove(id);
                    }
                }
            }

            Creep.ClientTick();
            Haunts.ClientTick();
        }
    }
}
