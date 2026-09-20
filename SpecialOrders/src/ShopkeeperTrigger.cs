using UnityEngine;

namespace SpecialOrders
{
    // Makes the shopkeeper robot interactive: get close, look at it, press Interact.
    //
    // This does its own distance + aim test instead of using the game's InfoTextTrigger. That system is a physics raycast
    // against a trigger collider, which has a dead zone (a ray that starts inside the collider never hits it, and the robot
    // hangs from the ceiling, so walking under it put the camera inside) and refuses hits that land inside solid geometry.
    // The prompt itself is shown with the game's own info text, so it looks like every other prompt.
    internal sealed class ShopkeeperTrigger : MonoBehaviour
    {
        private const string Prompt = "Press [interact] to place a special order";

        private static ShopkeeperTrigger current;

        private ShopKeeper keeper;
        private Vector3 localCenter;
        private float bodyRadius;
        private int sightMask;

        // The robot can be switched off (Sleeping). Used to close the menu if that happens while it is open.
        internal static bool KeeperIsAsleep
        {
            get
            {
                if (current == null || current.keeper == null)
                {
                    return false;
                }
                ShopKeeper.State state = current.keeper.currentState;
                return state == ShopKeeper.State.Sleeping || state == ShopKeeper.State.GoToSleep;
            }
        }

        internal static void Attach(ShopKeeper keeper)
        {
            if (keeper == null)
            {
                return;
            }
            if (current != null)
            {
                if (current.keeper == keeper)
                {
                    return;
                }
                Destroy(current.gameObject);
                current = null;
            }

            Bounds bounds = default(Bounds);
            bool hasBounds = false;
            foreach (Renderer renderer in keeper.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Vector3 extents = bounds.extents;
            Vector3 center = hasBounds ? bounds.center : keeper.transform.position;
            float radius = hasBounds ? Mathf.Clamp(Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)), 0.6f, 2.5f) : 1f;

            GameObject root = new GameObject("SpecialOrdersInteraction");
            ShopkeeperTrigger controller = root.AddComponent<ShopkeeperTrigger>();
            controller.keeper = keeper;
            controller.localCenter = keeper.transform.InverseTransformPoint(center);
            controller.bodyRadius = radius;
            controller.sightMask = LayerMask.GetMask("Default", "PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "StaticGrabObject");
            current = controller;

            Plugin.Log.LogInfo("Shopkeeper interaction ready (" + (hasBounds ? "body extents " + extents.ToString("0.00") : "no bounds")
                + ", body radius " + radius.ToString("0.00") + " m, reach " + (radius + Plugin.InteractDistance.Value).ToString("0.0") + " m from its center).");
        }

        private void OnDestroy()
        {
            if (current == this)
            {
                current = null;
            }
        }

        private void Update()
        {
            if (keeper == null)
            {
                Destroy(gameObject);
                return;
            }

            if (OrdersMenu.IsOpen || !KeeperIsAwake() || !OrdersMenu.CanOpen() || !LocalPlayerCanInteract() || !LooksAtKeeper())
            {
                return;
            }

            InputManager input = InputManager.instance;
            string text = input != null ? input.InputDisplayReplaceTags(Prompt, "<color=#fff><u><b>", "</b></u></color>") : Prompt;
            if (ItemInfoUI.instance != null)
            {
                ItemInfoUI.instance.ItemInfoText(null, text);
            }

            if (SemiFunc.InputDown(InputKey.Interact))
            {
                OrdersMenu.Open(false);
            }
        }

        // Only when the robot is switched on and calm. Not while asleep, waking up, or busy with a troublemaker.
        private bool KeeperIsAwake()
        {
            ShopKeeper.State state = keeper.currentState;
            return state == ShopKeeper.State.Observe || state == ShopKeeper.State.Flash;
        }

        // Not while dead/spectating (the same key switches spectated players) or while holding something (it is used to toggle items).
        private static bool LocalPlayerCanInteract()
        {
            PlayerController controller = PlayerController.instance;
            if (controller == null || controller.playerAvatarScript == null || Refs.PlayerDisabled(controller.playerAvatarScript))
            {
                return false;
            }
            return PhysGrabber.instance == null || !PhysGrabber.instance.grabbed;
        }

        private bool LooksAtKeeper()
        {
            PlayerController controller = PlayerController.instance;
            if (controller.playerAvatarScript.localCamera == null)
            {
                return false;
            }
            Transform view = controller.playerAvatarScript.localCamera.GetOverrideTransform();
            Vector3 center = keeper.transform.TransformPoint(localCenter);
            Vector3 toCenter = center - view.position;
            float distance = toCenter.magnitude;

            // Close enough: within InteractDistance of the robot's body, not of its center.
            if (distance > bodyRadius + Plugin.InteractDistance.Value)
            {
                return false;
            }

            // Looking at it: the view direction must point at the body (or roughly forward when standing right under it).
            Vector3 direction = toCenter / Mathf.Max(distance, 0.0001f);
            float aimRadius = bodyRadius * 0.85f;
            bool aimed = distance <= aimRadius
                ? Vector3.Dot(view.forward, direction) > 0.2f
                : Vector3.Angle(view.forward, direction) <= Mathf.Asin(aimRadius / distance) * Mathf.Rad2Deg;
            if (!aimed)
            {
                return false;
            }

            // Nothing solid clearly in the way (hits right next to the robot, like its own body or ceiling mount, don't count).
            RaycastHit hit;
            if (Physics.Linecast(view.position, center, out hit, sightMask, QueryTriggerInteraction.Ignore)
                && hit.distance < distance - bodyRadius)
            {
                return false;
            }
            return true;
        }
    }
}
