using UnityEngine;
using Random = UnityEngine.Random;

namespace UsableValuables
{
    // On the dish sponge's prefab: while the sponge is moved (carried, shaken, thrown, dropped) it blows soap bubbles and drips, more
    // the faster it goes. The first time somebody picks it up it also plays its bubbling sound (bubbles.mp3) with a burst of bubbles for
    // as long as that sound lasts, and then stops. It works from where the sponge is, frame by frame, so it is the same on every
    // machine that has the mod, with nothing sent over the network. It stays quiet in the cart, which moves it around for you.
    internal sealed class SpongeFx : MonoBehaviour
    {
        // A sponge that is turned moves its edges even where it stays: about this far from its middle.
        private const float EdgeRadius = 0.06f;
        private const int MaxBubblesPerFrame = 12;
        private const float MinDripGap = 0.18f;

        // Bubbles per second during the burst of the first pick-up, at a BubbleAmount of 1.
        private const float BurstRate = 90f;

        private static AudioClip[] drips;
        private static AudioClip pop;
        private static Texture2D bubbleTexture;
        private static Texture2D dropTexture;
        private static Material bubbleMaterial;
        private static Material dropMaterial;
        private static bool materialsTried;

        private PhysGrabObject body;
        private PhysGrabObjectImpactDetector impact;
        private ParticleSystem bubbles;
        private ParticleSystem drops;

        private bool hasLast;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private float speed;

        private float bubbleDebt;
        private float dripDistance;
        private float nextDripAfter = 1.5f;
        private float lastDripAt = -10f;
        private int lastDrip = -1;

        private bool firstGrabDone;
        private float burstStart = -1f;
        private float burstLength;
        private float burstDebt;

        private void Awake()
        {
            body = GetComponent<PhysGrabObject>();
            impact = GetComponent<PhysGrabObjectImpactDetector>();
            EnsureAssets();
            Transform model = transform.Find("Sponge Model");
            int layer = model != null ? model.gameObject.layer : gameObject.layer;
            bubbles = MakeSystem("Sponge Bubbles", bubbleMaterial, -0.05f, layer, true);
            drops = MakeSystem("Sponge Drops", dropMaterial, 1f, layer, false);
        }

        // The clips and sprites are made once, on the first sponge that wakes up.
        private static void EnsureAssets()
        {
            if (drips == null)
            {
                drips = SpongeAssets.BuildDrips();
                pop = SpongeAssets.BuildPop();
            }
            if (materialsTried)
            {
                return;
            }
            materialsTried = true;
            bubbleTexture = SpongeAssets.BuildBubbleTexture();
            dropTexture = SpongeAssets.BuildDropTexture();
            string shader;
            bubbleMaterial = SpongeAssets.BuildParticleMaterial(bubbleTexture, "Sponge Bubble", out shader);
            dropMaterial = SpongeAssets.BuildParticleMaterial(dropTexture, "Sponge Drop", out shader);
            if (bubbleMaterial == null)
            {
                Plugin.Log.LogWarning("None of the shaders the dish sponge's bubbles need is in the game: it will drip without bubbles.");
            }
            else
            {
                Plugin.Log.LogInfo("The dish sponge's bubbles use the shader \"" + shader + "\".");
            }
        }

        // A particle system that only shows what is emitted into it by hand, in world space, so the bubbles stay where they were
        // blown while the sponge moves on.
        private ParticleSystem MakeSystem(string name, Material material, float gravity, int layer, bool fadeAndGrow)
        {
            if (material == null)
            {
                return null;
            }
            GameObject holder = new GameObject(name) { layer = layer };
            holder.transform.SetParent(transform, false);
            ParticleSystem system = holder.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            main.startLifetime = 2f;
            main.startSize = 0.03f;
            main.maxParticles = 600;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            if (fadeAndGrow)
            {
                ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
                colour.enabled = true;
                Gradient fade = new Gradient();
                fade.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                colour.color = new ParticleSystem.MinMaxGradient(fade);

                ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.4f), new Keyframe(0.15f, 1f), new Keyframe(0.85f, 1.05f), new Keyframe(1f, 1.3f)));
            }

            ParticleSystemRenderer particleRenderer = holder.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = material;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            system.Play();
            return system;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Track(dt);
            if (!Plugin.Enabled.Value)
            {
                return;
            }
            bool inCart = impact != null && impact.inCart;

            FirstGrab(dt, inCart);

            if (inCart || speed < Sponge.MinSpeed)
            {
                return;
            }

            // 0 at the threshold, 1 at four metres per second above it.
            float level = Mathf.Clamp01((speed - Sponge.MinSpeed) / 4f);

            bubbleDebt += Sponge.BubbleAmount * (7f + 40f * level) * dt;
            int count = Mathf.Min((int)bubbleDebt, MaxBubblesPerFrame);
            bubbleDebt -= Mathf.Floor(bubbleDebt);
            if (count > 0)
            {
                Blow(count, false);
            }

            dripDistance += speed * dt;
            if (dripDistance >= nextDripAfter && Time.time - lastDripAt >= MinDripGap)
            {
                Drip();
                dripDistance = 0f;
                nextDripAfter = Random.Range(1.5f, 3.2f);
            }
        }

        // How fast the sponge is going, smoothed: what it moved since the last frame, plus what its turning moves its edges.
        private void Track(float dt)
        {
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            if (!hasLast)
            {
                hasLast = true;
                lastPosition = position;
                lastRotation = rotation;
                return;
            }
            Vector3 step = position - lastPosition;
            float turned = Quaternion.Angle(rotation, lastRotation) * Mathf.Deg2Rad;
            lastPosition = position;
            lastRotation = rotation;

            // Put somewhere (spawned, sent to the truck): not a movement.
            if (step.sqrMagnitude > 4f)
            {
                return;
            }
            float instant = step.magnitude / dt + turned / dt * EdgeRadius;
            speed = Mathf.Lerp(speed, instant, 1f - Mathf.Exp(-10f * dt));
        }

        // The first time somebody holds this sponge: its bubbling sound plays, with a burst of bubbles that thins out as the sound
        // does, and when the sound is over it stops (the bubbles and drips of moving it go on as usual).
        private void FirstGrab(float dt, bool inCart)
        {
            if (!firstGrabDone)
            {
                if (body == null || !body.grabbed)
                {
                    return;
                }
                firstGrabDone = true;
                AudioClip clip = UserAudio.Bubbles;
                burstLength = clip != null ? Mathf.Clamp(clip.length, 1f, 8f) : 2f;
                burstStart = Time.time;
                if (clip != null)
                {
                    GameAudio.PlayClip(clip, transform, transform.position, Sponge.FirstGrabVolume, Sponge.FirstGrabFalloff);
                }
            }

            if (burstStart < 0f || inCart)
            {
                return;
            }
            float t = (Time.time - burstStart) / burstLength;
            if (t >= 1f)
            {
                burstStart = -1f;
                return;
            }

            // Full for the first 60% of the sound, then fading out.
            float fade = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
            burstDebt += BurstRate * Sponge.BubbleAmount * fade * dt;
            int count = Mathf.Min((int)burstDebt, MaxBubblesPerFrame * 2);
            burstDebt -= Mathf.Floor(burstDebt);
            if (count > 0)
            {
                Blow(count, true);
            }
        }

        private void Blow(int count, bool burst)
        {
            if (bubbles != null)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 local = burst
                        ? new Vector3(Random.Range(-0.09f, 0.09f), Random.Range(0.02f, 0.12f), Random.Range(-0.06f, 0.06f))
                        : new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.02f, 0.09f), Random.Range(-0.05f, 0.05f));

                    // Mostly small ones, some big ones.
                    float size = Mathf.Lerp(0.025f, burst ? 0.09f : 0.07f, Random.value * Random.value);
                    if (Random.value < 0.08f)
                    {
                        size *= 1.6f;
                    }

                    ParticleSystem.EmitParams bubble = new ParticleSystem.EmitParams
                    {
                        position = transform.TransformPoint(local),
                        velocity = Random.onUnitSphere * (burst ? 0.35f : 0.15f) + Vector3.up * (burst ? 0.4f : 0.12f),
                        startSize = size,
                        startLifetime = Random.Range(1.5f, 3f),
                        startColor = new Color(0.9f, 0.97f, 1f, Random.Range(0.6f, 0.95f)),
                    };
                    bubbles.Emit(bubble, 1);
                }
            }

            // Now and then one of them bursts (the sound of the first pick-up has its own).
            if (!burst && pop != null && Random.value < 0.04f * count)
            {
                GameAudio.PlayClip(pop, transform, transform.position, Sponge.DripVolume * 0.6f, Sponge.DripFalloff);
            }
        }

        private void Drip()
        {
            lastDripAt = Time.time;
            float volume = Sponge.DripVolume * Random.Range(0.65f, 1f);
            AudioClip custom = UserAudio.Drip;
            if (custom != null)
            {
                GameAudio.PlayClip(custom, transform, transform.position, volume, Sponge.DripFalloff);
            }
            else if (drips != null && drips.Length > 0)
            {
                int index = Random.Range(0, drips.Length);
                if (index == lastDrip)
                {
                    index = (index + 1) % drips.Length;
                }
                lastDrip = index;
                GameAudio.PlayClip(drips[index], transform, transform.position, volume, Sponge.DripFalloff);
            }

            if (drops != null)
            {
                ParticleSystem.EmitParams drop = new ParticleSystem.EmitParams
                {
                    position = transform.TransformPoint(new Vector3(Random.Range(-0.06f, 0.06f), 0.01f, Random.Range(-0.04f, 0.04f))),
                    velocity = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0f, 0.3f), Random.Range(-0.25f, 0.25f)),
                    startSize = Random.Range(0.012f, 0.02f),
                    startLifetime = 0.7f,
                    startColor = new Color(0.72f, 0.88f, 1f, 0.9f),
                };
                drops.Emit(drop, 1);
            }
        }
    }
}
