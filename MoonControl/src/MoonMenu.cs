using System;
using System.Collections.Generic;
using System.Text;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonControl
{
    // The page that lets you pick which moon's effects are in effect right now, built with MenuLib so it looks and behaves
    // like the game's own menus (the same two-column layout SpecialOrders' order list uses: a scrollable list on the left,
    // a preview of whatever row is under the mouse on the right). One row per moon level you have actually unlocked
    // (0 = none, the game's own baseline) plus "Random" (a standing strategy, not an immediate pick - see RandomSentinel),
    // and whichever is currently in effect is marked. Only the host can pick one (Net.Request/RequestRandom themselves also
    // refuse anyone else, as a backstop) - everyone else's copy is read-only, its highlight still following the host.
    internal static class MoonMenu
    {
        private const string Gold = "#FFD24A";
        private const string Grey = "#808080";
        private const string White = "#FFFFFF";

        // selectedLevel's sentinel for "previewing/selected on Random", since 0 already means "None" and every other
        // non-negative number is a real moon level.
        private const int RandomSentinel = -1;

        // The gap between the popup's own panel and the preview panel beside it.
        private const float PanelGap = 10f;

        private static REPOPopupPage page;
        private static TextMeshProUGUI textStyle;
        private static readonly Dictionary<int, REPOButton> rows = new Dictionary<int, REPOButton>();
        private static REPOButton randomButton;
        private static float rowWidth;
        private static int shownFor = -1;          // the Unlocked() value the current rows were built for
        private static int highlighted = -1;       // the Applied() value the labels currently show
        private static bool highlightedRandom;     // the RandomMode() value the labels currently show

        // ---- the right-hand preview: whichever row is under the mouse (defaults to whatever is applied/selected)
        private static RawImage previewIcon;
        private static readonly List<RawImage> multiIcons = new List<RawImage>();
        private static REPOLabel previewTitle;
        private static REPOLabel previewAttributes;
        private static REPOLabel description;
        private static float iconAreaLeft, iconAreaTop, iconAreaWidth, iconAreaHeight;
        private static int selectedLevel = -1;

        // ---- the Close button (hit-tested by hand every frame, see TickCloseButton)
        private static RectTransform closeRect;
        private static Image closeImage;
        private static TextMeshProUGUI closeText;
        private static Camera closeCamera;
        private static bool closeHovering;
        private static readonly Color CloseIdle = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        private static readonly Color CloseHover = new Color(0.32f, 0.32f, 0.32f, 0.95f);

        internal static bool IsOpen
        {
            get { return page != null; }
        }

        internal static bool CanOpen()
        {
            if (!SemiFunc.RunIsLobby())
            {
                return false;
            }
            if (GameDirector.instance == null || GameDirector.instance.currentState != GameDirector.gameState.Main)
            {
                return false;
            }
            MenuManager menu = MenuManager.instance;
            if (menu == null || Refs.CurrentMenuPage(menu) != null)
            {
                return false;
            }
            return SemiFunc.NoTextInputsActive();
        }

        internal static void Open()
        {
            if (IsOpen || !CanOpen())
            {
                return;
            }
            try
            {
                rows.Clear();
                randomButton = null;
                shownFor = -1;
                highlighted = -1;
                highlightedRandom = false;
                selectedLevel = MoonState.RandomMode ? RandomSentinel : MoonState.Applied;

                page = MenuAPI.CreateREPOPopupPage("MOON CONTROL", REPOPopupPage.PresetSide.Left, false, true, 6f);
                page.gameObject.AddComponent<Watcher>();
                // Shrinks the whole popup (background, list, scrollbar, preview panel - everything under "Page Content")
                // uniformly. CenterOnScreen() below measures world corners, so it sees the scaled-down size correctly.
                page.rectTransform.localScale = Vector3.one * Plugin.MenuScale.Value;
                rowWidth = Mathf.Max(100f, page.maskRectTransform.sizeDelta.x - 14f);

                REPOLabel donor = MenuAPI.CreateREPOLabel("", page.rectTransform);
                donor.gameObject.SetActive(false);
                textStyle = donor.labelTMP;

                page.OpenPage(false);
                Refresh();
                BuildSidePanel();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not open the moon menu: " + ex);
                if (page != null)
                {
                    UnityEngine.Object.Destroy(page.gameObject);
                }
                OnPageDestroyed();
            }
        }

        // Rebuilds the row list if how much is unlocked has changed since it was last built (finishing a level while the
        // menu sits open, however unlikely), and always refreshes which one is highlighted.
        private static void Refresh()
        {
            if (page == null)
            {
                return;
            }
            int unlocked = MoonState.Unlocked(RunManager.instance);
            if (unlocked != shownFor)
            {
                BuildRows(unlocked);
                shownFor = unlocked;
            }

            int applied = MoonState.Applied;
            bool randomMode = MoonState.RandomMode;
            if (applied != highlighted || randomMode != highlightedRandom)
            {
                highlighted = applied;
                highlightedRandom = randomMode;
                foreach (KeyValuePair<int, REPOButton> pair in rows)
                {
                    pair.Value.labelTMP.text = RowText(pair.Key, !randomMode && pair.Key == applied);
                }
                if (randomButton != null)
                {
                    randomButton.labelTMP.text = RandomRowText(randomMode);
                }
            }

            // Whichever row is under the mouse drives the preview - same technique SpecialOrders' order list uses.
            bool hovering = false;
            foreach (KeyValuePair<int, REPOButton> pair in rows)
            {
                REPOButton button = pair.Value;
                if (button != null && button.menuButton != null && button.gameObject.activeInHierarchy && Refs.ButtonHovering(button.menuButton))
                {
                    Select(pair.Key);
                    hovering = true;
                    break;
                }
            }
            if (!hovering && randomButton != null && randomButton.menuButton != null && randomButton.gameObject.activeInHierarchy
                && Refs.ButtonHovering(randomButton.menuButton))
            {
                Select(RandomSentinel);
            }
        }

        private static void BuildRows(int unlocked)
        {
            foreach (REPOButton button in rows.Values)
            {
                if (button != null)
                {
                    UnityEngine.Object.Destroy(button.gameObject);
                }
            }
            rows.Clear();
            if (randomButton != null)
            {
                UnityEngine.Object.Destroy(randomButton.gameObject);
                randomButton = null;
            }

            bool isHost = SemiFunc.IsMasterClientOrSingleplayer();
            for (int level = 0; level <= unlocked; level++)
            {
                int captured = level;
                // The first row carries the gap between the page's title and the start of the list (REPOScrollView honours
                // each element's own top padding when it stacks them).
                float topPadding = level == 0 ? 18f : 0f;
                page.AddElementToScrollView(scroll =>
                {
                    REPOButton button = CreateRow(scroll, RowText(captured, !MoonState.RandomMode && captured == MoonState.Applied),
                        () => Net.Request(captured));
                    rows[captured] = button;
                    return button.rectTransform;
                }, topPadding);
            }

            if (isHost && unlocked >= 1)
            {
                page.AddElementToScrollView(scroll =>
                {
                    REPOButton button = CreateRow(scroll, RandomRowText(MoonState.RandomMode), () => Net.RequestRandom());
                    randomButton = button;
                    return button.rectTransform;
                });
            }

            if (unlocked < 1)
            {
                AddNote2("No moon is unlocked yet in this run - it takes 5 levels for the first one. Only \"none\" is available.");
            }
            if (!isHost)
            {
                AddNote2("You are not the host, so this list is read-only for you - it still follows what the host picks.");
            }
        }

        private static REPOButton CreateRow(Transform scroll, string text, Action onClick)
        {
            REPOButton button = MenuAPI.CreateREPOButton(text, onClick, scroll);
            // Every row stays exactly one line tall: word-wrap off, auto-sizing shrinks the font instead for a long moon
            // name, with an ellipsis as the last resort. This only ever SETS a fixed height computed from the label's own
            // current font size - it never asks TMP to measure one back, which is what kept going wrong before
            // (GetPreferredValues/ForceMeshUpdate on this same REPOButton label gave nonsense every time it was tried).
            TextMeshProUGUI label = button.labelTMP;
            float baseFontSize = label.fontSize;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMax = baseFontSize;
            label.fontSizeMin = Mathf.Max(10f, baseFontSize * 0.55f);
            float lineHeight = baseFontSize > 0f ? baseFontSize * 1.3f : 24f;
            button.overrideButtonSize = new Vector2(rowWidth, lineHeight);
            return button;
        }

        private static string RowText(int level, bool applied)
        {
            string label = level <= 0 ? "None (the game's own baseline)" : "Moon " + level + NameSuffix(level);
            string colour = applied ? Gold : White;
            string mark = applied ? "◉ " : "○ ";
            return "<color=" + colour + ">" + mark + label + "</color>";
        }

        private static string RandomRowText(bool applied)
        {
            string colour = applied ? Gold : White;
            string mark = applied ? "◉ " : "○ ";
            return "<color=" + colour + ">" + mark + "Random (among unlocked moons)</color>";
        }

        private static string NameSuffix(int level)
        {
            RunManager run = RunManager.instance;
            string name = run == null ? "" : run.MoonGetName(level);
            return string.IsNullOrEmpty(name) ? "" : " - " + name;
        }

        private static float BonusPercent(int level)
        {
            return Plugin.ValueBonusPercent.Value * level;
        }

        // ---- selecting a row for preview (hover-driven, defaults to whatever is applied/selected on open)

        private static void Select(int level)
        {
            if (level == selectedLevel)
            {
                return;
            }
            selectedLevel = level;
            RefreshPreview();
        }

        private static void RefreshPreview()
        {
            if (previewTitle == null)
            {
                return;
            }
            RunManager run = RunManager.instance;

            if (selectedLevel == RandomSentinel)
            {
                previewTitle.labelTMP.text = "Random";
                int unlocked = MoonState.Unlocked(run);
                ShowMultiIcons(unlocked);
                previewAttributes.labelTMP.text = unlocked < 1
                    ? "No moon is unlocked yet, so there is nothing to roll among until level 5."
                    : "A random moon among the ones you have unlocked is applied as a surprise the moment the next level "
                        + "starts, and rolled again for the one after that - you find out which one only by playing it.";
                return;
            }

            if (selectedLevel <= 0 || run == null)
            {
                previewTitle.labelTMP.text = "None";
                ShowSingleIcon(null);
                previewAttributes.labelTMP.text = "The game's own baseline - no moon effects, and no bonus to what valuables are worth.";
                return;
            }

            string name = run.MoonGetName(selectedLevel);
            previewTitle.labelTMP.text = "<color=" + Gold + ">Moon " + selectedLevel + (string.IsNullOrEmpty(name) ? "" : " - " + name) + "</color>";
            ShowSingleIcon(run.MoonGetIcon(selectedLevel));

            StringBuilder sb = new StringBuilder();
            sb.Append("<color=" + Gold + ">+").Append(BonusPercent(selectedLevel).ToString("0.#")).Append("% to what every valuable is worth</color>");
            List<Moon.MoonAttribute> attributes = run.MoonGetAttributes(selectedLevel);
            if (attributes != null)
            {
                foreach (Moon.MoonAttribute attribute in attributes)
                {
                    if (attribute == null)
                    {
                        continue;
                    }
                    string text = attribute.LocalizedText != null ? attribute.LocalizedText.GetLocalizedString() : attribute.text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        sb.Append("\n").Append(text);
                    }
                }
            }
            previewAttributes.labelTMP.text = sb.ToString();
        }

        // A single icon, centered in the icon area - used for "None" and every real moon level.
        private static void ShowSingleIcon(Texture texture)
        {
            ClearMultiIcons();
            if (previewIcon == null)
            {
                return;
            }
            float size = Mathf.Min(iconAreaWidth * 0.4f, iconAreaHeight);
            RectTransform rect = previewIcon.rectTransform;
            rect.sizeDelta = new Vector2(size, size);
            rect.localPosition = new Vector3(iconAreaLeft + (iconAreaWidth - size) * 0.5f, iconAreaTop, 0f);
            previewIcon.texture = texture;
            previewIcon.enabled = texture != null;
        }

        // Every unlocked moon's icon, side by side, filling the same icon area - "it could be any of these" for Random.
        private static void ShowMultiIcons(int unlocked)
        {
            ClearMultiIcons();
            if (previewIcon != null)
            {
                previewIcon.enabled = false;
            }
            if (unlocked < 1)
            {
                return;
            }
            RunManager run = RunManager.instance;
            float gap = 4f;
            float cell = Mathf.Min(iconAreaHeight, (iconAreaWidth - gap * (unlocked - 1)) / unlocked);
            float totalWidth = cell * unlocked + gap * (unlocked - 1);
            float startX = iconAreaLeft + (iconAreaWidth - totalWidth) * 0.5f;
            for (int i = 1; i <= unlocked; i++)
            {
                GameObject go = new GameObject("RandomPreviewIcon" + i, typeof(RectTransform));
                go.transform.SetParent(page.rectTransform, false);
                RectTransform rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(cell, cell);
                rect.localPosition = new Vector3(startX + (i - 1) * (cell + gap), iconAreaTop, 0f);
                RawImage icon = go.AddComponent<RawImage>();
                icon.raycastTarget = false;
                Texture texture = run != null ? run.MoonGetIcon(i) : null;
                icon.texture = texture;
                icon.enabled = texture != null;
                multiIcons.Add(icon);
            }
        }

        private static void ClearMultiIcons()
        {
            foreach (RawImage icon in multiIcons)
            {
                if (icon != null)
                {
                    UnityEngine.Object.Destroy(icon.gameObject);
                }
            }
            multiIcons.Clear();
        }

        // ---- the right-hand side panel: icon(s) + name + effects on top (like the game's own "moon just changed"
        // screen), the mod's general explanation at the bottom, all against its own background so it reads against the 3D
        // scene behind the menu. Built once, as a twin of the popup's own background panel (the "Panel" child MenuLib
        // re-parents under the page content): same size, sitting right beside it.

        private static void BuildSidePanel()
        {
            try
            {
                Transform host = page.rectTransform;
                Vector3[] corners = new Vector3[4];

                // Rows are only actually laid out (stacked to their final Y position within the scroll view) once
                // REPOScrollView processes them - which had not happened yet the first time this measured them, right
                // after creating them in the same call, so it was reading their pre-layout (not yet stacked) positions.
                // Forcing that pass first is what OpenPage() itself does before any rows exist yet, so it has to be
                // repeated here now that they do.
                if (page.scrollView != null)
                {
                    page.scrollView.UpdateElements();
                }

                // The popup's own background art, which is what reads on screen as "the left panel". Matching it exactly
                // (and sitting the new panel alongside) is what makes the two columns look like a pair, instead of the
                // right one being a smaller box floating next to a much taller one.
                RectTransform panel = page.rectTransform.Find("Panel") as RectTransform;
                float left, top, bottom, side;
                if (panel != null)
                {
                    panel.GetWorldCorners(corners);
                    Vector3 panelBottomLeft = host.InverseTransformPoint(corners[0]);
                    Vector3 panelTopRight = host.InverseTransformPoint(corners[2]);
                    top = panelTopRight.y;
                    bottom = panelBottomLeft.y;
                    side = panelTopRight.x - panelBottomLeft.x;
                    left = panelTopRight.x + PanelGap;
                }
                else
                {
                    // No such child to measure (a MenuLib change, say): fall back to the list's own mask, which is at least
                    // always there.
                    page.maskRectTransform.GetWorldCorners(corners);
                    Vector3 maskBottomLeft = host.InverseTransformPoint(corners[0]);
                    Vector3 maskTopRight = host.InverseTransformPoint(corners[2]);
                    top = maskTopRight.y;
                    bottom = maskBottomLeft.y;
                    side = (maskTopRight.y - maskBottomLeft.y) * 0.73f;
                    left = maskTopRight.x + PanelGap;
                }

                // A dark panel behind everything else on this side - without it the text just floats over the 3D scene
                // behind the menu, unreadable.
                RectTransform backgroundRect = AddBackground(host, new Vector2(left, top), side, top - bottom);

                // Everything inside the panel keeps clear of its edges.
                float inset = Mathf.Max(10f, (top - bottom) * 0.04f);
                left += inset;
                side -= inset * 2f;
                top -= inset;
                bottom += inset;
                float height = top - bottom;

                float titleHeight = height * 0.09f;
                previewTitle = AddTextBlock(host, new Vector2(left, top), side, titleHeight, 20f, 12f, true);
                previewTitle.labelTMP.alignment = TextAlignmentOptions.Midline;

                float gap = height * 0.02f;
                iconAreaTop = top - titleHeight - gap;
                iconAreaHeight = height * 0.16f;
                iconAreaLeft = left;
                iconAreaWidth = side;
                GameObject iconGo = new GameObject("PreviewIcon", typeof(RectTransform));
                iconGo.transform.SetParent(host, false);
                RectTransform iconRect = (RectTransform)iconGo.transform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0f, 1f);
                previewIcon = iconGo.AddComponent<RawImage>();
                previewIcon.raycastTarget = false;

                // The bottom of the column is split into two reserved strips: the explanation, and below it a footer
                // just for Close. Close used to be placed at the column's bottom edge with nothing reserved for it, so
                // it landed on top of the explanation's last lines. The explanation is also kept short enough to fit
                // its own strip outright - the longer one it had overflowed its box (TMP draws outside the rect once
                // auto-sizing bottoms out) and spilled down across the footer as well.
                const float closeWidth = 90f;
                const float closeHeight = 26f;
                float footer = closeHeight + gap;

                float descHeight = Mathf.Max(height * 0.16f, 40f);
                float descTop = bottom + footer + descHeight;
                description = AddTextBlock(host, new Vector2(left, descTop), side, descHeight, 12f, 8f, false);
                description.labelTMP.text =
                    "Each moon level applied adds " + Plugin.ValueBonusPercent.Value.ToString("0.#")
                    + "% to what every valuable is worth. Only the host's choice counts.";

                float attrTop = iconAreaTop - iconAreaHeight - gap;
                float attrBottom = descTop + gap;
                float attrHeight = Mathf.Max(attrTop - attrBottom, height * 0.1f);
                previewAttributes = AddTextBlock(host, new Vector2(left, attrTop), side, attrHeight, 15f, 8f, false);

                RectTransform close = AddCloseButton(host, new Vector2(left + side - closeWidth, bottom), closeWidth, closeHeight);

                RefreshPreview();

                List<RectTransform> parts = new List<RectTransform>
                {
                    panel, page.maskRectTransform, page.scrollBarRectTransform, backgroundRect, close,
                    previewTitle.rectTransform, previewAttributes.rectTransform, description.rectTransform,
                };
                CenterOnScreen(parts);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not build the moon menu's preview panel: " + ex);
            }
        }

        // A free-standing text block anchored by its top-left corner (the same recipe SpecialOrders' order list uses for
        // its item name/description beside the preview). The text shrinks to fit the box (down to minFontSize) instead of
        // ever being cut off or overflowing it.
        private static REPOLabel AddTextBlock(Transform host, Vector2 topLeft, float width, float height, float fontSize, float minFontSize, bool singleLine)
        {
            REPOLabel label = MenuAPI.CreateREPOLabel("", host);
            SetTopLeftRect(label.rectTransform, width, height);
            TextMeshProUGUI text = label.labelTMP;
            text.fontSize = fontSize;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = minFontSize;
            text.fontSizeMax = fontSize;
            text.richText = true;
            if (singleLine)
            {
                text.enableWordWrapping = false;
                text.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                text.enableWordWrapping = true;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.color = new Color(0.85f, 0.85f, 0.85f);
            }
            SetTopLeftRect(text.rectTransform, width, height);
            text.rectTransform.localPosition = Vector3.zero;
            label.rectTransform.localPosition = new Vector3(topLeft.x, topLeft.y, 0f);
            return label;
        }

        private static void SetTopLeftRect(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
        }

        // A plain dark translucent panel, top-left anchored like AddTextBlock's own rect - just enough to read text
        // against, not trying to match the popup's own template art.
        private static RectTransform AddBackground(Transform host, Vector2 topLeft, float width, float height)
        {
            GameObject go = new GameObject("PreviewBackground", typeof(RectTransform));
            go.transform.SetParent(host, false);
            RectTransform rect = (RectTransform)go.transform;
            SetTopLeftRect(rect, width, height);
            rect.localPosition = new Vector3(topLeft.x, topLeft.y, 0f);
            Image image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = false;
            return rect;
        }

        // A self-contained close button: just an Image and a label, hit-tested by hand in TickCloseButton.
        //
        // It is deliberately neither a REPOButton/MenuButton nor a UnityEngine.UI.Button. MenuButton drives its hover
        // highlight through a shared "selection box" (SemiFunc.MenuSelectionBoxTargetSet) meant for buttons living in a
        // real menu grid or scroll list; outside of one it showed a highlight roughly a full list row wide no matter
        // what size the button itself was given. A UnityEngine.UI.Button, in turn, was never clickable at all: the
        // game's menus have no EventSystem-driven input, every one of its own buttons polls SemiFunc.UIMouseHover plus
        // the legacy Input.GetMouseButtonDown(0) itself (MenuButton.HoverLogic), so nothing ever raised onClick.
        //
        // bottomLeft is where its bottom-left corner should land; the pivot has to be (0,0) for the game's own hover
        // test (used as a fallback below) to line up - it applies the pivot offset twice for any other pivot.
        private static RectTransform AddCloseButton(Transform host, Vector2 bottomLeft, float width, float height)
        {
            GameObject go = new GameObject("Close", typeof(RectTransform));
            go.transform.SetParent(host, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            rect.localPosition = new Vector3(bottomLeft.x, bottomLeft.y, 0f);

            closeImage = go.AddComponent<Image>();
            closeImage.color = CloseIdle;
            closeImage.raycastTarget = false;

            closeText = CreateText(rect, textStyle, "Text", 14f, TextAlignmentOptions.Midline);
            Stretch(closeText.rectTransform);
            closeText.text = "Close";

            Canvas canvas = page.GetComponentInParent<Canvas>();
            closeCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (canvas.worldCamera != null ? canvas.worldCamera : OverlayCamera())
                : null;
            closeRect = rect;
            closeHovering = false;
            return rect;
        }

        // The camera the game itself measures UI against (CameraOverlay.overlayCamera is internal, but its Awake just
        // takes the Camera off its own object, so this is the same one).
        private static Camera OverlayCamera()
        {
            try
            {
                return CameraOverlay.instance != null ? CameraOverlay.instance.GetComponent<Camera>() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Polls the mouse the same way the game's own buttons do. The primary test is Unity's own, which is the only
        // one that stays correct while the whole popup is scaled by MenuScale: the game's math adds the rect's
        // UNSCALED extents to an already-scaled offset, so its box comes out 1/MenuScale too large. Its test is still
        // kept as a fallback, in case this canvas needs a camera other than the one worked out above.
        private static void TickCloseButton()
        {
            if (closeRect == null)
            {
                return;
            }
            bool hovering;
            try
            {
                hovering = RectTransformUtility.RectangleContainsScreenPoint(closeRect, Input.mousePosition, closeCamera)
                    || (page.menuPage != null && SemiFunc.UIMouseHover(page.menuPage, closeRect, "-1"));
            }
            catch (Exception)
            {
                return;
            }

            if (hovering != closeHovering)
            {
                closeHovering = hovering;
                if (closeImage != null)
                {
                    closeImage.color = hovering ? CloseHover : CloseIdle;
                }
                if (closeText != null)
                {
                    closeText.color = hovering ? HexColor(Gold) : Color.white;
                }
            }
            if (hovering && Input.GetMouseButtonDown(0))
            {
                page.ClosePage(true);
            }
        }

        // A second, shorter note (used for "nothing unlocked yet" and the read-only notice): its own object each time, so
        // several can coexist without fighting over one cached reference.
        private static void AddNote2(string text)
        {
            page.AddElementToScrollView(scroll =>
            {
                GameObject go = new GameObject("Note2", typeof(RectTransform));
                go.transform.SetParent(scroll, false);
                RectTransform rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(rowWidth, 40f);
                rect.localPosition = Vector3.zero;

                TextMeshProUGUI text2 = CreateText(rect, textStyle, "Text", 14f, TextAlignmentOptions.TopLeft);
                Stretch(text2.rectTransform);
                text2.color = HexColor(Grey);
                text2.enableWordWrapping = true;
                text2.text = text;
                return rect;
            }, 0f, 8f);
        }

        // ---- centering (the popup otherwise settles hugging the left edge of the screen, leaving the rest empty)

        // REPOPopupPage.PresetSide.Left only controls which side it slides in from, not where it settles - it lands off to
        // that side. Moves the whole group (list, scrollbar, background, Close and the preview panel) so it sits centered
        // on the screen instead, the same technique SpecialOrders' order list uses (accounting for the page still being
        // mid-slide-in when this runs).
        private static void CenterOnScreen(List<RectTransform> parts)
        {
            try
            {
                Canvas canvas = page.GetComponentInParent<Canvas>();
                RectTransform canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
                if (canvasRect == null)
                {
                    return;
                }
                Vector3 screenCenter = canvasRect.TransformPoint(canvasRect.rect.center);

                Vector3[] corners = new Vector3[4];
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                foreach (RectTransform part in parts)
                {
                    if (part == null)
                    {
                        continue;
                    }
                    part.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        min = Vector2.Min(min, corner);
                        max = Vector2.Max(max, corner);
                    }
                }
                if (min.x > max.x)
                {
                    return;
                }

                Transform root = page.transform;
                Vector3 slide = Vector3.zero;
                MenuPage menuPage = page.menuPage;
                if (root.parent != null && Refs.PageRect(menuPage) != null)
                {
                    Vector2 landed = Refs.PageOriginalPosition(menuPage);
                    Vector3 landedLocal = new Vector3(landed.x, landed.y, root.localPosition.z);
                    slide = root.parent.TransformVector(root.localPosition - landedLocal);
                }

                Vector3 groupCenter = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, screenCenter.z) - slide;
                Vector3 shift = screenCenter - groupCenter;
                shift.z = 0f;

                Vector3 canvasSize = Vector3.Scale(canvasRect.rect.size, canvasRect.lossyScale);
                if (Mathf.Abs(shift.x) > canvasSize.x * 0.5f || Mathf.Abs(shift.y) > canvasSize.y * 0.5f)
                {
                    Plugin.Log.LogWarning("Skipped centering the moon menu: the measured shift " + shift.ToString("0.0") + " is implausible.");
                    return;
                }
                page.rectTransform.position += shift;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not center the moon menu: " + ex.Message);
            }
        }

        private static Color HexColor(string hex)
        {
            Color colour;
            return ColorUtility.TryParseHtmlString(hex, out colour) ? colour : Color.grey;
        }

        private static TextMeshProUGUI CreateText(Transform parent, TextMeshProUGUI style, string objectName, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = alignment;
            text.color = Color.white;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ---- lifecycle

        private static void Tick()
        {
            if (!IsOpen)
            {
                return;
            }
            if (GameDirector.instance != null)
            {
                GameDirector.instance.SetDisableEscMenu(0.3f);
            }
            // Only close for reasons that genuinely mean "this should not be open any more" (left the truck, or the game
            // moved past the lobby state) - not the full CanOpen(), which also checks "is some menu already open". Once
            // this page is open it IS that menu, so re-running that check here closed it again the same frame it opened
            // (the button worked, but the menu flashed open and shut instead of staying up). SpecialOrders' order list
            // has the same split for the same reason.
            if (!SemiFunc.RunIsLobby() || GameDirector.instance == null || GameDirector.instance.currentState != GameDirector.gameState.Main)
            {
                page.ClosePage(true);
                return;
            }
            Refresh();
            TickCloseButton();
        }

        private static void OnPageDestroyed()
        {
            page = null;
            textStyle = null;
            randomButton = null;
            previewIcon = null;
            previewTitle = null;
            previewAttributes = null;
            description = null;
            closeRect = null;
            closeImage = null;
            closeText = null;
            closeCamera = null;
            closeHovering = false;
            multiIcons.Clear();
            selectedLevel = -1;
            rows.Clear();
            shownFor = -1;
            highlighted = -1;
            highlightedRandom = false;
        }

        private sealed class Watcher : MonoBehaviour
        {
            private void Update()
            {
                Tick();
            }

            private void OnDestroy()
            {
                OnPageDestroyed();
            }
        }
    }
}
