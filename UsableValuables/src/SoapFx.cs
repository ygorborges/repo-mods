using UnityEngine;
using Random = UnityEngine.Random;

namespace UsableValuables
{
    // On the soap's prefab: what makes it behave like nothing else in the game.
    //
    //  - It is slippery and bouncy (SoapAssets.BuildPhysicMaterial, put on its collider when it is built), so it skids and bounces
    //    off things instead of just stopping.
    //  - While it rests on the ground - not held, not in the cart, not already moving much - it gives itself a little hop now and
    //    then, the way the game's own rubber duck toy fidgets when nobody is touching it. The host decides and gives the push; the
    //    game's own networking for the valuable's position (the same one that carries the handface's turn) carries the result to
    //    everybody, so this needs no RPC of its own.
    //  - Whatever touches it - a player or an enemy - finds its footing on it, and an enemy that runs into it is knocked down for a
    //    moment (the game's own stunned state, the one a weapon's hit gives).
    internal sealed class SoapFx : MonoBehaviour
    {
        // Below this speed it counts as "resting" and can hop; above it, it is already skidding or flying and is left alone.
        private const float RestSpeed = 0.35f;

        private PhysGrabObject body;
        private PhysGrabObjectImpactDetector impact;
        private Rigidbody rb;
        private float nextHopAt = -1f;

        private void Awake()
        {
            body = GetComponent<PhysGrabObject>();
            impact = GetComponent<PhysGrabObjectImpactDetector>();
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (!Plugin.Enabled.Value || !Soap.Ready || !SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            if (body == null || rb == null || body.grabbed || (impact != null && impact.inCart))
            {
                nextHopAt = -1f;      // held or put away: forget the countdown, so it doesn't hop the instant it is let go of or dropped
                return;
            }
            if (Soap.HopStrength <= 0f || rb.velocity.magnitude > RestSpeed)
            {
                return;
            }

            if (nextHopAt < 0f)
            {
                nextHopAt = Time.time + Random.Range(Soap.HopMinInterval, Soap.HopMaxInterval);
                return;
            }
            if (Time.time < nextHopAt)
            {
                return;
            }
            nextHopAt = Time.time + Random.Range(Soap.HopMinInterval, Soap.HopMaxInterval);

            float strength = Soap.HopStrength;
            rb.AddForce(Vector3.up * (1.1f * strength), ForceMode.Impulse);
            rb.AddForce(new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (0.35f * strength), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * (0.6f * strength), ForceMode.Impulse);
        }

        // A physical touch, not a trigger: the soap has a solid collider, so this fires the moment it actually bumps into something.
        private void OnCollisionEnter(Collision collision)
        {
            if (!Plugin.Enabled.Value || !Soap.Ready || Soap.StunSeconds <= 0f || !SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            EnemyRigidbody enemyRigidbody = collision.collider == null ? null : collision.collider.GetComponentInParent<EnemyRigidbody>();
            Enemy enemy = enemyRigidbody == null ? null : enemyRigidbody.enemy;
            if (enemy == null || !Refs.EnemyHasStunned(enemy))
            {
                return;
            }
            Refs.EnemyStunned(enemy).Set(Soap.StunSeconds);
        }
    }
}
