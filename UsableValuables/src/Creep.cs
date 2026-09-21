using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UsableValuables
{
    // The handface's second behaviour, for when nobody holds it (the first one, laughing and shaking, is for when somebody does):
    //
    //  - while a player moves near it, it slowly turns to face them, faster and faster the closer it gets to the throw, and a
    //    creeping sound plays, fading in when the movement starts and out when it stops, and the noise draws enemies;
    //  - if the player keeps moving until the sound is over, the handface is flung at the nearest player, hurts them on impact,
    //    puts them in ragdoll and laughs. That happens once: after it the handface does nothing any more, in either behaviour.
    //
    // It does nothing while somebody holds it or while it is in the cart. Between one behaviour and the other there is a rest
    // (a few seconds after the laughing before it can creep, and after the creeping before it can laugh). The host decides (who
    // moves, when the sound is over, who is the nearest); everybody plays the sound; and each machine checks for the impact
    // against its own player, because in the game only the owner of a player can hurt them.
    internal static class Creep
    {
        // Nobody moving near it for this long ends the creeping (the game's own "moving" flag already lingers a moment).
        private const float Grace = 0.35f;
        private const float FadeIn = 0.6f;
        private const float FadeOut = 0.8f;
        // How long after the throw the hand can hurt somebody, and how close to them it has to get (from their eyes or their middle).
        private const float FlightSeconds = 2f;
        private const float HitRadius = 1.2f;
        // If the sound file could not be loaded, the creeping lasts this long instead.
        private const float FallbackSeconds = 8f;
        // Who is moving is asked this often, not every frame.
        private const float CheckEvery = 0.2f;

        // The turning: the fastest it turns, in degrees per second, grows from TurnStart (as it begins to creep) to TurnEnd (just
        // before the throw), following the square of how far the sound has got, so it is slow for a long while and then quick. It
        // eases into the angle rather than snapping to it (TurnGain, per second). It must not move from where it lies or tip over:
        // a spring pulls it back to how it was lying, apart from its turn (TiltSpring, an angular acceleration in radians per second
        // squared per radian of tilt, so it does not depend on the mass), another keeps it where it was (PlaceSpring), and it
        // cannot slide.
        private const float TurnStart = 15f;
        private const float TurnEnd = 240f;
        private const float TurnGain = 3f;
        private const float TiltSpring = 30f;
        private const float TiltDamping = 6f;
        private const float PlaceSpring = 40f;
        private const float PlaceDamping = 0.5f;

        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<float> range;
        private static ConfigEntry<float> turn;
        private static ConfigEntry<float> frontOffset;
        private static ConfigEntry<float> launchSpeed;
        private static ConfigEntry<int> launchDamage;
        private static ConfigEntry<float> ragdollSeconds;
        private static ConfigEntry<float> ragdollForce;
        private static ConfigEntry<float> behaviourCooldown;
        private static ConfigEntry<float> volume;
        private static ConfigEntry<float> falloff;

        private enum Phase
        {
            Idle,
            Twitching,
            Flying,
        }

        // A handface (host only), keyed by the physics body's instance id.
        private sealed class Hand
        {
            public PhysGrabObject Body;
            public PhysGrabObjectImpactDetector Impact;
            public Phase Phase;
            public bool Done;               // it has been flung once: it does nothing any more
            public float PrimaryBlockedUntil;      // the rest after the creeping before it can laugh
            public float SecondaryBlockedUntil;    // the rest after the laughing before it can creep
            public float Progress;      // seconds of the sound played so far: it only runs while somebody keeps moving
            public float StillFor;      // seconds since nobody moved near it
            public float NextCheck;
            public bool MovingNow;
            public PlayerAvatar Target; // the nearest player, whom it turns to face
            public float NextAlert;
            public float Until;         // Flying: when the flight ends
            public Vector3 RestPosition;    // where and how it was lying when it started to creep
            public Quaternion RestRotation;
        }

        // On every machine: the creeping sound that is playing, and its fade.
        private sealed class Playback
        {
            public PhysGrabObject Body;
            public AudioSource Source;
            public float Level;         // 0-1, the fade
            public bool Fading;
        }

        // On every machine: a hand that was just flung, to see whether it hits the local player.
        private sealed class Flight
        {
            public PhysGrabObject Body;
            public Vector3 LastPosition;
            public Vector3 PreviousPosition;    // one frame earlier, to tell which way it is flying
            public float Until;
            public float Radius;
            public int Damage;
            public float RagdollSeconds;
            public float RagdollForce;
            public bool Hit;
        }

        private static readonly Dictionary<int, Hand> hands = new Dictionary<int, Hand>();
        private static readonly Dictionary<int, Playback> playbacks = new Dictionary<int, Playback>();
        private static readonly List<Flight> flights = new List<Flight>();

        internal static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Handface", "TwitchEnabled", true,
                "When nobody holds the handface (and it is not in the cart) it turns slowly to face a player who moves near it, and makes a "
                + "creeping sound; if that player keeps moving until the sound is over, it is flung at the nearest player, once. "
                + "Only the host's setting is used.");
            range = config.Bind("Handface", "TwitchRange", 5f,
                new ConfigDescription("Metres around the handface within which a moving player sets it off. Only the host's setting is used.",
                    new AcceptableValueRange<float>(1f, 30f)));
            turn = config.Bind("Handface", "TwitchTurn", 1f,
                new ConfigDescription("How fast the handface turns to face the player while it creeps: 1 = slowly at first and quicker and quicker "
                    + "until the throw (from 15 to 240 degrees per second); 0 = it does not turn. Only the host's setting is used.",
                    new AcceptableValueRange<float>(0f, 3f)));
            frontOffset = config.Bind("Handface", "FrontOffsetDegrees", 0f,
                new ConfigDescription("Which side of the handface is its front, the side that turns to face the player: 0 = the way the model "
                    + "faces, 90, 180 or 270 = a quarter, half or three quarters of a turn further round. Only the host's setting is used.",
                    new AcceptableValueRange<float>(0f, 360f)));
            launchSpeed = config.Bind("Handface", "LaunchSpeed", 14f,
                new ConfigDescription("Speed, in metres per second, at which the handface is flung at the player. Only the host's setting is used.",
                    new AcceptableValueRange<float>(2f, 40f)));
            launchDamage = config.Bind("Handface", "LaunchDamage", 20,
                new ConfigDescription("Damage a player takes when the flung handface hits them. Only the host's setting is used.",
                    new AcceptableValueRange<int>(0, 100)));
            ragdollSeconds = config.Bind("Handface", "RagdollSeconds", 2f,
                new ConfigDescription("Seconds the player the flung handface hits stays in ragdoll (the game's tumble). 0 = no ragdoll. "
                    + "Only the host's setting is used.", new AcceptableValueRange<float>(0f, 10f)));
            ragdollForce = config.Bind("Handface", "RagdollForce", 10f,
                new ConfigDescription("How hard the hit shoves the player, along the way the hand was flying (an impulse: 10 is a solid knock, "
                    + "0 = they just fall). Only the host's setting is used.", new AcceptableValueRange<float>(0f, 50f)));
            behaviourCooldown = config.Bind("Handface", "BehaviourCooldownSeconds", 4f,
                new ConfigDescription("The rest between one of the handface's behaviours and the other: after it stops laughing it cannot start "
                    + "creeping, and after it stops creeping it cannot start laughing, for this many seconds. Only the host's setting is used.",
                    new AcceptableValueRange<float>(0f, 60f)));
            volume = config.Bind("Handface", "TwitchVolume", 1f,
                new ConfigDescription("How loud the creeping sound is on your machine (1 = as loud as a sound can be played).",
                    new AcceptableValueRange<float>(0f, 1f)));
            falloff = config.Bind("Handface", "TwitchFalloff", 1.5f,
                new ConfigDescription("How far the creeping sound carries on your machine (1 = like the game's own sounds).",
                    new AcceptableValueRange<float>(1f, 5f)));
        }

        // ---- host

        private static Hand GetHand(PhysGrabObject body)
        {
            int id = body.GetInstanceID();
            Hand hand;
            if (!hands.TryGetValue(id, out hand))
            {
                hand = new Hand { Body = body, Impact = body.GetComponent<PhysGrabObjectImpactDetector>() };
                hands[id] = hand;
            }
            return hand;
        }

        internal static void Register(PhysGrabObject body)
        {
            GetHand(body);
        }

        // May the handface start laughing (the first behaviour) now? Not once it has been flung, not while it is creeping, and not
        // in the rest after the creeping.
        internal static bool PrimaryAllowed(PhysGrabObject body, float now)
        {
            Hand hand = GetHand(body);
            return !hand.Done && hand.Phase == Phase.Idle && now >= hand.PrimaryBlockedUntil;
        }

        // The laughing is over: the handface has to rest before it can creep.
        internal static void PrimaryEnded(PhysGrabObject body, float now)
        {
            GetHand(body).SecondaryBlockedUntil = now + behaviourCooldown.Value;
        }

        private static float Length
        {
            get { return UserAudio.Twitch != null ? UserAudio.Twitch.length : FallbackSeconds; }
        }

        internal static void Tick(float now)
        {
            if (hands.Count == 0)
            {
                return;
            }

            float dt = Time.deltaTime;
            List<int> gone = null;
            foreach (KeyValuePair<int, Hand> pair in hands)
            {
                Hand hand = pair.Value;
                PhysGrabObject body = hand.Body;
                if (body == null || body.dead)
                {
                    if (gone == null)
                    {
                        gone = new List<int>();
                    }
                    gone.Add(pair.Key);
                    continue;
                }

                // A handface that has been flung is done for good (it stays in the list so it is not taken for a new one).
                if (hand.Done && hand.Phase == Phase.Idle)
                {
                    continue;
                }

                // Nothing happens while somebody holds it or while it is in the cart.
                bool blocked = !enabled.Value || body.grabbed || (hand.Impact != null && hand.Impact.inCart);

                switch (hand.Phase)
                {
                    case Phase.Idle:
                        if (!blocked && now >= hand.SecondaryBlockedUntil && !Mischief.IsLaughing(body) && MovingNear(hand, now))
                        {
                            Begin(hand, now);
                        }
                        break;

                    case Phase.Twitching:
                        if (blocked)
                        {
                            Settle(hand, now);
                            break;
                        }
                        if (MovingNear(hand, now))
                        {
                            hand.StillFor = 0f;
                            hand.Progress += dt;
                        }
                        else
                        {
                            hand.StillFor += dt;
                        }
                        if (hand.StillFor > Grace)
                        {
                            Settle(hand, now);
                            break;
                        }
                        if (now >= hand.NextAlert)
                        {
                            hand.NextAlert = now + 1f;
                            Mischief.Alert(body);
                        }
                        if (hand.Progress >= Length)
                        {
                            Launch(hand, now);
                        }
                        break;

                    case Phase.Flying:
                        if (now >= hand.Until)
                        {
                            hand.Phase = Phase.Idle;
                        }
                        break;
                }
            }

            if (gone != null)
            {
                foreach (int id in gone)
                {
                    hands.Remove(id);
                }
            }
        }

        // Is a player within range moving right now? Asked a few times a second, and the answer kept in between. The nearest player
        // (whom it turns to face, and at whom it will be flung) is looked up at the same time.
        private static bool MovingNear(Hand hand, float now)
        {
            if (now < hand.NextCheck)
            {
                return hand.MovingNow;
            }
            hand.NextCheck = now + CheckEvery;
            hand.MovingNow = false;
            foreach (PlayerAvatar player in SemiFunc.PlayerGetAllPlayerAvatarWithinRange(range.Value, hand.Body.centerPoint))
            {
                if (player != null && Refs.IsMoving(player))
                {
                    hand.MovingNow = true;
                    break;
                }
            }
            hand.Target = SemiFunc.PlayerGetNearestPlayerAvatarWithinRange(range.Value * 2f, hand.Body.centerPoint);
            return hand.MovingNow;
        }

        private static void Begin(Hand hand, float now)
        {
            Plugin.Log.LogDebug("A handface started creeping.");
            hand.Phase = Phase.Twitching;
            hand.Progress = 0f;
            hand.StillFor = 0f;
            hand.NextAlert = now;
            if (hand.Body.rb != null)
            {
                hand.RestPosition = hand.Body.rb.position;
                hand.RestRotation = hand.Body.rb.rotation;
            }
            // The range goes along so every machine knows who is close enough to get the screen glitch.
            Net.AnnounceQuirk(hand.Body, Quirk.Handface, QuirkPhase.Twitch, range.Value);
        }

        // The movement stopped (or it was picked up, or put in the cart): the sound fades out and it starts over next time, and the
        // handface has to rest before it can laugh.
        private static void Settle(Hand hand, float now)
        {
            hand.Phase = Phase.Idle;
            hand.Progress = 0f;
            hand.StillFor = 0f;
            hand.PrimaryBlockedUntil = now + behaviourCooldown.Value;
            Net.AnnounceQuirk(hand.Body, Quirk.Handface, QuirkPhase.Settle);
        }

        private static void Launch(Hand hand, float now)
        {
            PhysGrabObject body = hand.Body;
            PlayerAvatar target = SemiFunc.PlayerGetNearestPlayerAvatarWithinRange(range.Value * 2f, body.centerPoint);
            Rigidbody rb = body.rb;
            if (target == null || rb == null)
            {
                Settle(hand, now);
                return;
            }

            Plugin.Log.LogDebug("A handface was flung at a player; it is done now.");
            Vector3 aim = target.PlayerVisionTarget.VisionTransform.position + Vector3.down * 0.25f;
            Vector3 direction = aim - body.centerPoint;
            direction = direction.sqrMagnitude < 0.01f ? Vector3.up : direction.normalized;

            // It flies straight for a moment instead of dropping, and it is not broken by its own throw.
            body.OverrideZeroGravity(0.6f);
            body.OverrideIndestructible(FlightSeconds + 1f);
            rb.velocity = direction * launchSpeed.Value;
            rb.angularVelocity = Random.insideUnitSphere * 12f;

            hand.Phase = Phase.Flying;
            hand.Until = now + FlightSeconds;
            hand.Progress = 0f;
            // Once is all it gets: from now on it is a plain valuable again.
            hand.Done = true;
            Mischief.Alert(body);
            Net.AnnounceQuirk(body, Quirk.Handface, QuirkPhase.Launch, HitRadius, launchDamage.Value, 0, ragdollSeconds.Value, ragdollForce.Value);
        }

        // The turning while it creeps (host, one physics step).
        internal static void FixedTick()
        {
            if (turn.Value <= 0f)
            {
                return;
            }
            foreach (Hand hand in hands.Values)
            {
                if (hand.Phase == Phase.Twitching)
                {
                    Turn(hand);
                }
            }
        }

        private static void Turn(Hand hand)
        {
            Rigidbody rb = hand.Body != null ? hand.Body.rb : null;
            if (rb == null)
            {
                return;
            }

            // If something else moves the hand a lot (somebody shoves it), it is where it lies now.
            if (Vector3.Distance(rb.position, hand.RestPosition) > 0.6f)
            {
                hand.RestPosition = rb.position;
            }

            // The tilt away from how it was lying, leaving out the turning about the vertical (that is what is being done on purpose):
            // the rotation since it started is split into that turning and the rest, the rest being the tilt.
            Quaternion tiltRotation = TiltOf(rb.rotation * Quaternion.Inverse(hand.RestRotation));
            float angle;
            Vector3 axis;
            tiltRotation.ToAngleAxis(out angle, out axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }
            if (Mathf.Abs(angle) > 45f)
            {
                // Tipped by something else: this is how it lies now.
                hand.RestRotation = rb.rotation;
                angle = 0f;
            }
            Vector3 tilt = axis * (angle * Mathf.Deg2Rad);

            // How fast it may turn: slowly to begin with, and the more the sound has got on the quicker.
            float progress = Mathf.Clamp01(hand.Progress / Length);
            float fastest = Mathf.Lerp(TurnStart, TurnEnd, progress * progress) * turn.Value;

            // Which way it faces (its front, flattened to the ground) and which way the player is; if either is straight up or down
            // there is nothing to turn towards.
            float yawRate = 0f;
            PlayerAvatar target = hand.Target;
            if (target != null && target.PlayerVisionTarget != null && target.PlayerVisionTarget.VisionTransform != null)
            {
                Vector3 face = rb.rotation * (Quaternion.Euler(0f, frontOffset.Value, 0f) * Vector3.forward);
                face.y = 0f;
                Vector3 toward = target.PlayerVisionTarget.VisionTransform.position - rb.position;
                toward.y = 0f;
                if (face.sqrMagnitude > 0.04f && toward.sqrMagnitude > 0.04f)
                {
                    float error = Vector3.SignedAngle(face, toward, Vector3.up);
                    yawRate = Mathf.Clamp(error * TurnGain, -fastest, fastest) * Mathf.Deg2Rad;
                }
            }

            // Turn about the vertical at that rate, and pull the tilt back to how it was lying.
            Vector3 spin = rb.angularVelocity;
            rb.AddTorque(-tilt * TiltSpring - new Vector3(spin.x, 0f, spin.z) * TiltDamping, ForceMode.Acceleration);
            rb.angularVelocity = new Vector3(spin.x, yawRate, spin.z);

            // Where it lies stays put: a spring on its position (sideways only, gravity and the floor do the rest) and no sliding.
            Vector3 offset = rb.position - hand.RestPosition;
            offset.y = 0f;
            rb.AddForce(-offset * PlaceSpring, ForceMode.Acceleration);
            Vector3 velocity = rb.velocity;
            rb.velocity = new Vector3(velocity.x * PlaceDamping, velocity.y, velocity.z * PlaceDamping);
        }

        // Splits a rotation (as it is in the world) into a turn about the vertical and the rest, and returns the rest.
        private static Quaternion TiltOf(Quaternion rotation)
        {
            Vector3 along = Vector3.Project(new Vector3(rotation.x, rotation.y, rotation.z), Vector3.up);
            float length = Mathf.Sqrt(along.sqrMagnitude + rotation.w * rotation.w);
            if (length < 0.00001f)
            {
                // A half turn end over end: nothing sensible to split (the caller will take it for a shove).
                return rotation;
            }
            Quaternion twist = new Quaternion(along.x / length, along.y / length, along.z / length, rotation.w / length);
            return rotation * Quaternion.Inverse(twist);
        }

        // ---- every machine

        internal static void ClientTwitch(PhysGrabObject body, Vector3 position, float range)
        {
            // Whoever is within range gets the screen glitch the game shows when a trap goes off.
            if (Mischief.FaceGlitch)
            {
                ScreenGlitch.PlayIfNear(position, range);
            }

            int key = body != null ? body.GetInstanceID() : 0;
            Stop(key);

            // Starts all but silent and is faded in by ClientTick.
            AudioSource source = GameAudio.PlayClip(UserAudio.Twitch, body != null ? body.transform : null, position, 0.001f, falloff.Value);
            if (source != null)
            {
                playbacks[key] = new Playback { Body = body, Source = source };
            }
        }

        internal static void ClientSettle(PhysGrabObject body)
        {
            Playback playback;
            if (playbacks.TryGetValue(body != null ? body.GetInstanceID() : 0, out playback))
            {
                playback.Fading = true;
            }
        }

        internal static void ClientLaunch(PhysGrabObject body, Vector3 position, float radius, int damage, float ragdollSeconds, float ragdollForce)
        {
            Stop(body != null ? body.GetInstanceID() : 0);
            Mischief.Laugh(body, position);
            flights.Add(new Flight
            {
                Body = body,
                LastPosition = position,
                PreviousPosition = position,
                Until = Time.time + FlightSeconds,
                Radius = radius,
                Damage = damage,
                RagdollSeconds = ragdollSeconds,
                RagdollForce = ragdollForce,
            });
        }

        private static void Stop(int key)
        {
            Playback playback;
            if (playbacks.TryGetValue(key, out playback))
            {
                if (playback.Source != null)
                {
                    playback.Source.Stop();
                    Object.Destroy(playback.Source.gameObject);
                }
                playbacks.Remove(key);
            }
        }

        internal static void ClientTick()
        {
            float dt = Time.deltaTime;

            if (playbacks.Count > 0)
            {
                List<int> done = null;
                foreach (KeyValuePair<int, Playback> pair in playbacks)
                {
                    Playback playback = pair.Value;
                    if (playback.Source == null)
                    {
                        if (done == null)
                        {
                            done = new List<int>();
                        }
                        done.Add(pair.Key);
                        continue;
                    }

                    // A hand that is gone takes its sound with it.
                    if (playback.Body == null)
                    {
                        playback.Fading = true;
                    }
                    playback.Level = Mathf.MoveTowards(playback.Level, playback.Fading ? 0f : 1f, dt / (playback.Fading ? FadeOut : FadeIn));
                    GameAudio.SetVolume(playback.Source, playback.Level * volume.Value);
                    if (playback.Fading && playback.Level <= 0.001f)
                    {
                        if (done == null)
                        {
                            done = new List<int>();
                        }
                        done.Add(pair.Key);
                    }
                }
                if (done != null)
                {
                    foreach (int key in done)
                    {
                        Stop(key);
                    }
                }
            }

            if (flights.Count > 0)
            {
                for (int i = flights.Count - 1; i >= 0; i--)
                {
                    Flight flight = flights[i];
                    if (flight.Hit || Time.time >= flight.Until)
                    {
                        flights.RemoveAt(i);
                        continue;
                    }
                    flight.PreviousPosition = flight.LastPosition;
                    if (flight.Body != null)
                    {
                        flight.LastPosition = flight.Body.centerPoint;
                    }
                    CheckHit(flight);
                }
            }
        }

        // Only this machine's own player can be hurt here: in the game a player is hurt by the machine that owns them.
        private static void CheckHit(Flight flight)
        {
            PlayerController controller = PlayerController.instance;
            PlayerAvatar player = controller == null ? null : controller.playerAvatarScript;
            if (player == null || player.PlayerVisionTarget == null || player.PlayerVisionTarget.VisionTransform == null)
            {
                return;
            }

            Vector3 eyes = player.PlayerVisionTarget.VisionTransform.position;
            Vector3 middle = (eyes + controller.transform.position) * 0.5f;
            float distance = Mathf.Min(Vector3.Distance(flight.LastPosition, eyes), Vector3.Distance(flight.LastPosition, middle));
            if (distance <= flight.Radius)
            {
                flight.Hit = true;
                player.playerHealth.Hurt(flight.Damage, true);
                KnockDown(player, flight, eyes);
            }
        }

        // Puts the player in ragdoll the way the game's own hurt colliders and enemies do: ask for the tumble, keep them in it for a
        // while, and shove them the way the hand was flying (a little upwards, so they are tossed rather than dragged). Like the
        // damage, this is asked on the machine of the player it is done to.
        private static void KnockDown(PlayerAvatar player, Flight flight, Vector3 eyes)
        {
            if (flight.RagdollSeconds <= 0f || Refs.Health(player.playerHealth) <= 0)
            {
                return;
            }
            PlayerTumble tumble = Refs.Tumble(player);
            if (tumble == null)
            {
                return;
            }

            Vector3 direction = flight.LastPosition - flight.PreviousPosition;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = eyes - flight.LastPosition;
            }
            direction = (direction.normalized + Vector3.up * 0.3f).normalized;

            tumble.TumbleRequest(true, false);
            tumble.TumbleOverrideTime(flight.RagdollSeconds);
            if (flight.RagdollForce > 0f)
            {
                tumble.TumbleForce(direction * flight.RagdollForce);
            }
        }
    }
}
