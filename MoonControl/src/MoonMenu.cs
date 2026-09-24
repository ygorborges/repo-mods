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
                page.AddElementToScrollView(scroll =>
                {
                    REPOButton button = CreateRow(scroll, RowText(captured, !MoonState.RandomMode && captured == MoonState.Applied),
                        () => Net.Request(captured));
                    rows[captured] = button;
                    return button.rectTransform;
                });
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
        // scene behind the menu. Built once, geometry measured the same way SpecialOrders' order list measures its own
        // side panel (world corners of the list's mask/scrollbar, converted to the page's local space) - reusing its
        // proven width fraction. Spans the full height of the list, same as the list itself.

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

                // The list's own mask is a fixed-size scroll container, taller than however many rows happen to be in it
                // right now (that is the whole point of a scroll view) - sizing the right panel to the MASK made it taller
                // than the actual, visible list whenever there were only a few rows. Measuring the rows themselves (plus
                // Random) gives the true visible top/bottom of the list instead.
                Vector2 rowsMin = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 rowsMax = new Vector2(float.MinValue, float.MinValue);
                foreach (REPOButton button in rows.Values)
                {
                    if (button == null)
                    {
                        continue;
                    }
                    button.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        rowsMin = Vector2.Min(rowsMin, corner);
                        rowsMax = Vector2.Max(rowsMax, corner);
                    }
                }
                if (randomButton != null)
                {
                    randomButton.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        rowsMin = Vector2.Min(rowsMin, corner);
                        rowsMax = Vector2.Max(rowsMax, corner);
                    }
                }
                bool haveRows = rowsMin.x <= rowsMax.x;
                Vector3 listTop = host.InverseTransformPoint(haveRows ? new Vector3(rowsMin.x, rowsMax.y, 0f) : Vector3.zero);
                Vector3 listBottom = host.InverseTransformPoint(haveRows ? new Vector3(rowsMin.x, rowsMin.y, 0f) : Vector3.zero);

                page.maskRectTransform.GetWorldCorners(corners);
                Vector3 maskTopLeft = host.InverseTransformPoint(corners[1]);
                Vector3 maskBottomLeft = host.InverseTransformPoint(corners[0]);
                // maskHeight (the scroll container's own, larger size) is still what decides the column's WIDTH and left
                // margin, the same proven fraction SpecialOrders' order list uses - only the vertical extent (top/bottom,
                // and everything stacked within it below) switches to the rows' real height.
                float maskHeight = maskTopLeft.y - maskBottomLeft.y;
                float top = haveRows ? listTop.y : maskTopLeft.y;
                float bottom = haveRows ? listBottom.y : maskBottomLeft.y;
                float height = top - bottom;

                page.scrollBarRectTransform.GetWorldCorners(corners);
                float left = host.InverseTransformPoint(corners[2]).x + maskHeight * 0.06f;
                float side = maskHeight * 0.56f * 1.3f;
                float margin = height * 0.03f;

                // A dark panel behind everything else on this side - without it the text just floats over the 3D scene
                // behind the menu, unreadable.
                RectTransform backgroundRect = AddBackground(host, new Vector2(left - margin, top + margin), side + margin * 2f, height + margin * 2f);

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

                // A fixed, modest chunk at the bottom for the general explanation - the attribute list above gets
                // whatever is left, which is most of the column, since it is real game text (one to a few full
                // sentences per moon) that needs far more room than a single status line did.
                float descHeight = Mathf.Max(height * 0.15f, 40f);
                description = AddTextBlock(host, new Vector2(left, bottom + descHeight), side, descHeight, 14f, 9f, false);
                description.labelTMP.text =
                    "Each moon level applied adds " + Plugin.ValueBonusPercent.Value.ToString("0.#")
                    + "% to what every valuable is worth (added together per level), for as long as it stays applied. Picking one "
                    + "also turns on everything else that moon changes in the game. Only the host's choice counts.";

                float attrTop = iconAreaTop - iconAreaHeight - gap;
                float attrBottom = bottom + descHeight + gap;
                float attrHeight = Mathf.Max(attrTop - attrBottom, height * 0.1f);
                previewAttributes = AddTextBlock(host, new Vector2(left, attrTop), side, attrHeight, 15f, 8f, false);

                // Close moved out of the list entirely, to the bottom-right corner of the whole popup - built as a plain
                // button rather than a REPOButton/MenuButton this time. MenuButton drives its hover highlight through a
                // shared "selection box" system (SemiFunc.MenuSelectionBoxTargetSet) built for buttons living in a proper
                // menu grid or scroll list; outside of one, twice fixing its rectTransform.sizeDelta (once directly, once
                // via overrideButtonSize) still left the highlight itself showing at roughly a full list row's width. A
                // plain Button+Image sidesteps that machinery entirely instead of chasing it further.
                RectTransform closeRect = AddCloseButton(host, new Vector2(left + side, bottom));

                RefreshPreview();

                List<RectTransform> parts = new List<RectTransform>
                {
                    page.maskRectTransform, page.scrollBarRectTransform, backgroundRect, closeRect,
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

        // A plain, self-contained close button - a UnityEngine.UI.Button, not a REPOButton/MenuButton, so it never touches
        // the game's own hover-highlight/selection-box system (see the comment where this is called). bottomRight is
        // where its own bottom-right corner should land.
        private static RectTransform AddCloseButton(Transform host, Vector2 bottomRight)
        {
            const float width = 90f;
            const float height = 30f;

            GameObject go = new GameObject("Close", typeof(RectTransform));
            go.transform.SetParent(host, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localPosition = new Vector3(bottomRight.x, bottomRight.y, 0f);

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.95f);
            colors.pressedColor = HexColor(Gold);
            button.colors = colors;
            button.onClick.AddListener(() => page.ClosePage(true));

            TextMeshProUGUI text = CreateText(rect, textStyle, "Text", 14f, TextAlignmentOptions.Midline);
            Stretch(text.rectTransform);
            text.text = "Close";
            text.raycastTarget = false;
            return rect;
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
