using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SpecialOrders
{
    // A small rotating 3D view of an item. The model is rebuilt from the item prefab's meshes and materials (the prefab
    // itself is never instantiated, so none of its game logic runs) and drawn by a private camera far away from the map
    // into a RenderTexture that a RawImage shows in the menu. Hold the mouse button over it and drag to spin the item.
    internal sealed class ItemPreview : MonoBehaviour
    {
        private static readonly Vector3 StagePosition = new Vector3(4000f, 4000f, 4000f);
        private const float FieldOfView = 28f;

        private MenuPage menuPage;
        private RectTransform rect;

        internal RectTransform Rect
        {
            get { return rect; }
        }
        private RawImage image;
        private RenderTexture texture;
        private Camera previewCamera;
        private Transform stage;
        private Transform spin;
        private Transform model;
        private int layer;
        private bool dragging;
        private Vector2 lastMouse;

        internal static ItemPreview Create(Transform parent, MenuPage page, Vector2 bottomLeft, float size, float brightness)
        {
            GameObject go = new GameObject("Item Preview", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);

            RectTransform rectTransform = (RectTransform)go.transform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            // Pivot at the bottom-left: the game's own hover test (SemiFunc.UIMouseHover) assumes that.
            rectTransform.pivot = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(size, size);
            rectTransform.localPosition = new Vector3(bottomLeft.x, bottomLeft.y, 0f);

            ItemPreview preview = go.AddComponent<ItemPreview>();
            preview.menuPage = page;
            preview.rect = rectTransform;
            preview.image = go.GetComponent<RawImage>();
            preview.Build(brightness);
            return preview;
        }

        private void Build(float brightness)
        {
            layer = FindFreeLayer();

            texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            texture.antiAliasing = 4;
            texture.name = "SpecialOrders preview";
            texture.Create();
            image.texture = texture;
            image.color = Color.white;
            image.raycastTarget = false;

            stage = new GameObject("SpecialOrders preview stage").transform;
            stage.position = StagePosition;

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(stage, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.03f, 0.05f, 0.09f, 1f);
            previewCamera.cullingMask = 1 << layer;
            previewCamera.fieldOfView = FieldOfView;
            previewCamera.nearClipPlane = 0.02f;
            previewCamera.farClipPlane = 60f;
            previewCamera.allowHDR = false;
            previewCamera.useOcclusionCulling = false;
            previewCamera.targetTexture = texture;

            AddLight("Key light", new Vector3(1.8f, 1.6f, -2.2f), new Color(1f, 0.97f, 0.9f), 1.7f * brightness);
            AddLight("Fill light", new Vector3(-2.2f, 0.4f, -1.2f), new Color(0.75f, 0.85f, 1f), 0.9f * brightness);

            spin = new GameObject("Spin").transform;
            spin.SetParent(stage, false);
            model = new GameObject("Model").transform;
            model.SetParent(spin, false);
        }

        private void AddLight(string lightName, Vector3 localPosition, Color color, float intensity)
        {
            GameObject lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(stage, false);
            lightObject.transform.localPosition = localPosition;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 25f;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << layer;
        }

        // Returns false (and hides the panel) when the item has nothing to draw.
        internal bool Show(Item item)
        {
            ClearModel();
            image.enabled = false;
            try
            {
                GameObject prefab = item != null && item.prefab != null && item.prefab.IsValid() ? item.prefab.Prefab : null;
                if (prefab == null)
                {
                    Plugin.Log.LogWarning("Preview: no prefab for '" + (item != null ? item.name : "null") + "'.");
                    return false;
                }

                int parts = 0;
                Transform root = prefab.transform;
                Matrix4x4 toRoot = root.worldToLocalMatrix;
                foreach (Renderer source in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = MeshOf(source);
                    if (mesh == null || !source.enabled || !ActiveInPrefab(source.transform, root))
                    {
                        continue;
                    }
                    Material[] materials = source.sharedMaterials;
                    if (materials == null || materials.Length == 0)
                    {
                        continue;
                    }

                    GameObject part = new GameObject(source.name);
                    part.layer = layer;
                    part.transform.SetParent(model, false);

                    Matrix4x4 m = toRoot * source.transform.localToWorldMatrix;
                    Vector3 x = m.GetColumn(0);
                    Vector3 y = m.GetColumn(1);
                    Vector3 z = m.GetColumn(2);
                    part.transform.localPosition = m.GetColumn(3);
                    part.transform.localRotation = z.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(z, y) : Quaternion.identity;
                    part.transform.localScale = new Vector3(m.determinant < 0f ? -x.magnitude : x.magnitude, y.magnitude, z.magnitude);

                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer target = part.AddComponent<MeshRenderer>();
                    target.sharedMaterials = materials;
                    target.shadowCastingMode = ShadowCastingMode.Off;
                    target.receiveShadows = false;
                    parts++;
                }

                if (parts == 0)
                {
                    Plugin.Log.LogWarning("Preview: '" + item.name + "' has no visible meshes to draw.");
                    return false;
                }

                Quaternion offset = item.spawnRotationOffset;
                bool validOffset = Mathf.Abs(offset.x) + Mathf.Abs(offset.y) + Mathf.Abs(offset.z) + Mathf.Abs(offset.w) > 1e-4f;
                model.localRotation = validOffset ? offset : Quaternion.identity;
                model.localScale = Vector3.one;
                model.localPosition = Vector3.zero;
                spin.localRotation = Quaternion.Euler(12f, 30f, 0f);

                Bounds bounds = default(Bounds);
                bool hasBounds = false;
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    if (!hasBounds)
                    {
                        bounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
                float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (!hasBounds || largest < 1e-4f)
                {
                    Plugin.Log.LogWarning("Preview: '" + item.name + "' has empty bounds.");
                    return false;
                }

                // Fit the item into a unit-sized area around the spin pivot, then back the camera off until the whole thing is in view.
                float scale = 1f / largest;
                Vector3 centerInSpin = spin.InverseTransformPoint(bounds.center);
                model.localScale = Vector3.one * scale;
                model.localPosition = -centerInSpin * scale;

                float radius = 0.5f * bounds.size.magnitude * scale;
                float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.12f;
                previewCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
                previewCamera.transform.localRotation = Quaternion.identity;

                Plugin.Log.LogDebug("Preview: '" + item.name + "' " + parts + " parts, size " + bounds.size.ToString("0.00") + ", camera distance " + distance.ToString("0.00") + ".");
                image.enabled = true;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not build the item preview: " + ex.Message);
                ClearModel();
                return false;
            }
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            MeshRenderer meshRenderer = renderer as MeshRenderer;
            if (meshRenderer != null)
            {
                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            return skinned != null ? skinned.sharedMesh : null;
        }

        private static bool ActiveInPrefab(Transform t, Transform root)
        {
            while (t != null)
            {
                if (!t.gameObject.activeSelf)
                {
                    return false;
                }
                if (t == root)
                {
                    return true;
                }
                t = t.parent;
            }
            return true;
        }

        private void ClearModel()
        {
            if (model == null)
            {
                return;
            }
            // Immediate, because the new parts are measured right after this and must not include the old ones.
            for (int i = model.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(model.GetChild(i).gameObject);
            }
        }

        private void Update()
        {
            if (spin == null || !image.enabled)
            {
                return;
            }

            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButton(0))
            {
                if (!dragging && Input.GetMouseButtonDown(0) && menuPage != null && SemiFunc.UIMouseHover(menuPage, rect, "-1"))
                {
                    dragging = true;
                }
            }
            else
            {
                dragging = false;
            }

            if (dragging)
            {
                Vector2 delta = mouse - lastMouse;
                spin.Rotate(Vector3.up, -delta.x * 0.5f, Space.World);
                spin.Rotate(Vector3.right, delta.y * 0.5f, Space.World);
            }
            else
            {
                spin.Rotate(Vector3.up, 24f * Time.unscaledDeltaTime, Space.World);
            }
            lastMouse = mouse;
        }

        private void OnDestroy()
        {
            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
            }
            if (stage != null)
            {
                Destroy(stage.gameObject);
            }
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        // A physics layer nobody uses, so the private camera and lights only ever see the preview.
        private static int FindFreeLayer()
        {
            for (int i = 31; i >= 8; i--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
