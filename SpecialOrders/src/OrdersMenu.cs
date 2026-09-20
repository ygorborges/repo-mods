using System;
using System.Collections.Generic;
using System.Text;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;

namespace SpecialOrders
{
    // The order list, built with MenuLib so it looks and behaves like the game's own menus.
    // Left: the item list (name, deposit, and what is left to pay on arrival). Right: a rotating preview of the item
    // under the cursor. The whole thing is centered on the screen.
    internal static class OrdersMenu
    {
        private static readonly SemiFunc.itemType[] CategoryOrder =
        {
            SemiFunc.itemType.gun, SemiFunc.itemType.melee, SemiFunc.itemType.grenade, SemiFunc.itemType.mine,
            SemiFunc.itemType.drone, SemiFunc.itemType.orb, SemiFunc.itemType.healthPack, SemiFunc.itemType.item_upgrade,
            SemiFunc.itemType.player_upgrade, SemiFunc.itemType.power_crystal, SemiFunc.itemType.tracker,
            SemiFunc.itemType.cart, SemiFunc.itemType.pocket_cart, SemiFunc.itemType.tool,
            SemiFunc.itemType.launcher, SemiFunc.itemType.vehicle,
        };

        // Width of each of the two price columns, as a share of the row width, and the gap at the right edge.
        private const float ColumnFraction = 0.2f;
        private const float RightPad = 12f;

        private const string Gold = "#FFD24A";
        private const string Green = "#7CE07C";
        private const string Grey = "#808080";

        private sealed class Row
        {
            public REPOButton Button;
            public TextMeshProUGUI Deposit;
            public TextMeshProUGUI Arrival;
            public TextMeshProUGUI Span;
        }

        // The note under the Close button: one line at least, four at most (messages are far shorter than that).
        private const float NoteFontSize = 16f;
        private const float NoteMinHeight = 26f;
        private const float NoteMaxHeight = 100f;

        private static REPOPopupPage page;
        private static TextMeshProUGUI note;
        private static REPOLabel titleLabel;
        private static REPOLabel detailLabel;
        private static ItemPreview preview;
        private static TextMeshProUGUI textStyle;
        private static readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        private static float rowWidth;
        private static float columnWidth;
        private static bool built;
        private static bool openedViaDebugHotkey;
        private static float openedAt;
        private static float busyUntil;
        private static string selectedKey;

        internal static bool IsOpen
        {
            get { return page != null; }
        }

        internal static bool CanOpen()
        {
            if (!SemiFunc.RunIsShop())
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

        internal static void Open(bool viaDebugHotkey)
        {
            if (IsOpen || !CanOpen())
            {
                return;
            }

            try
            {
                ClientState.Current = null;
                rows.Clear();
                built = false;
                selectedKey = null;
                busyUntil = 0f;
                openedAt = Time.unscaledTime;
                openedViaDebugHotkey = viaDebugHotkey;

                page = MenuAPI.CreateREPOPopupPage("SPECIAL ORDERS", REPOPopupPage.PresetSide.Left, false, true, 6f);
                page.gameObject.AddComponent<Watcher>();
                rowWidth = Mathf.Max(100f, page.maskRectTransform.sizeDelta.x - 14f);
                columnWidth = rowWidth * ColumnFraction;

                // A hidden label that only lends its font, glow and colour to the texts built below.
                REPOLabel donor = MenuAPI.CreateREPOLabel("", page.rectTransform);
                donor.gameObject.SetActive(false);
                textStyle = donor.labelTMP;

                page.AddElementToScrollView(scroll =>
                {
                    REPOButton close = MenuAPI.CreateREPOButton("Close", () => page.ClosePage(true), scroll);
                    return close.rectTransform;
                });
                AddNote("Asking the shopkeeper...");

                page.OpenPage(false);
                ClientState.Changed += OnSnapshot;
                Net.Request(RequestType.Sync, "");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not open the order menu: " + ex);
                if (page != null)
                {
                    UnityEngine.Object.Destroy(page.gameObject);
                }
                OnPageDestroyed();
            }
        }

        // A message under the Close button. It is our own text (not the game's label, whose box we don't control) that
        // wraps at the list width, and its row is as tall as the wrapped text needs, so nothing is ever cut off.
        private static void AddNote(string text)
        {
            page.AddElementToScrollView(scroll =>
            {
                GameObject go = new GameObject("Note", typeof(RectTransform));
                go.transform.SetParent(scroll, false);
                RectTransform rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(rowWidth, NoteMinHeight);
                // Like every MenuLib element: left edge at the scroll view's origin (with centre anchors it would start mid-list).
                rect.localPosition = Vector3.zero;

                note = CreateText(rect, textStyle, "Text", NoteFontSize, TextAlignmentOptions.TopLeft);
                Stretch(note.rectTransform);
                note.color = textStyle.color;
                note.enableWordWrapping = true;
                return rect;
            }, 0f, 8f);
            SetNote(text);
        }

        private static void SetNote(string text)
        {
            if (note == null || note.text == text)
            {
                return;
            }
            note.text = text;

            RectTransform rect = (RectTransform)note.transform.parent;
            float height;
            try
            {
                height = Mathf.Clamp(Mathf.Ceil(note.GetPreferredValues(text, rowWidth, 32767f).y) + 4f, NoteMinHeight, NoteMaxHeight);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug("Could not measure the note text, using two lines: " + ex.Message);
                height = NoteMinHeight * 2f;
            }
            if (Mathf.Abs(rect.sizeDelta.y - height) > 0.5f)
            {
                rect.sizeDelta = new Vector2(rowWidth, height);
                page.scrollView.UpdateElements();
            }
        }

        // A category heading with the titles of the two price columns on its right, so they stay visible as the list scrolls.
        private static void AddCategoryHeader(string title)
        {
            page.AddElementToScrollView(scroll =>
            {
                GameObject go = new GameObject("Category " + title, typeof(RectTransform));
                go.transform.SetParent(scroll, false);
                RectTransform rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(rowWidth, 34f);
                rect.localPosition = Vector3.zero;

                TextMeshProUGUI name = CreateText(rect, textStyle, "Title", 24f, TextAlignmentOptions.BottomLeft);
                Stretch(name.rectTransform);
                name.text = "<color=" + Gold + ">" + title + "</color>";

                TextMeshProUGUI deposit = CreateText(rect, textStyle, "Deposit title", 12f, TextAlignmentOptions.BottomRight);
                PlaceRight(deposit.rectTransform, RightPad + columnWidth, columnWidth);
                deposit.text = "<color=#C9B26B>DEPOSIT</color>";

                TextMeshProUGUI arrival = CreateText(rect, textStyle, "Arrival title", 12f, TextAlignmentOptions.BottomRight);
                PlaceRight(arrival.rectTransform, RightPad, columnWidth);
                arrival.text = "<color=#C9B26B>ARRIVAL</color>";
                return rect;
            }, 14f);
        }

        // Text that looks like the game's menu text (same font and glow), placed by the caller.
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

        // Pinned to the right edge of its parent, full height, `width` wide, `inset` away from the edge.
        private static void PlaceRight(RectTransform rect, float inset, float width)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            rect.anchoredPosition = new Vector2(-inset, 0f);
        }

        // A free-standing text block anchored by its top-left corner (used beside the list, outside the scroll view).
        // The text shrinks (down to minFontSize) to fit the box and is never replaced by "..."; a single-line block
        // shrinks to fit its width, a wrapping one to fit its height.
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
            if (singleLine)
            {
                text.enableWordWrapping = false;
                text.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                text.enableWordWrapping = true;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.color = new Color(0.92f, 0.92f, 0.92f);
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

        private static void OnSnapshot(ListingSnapshot s)
        {
            if (!IsOpen)
            {
                return;
            }

            busyUntil = 0f;
            page.headerTMP.text = "SPECIAL ORDERS   $" + SemiFunc.DollarGetString(s.Money) + "K";
            SetNote(ResultMessage(s) ?? ("Deposit now, the rest on arrival. Price +" + s.MarkupPercent + "%. Unbought orders are lost."));

            if (!built)
            {
                Build(s);
            }
            else
            {
                foreach (KeyValuePair<string, Row> pair in rows)
                {
                    int i = s.IndexOf(pair.Key);
                    Item item;
                    if (pair.Value.Button != null && i >= 0 && StatsManager.instance.itemDictionary.TryGetValue(pair.Key, out item))
                    {
                        ApplyRow(pair.Value, s, i, item);
                    }
                }
            }

            RefreshDetails();
        }

        private static void Build(ListingSnapshot s)
        {
            built = true;

            List<int>[] buckets = new List<int>[CategoryOrder.Length];
            for (int i = 0; i < s.Keys.Length; i++)
            {
                Item item;
                if (!StatsManager.instance.itemDictionary.TryGetValue(s.Keys[i], out item))
                {
                    continue;
                }
                int category = Array.IndexOf(CategoryOrder, item.itemType);
                if (category < 0)
                {
                    category = CategoryOrder.Length - 1;
                }
                if (buckets[category] == null)
                {
                    buckets[category] = new List<int>();
                }
                buckets[category].Add(i);
            }

            string firstKey = null;
            for (int c = 0; c < buckets.Length; c++)
            {
                List<int> indexes = buckets[c];
                if (indexes == null)
                {
                    continue;
                }
                indexes.Sort((a, b) => s.Prices[a] != s.Prices[b] ? s.Prices[a].CompareTo(s.Prices[b]) : string.CompareOrdinal(s.Keys[a], s.Keys[b]));

                AddCategoryHeader(CategoryName(CategoryOrder[c]));
                foreach (int i in indexes)
                {
                    string key = s.Keys[i];
                    if (firstKey == null)
                    {
                        firstKey = key;
                    }
                    Item item = StatsManager.instance.itemDictionary[key];
                    int index = i;
                    page.AddElementToScrollView(scroll =>
                    {
                        REPOButton button = MenuAPI.CreateREPOButton(Notifier.ItemName(item), () => OnRowClicked(key), scroll);
                        TextMeshProUGUI label = button.labelTMP;
                        label.enableAutoSizing = false;
                        label.enableWordWrapping = false;
                        label.overflowMode = TextOverflowModes.Ellipsis;
                        // Fixed width = the list width. Without it the button grows with its text and spills past the scroll area.
                        button.overrideButtonSize = new Vector2(rowWidth, button.GetLabelSize().y);

                        Row row = CreateRow(button);
                        rows[key] = row;
                        ApplyRow(row, s, index, item);
                        return button.rectTransform;
                    });
                }
            }

            try
            {
                BuildSidePanel();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not build the item panel: " + ex);
            }
            if (firstKey != null)
            {
                Select(firstKey);
            }
        }

        // The name is the button's own label; the deposit, the amount due on arrival and (for items that can't be ordered)
        // a status are separate texts on the right. Because they are separate, a long name is cut with "..." before it
        // reaches them instead of printing over them.
        private static Row CreateRow(REPOButton button)
        {
            TextMeshProUGUI name = button.labelTMP;
            Row row = new Row { Button = button };
            row.Deposit = CreateCell(button.transform, name, "Deposit", RightPad + columnWidth, columnWidth);
            row.Arrival = CreateCell(button.transform, name, "Arrival", RightPad, columnWidth);
            row.Span = CreateCell(button.transform, name, "Status", RightPad, columnWidth * 2f);

            Vector4 margin = name.margin;
            name.margin = new Vector4(margin.x, margin.y, 2f * columnWidth + RightPad + 14f, margin.w);
            return row;
        }

        private static TextMeshProUGUI CreateCell(Transform parent, TextMeshProUGUI name, string objectName, float inset, float width)
        {
            TextMeshProUGUI cell = CreateText(parent, name, objectName, name.fontSize, TextAlignmentOptions.MidlineRight);
            PlaceRight(cell.rectTransform, inset, width);
            cell.gameObject.AddComponent<ColorFollower>().source = name;
            return cell;
        }

        private static void ApplyRow(Row row, ListingSnapshot s, int i, Item item)
        {
            ItemState state = (ItemState)s.States[i];
            int deposit = s.Deposits[i];
            int rest = Math.Max(1, s.Prices[i] - deposit);

            string tint = null;
            string depositText = "";
            string arrivalText = "";
            string statusText = "";
            switch (state)
            {
                case ItemState.Available:
                    depositText = "$" + deposit + "K";
                    arrivalText = "$" + rest + "K";
                    break;
                case ItemState.Ordered:
                    tint = Gold;
                    depositText = "paid";
                    arrivalText = "$" + rest + "K";
                    break;
                case ItemState.Ready:
                    tint = Green;
                    depositText = "paid";
                    arrivalText = "$" + rest + "K";
                    break;
                default:
                    tint = Grey;
                    statusText = ShortReason(state, item);
                    break;
            }

            row.Button.labelTMP.text = Tint(RowName(item), tint);
            row.Deposit.text = Tint(depositText, tint);
            row.Arrival.text = Tint(arrivalText, tint);
            row.Span.text = Tint(statusText, tint);
        }

        private static string Tint(string text, string color)
        {
            return color == null || text.Length == 0 ? text : "<color=" + color + ">" + text + "</color>";
        }

        // The list is grouped by category, so "Health Upgrade" only needs to say "Health" under UPGRADES.
        private static string RowName(Item item)
        {
            string name = Notifier.ItemName(item);
            const string suffix = " Upgrade";
            if (item.itemType == SemiFunc.itemType.item_upgrade && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && name.Length > suffix.Length)
            {
                name = name.Substring(0, name.Length - suffix.Length);
            }
            return name;
        }

        // Places the preview and the item name/description to the right of the list, measured from the list's real
        // on-screen size, then centers the whole menu on the screen.
        private static void BuildSidePanel()
        {
            Transform host = page.rectTransform;
            Vector3[] corners = new Vector3[4];
            page.maskRectTransform.GetWorldCorners(corners);
            Vector3 maskTopLeft = host.InverseTransformPoint(corners[1]);
            Vector3 maskBottomLeft = host.InverseTransformPoint(corners[0]);
            float height = maskTopLeft.y - maskBottomLeft.y;

            page.scrollBarRectTransform.GetWorldCorners(corners);
            float left = host.InverseTransformPoint(corners[2]).x + height * 0.06f;

            // Starts a little above the list (only the list has the header over it) so the text below still fits on screen.
            float side = height * 0.56f * 1.3f;
            float top = maskTopLeft.y + height * 0.10f;

            if (Plugin.ShowItemPreview.Value)
            {
                try
                {
                    preview = ItemPreview.Create(host, page.menuPage, new Vector2(left, top - side), side, Plugin.PreviewBrightness.Value);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning("Item preview unavailable: " + ex.Message);
                    preview = null;
                }
            }

            // Below the preview: the item name (one line), then what a click does, down to the bottom of the list.
            float titleTop = top - side - height * 0.02f;
            float titleHeight = height * 0.11f;
            float detailTop = titleTop - titleHeight - height * 0.01f;
            float detailHeight = Mathf.Max(detailTop - maskBottomLeft.y, height * 0.15f);
            titleLabel = AddTextBlock(host, new Vector2(left, titleTop), side, titleHeight, 22f, 12f, true);
            detailLabel = AddTextBlock(host, new Vector2(left, detailTop), side, detailHeight, 16f, 11f, false);

            List<RectTransform> parts = new List<RectTransform>
            {
                page.maskRectTransform, page.scrollBarRectTransform, titleLabel.rectTransform, detailLabel.rectTransform,
            };
            Transform panel = host.Find("Panel");
            if (panel != null)
            {
                parts.Add(panel as RectTransform);
            }
            if (preview != null)
            {
                parts.Add(preview.Rect);
            }
            CenterOnScreen(parts);
        }

        // Moves the page content so the given parts, as a group, are centered on the screen (both ways).
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

                // The page slides in from above. If it has already started, work out where the group will be once it has landed.
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
                    Plugin.Log.LogWarning("Skipped centering the order menu: the measured shift " + shift.ToString("0.0") + " is implausible.");
                    return;
                }
                page.rectTransform.position += shift;
                Plugin.Log.LogDebug("Centered the order menu (shift " + shift.ToString("0.0") + ").");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not center the order menu: " + ex.Message);
            }
        }

        private static void Select(string key)
        {
            selectedKey = key;
            Item item;
            if (preview != null && StatsManager.instance.itemDictionary.TryGetValue(key, out item))
            {
                preview.Show(item);
            }
            RefreshDetails();
        }

        // The prices live in the list; this panel only names the item and says what a click does. Each line is short
        // (about 28 characters fit on a line) and there is room for two; the item's own description is left out because
        // it never fit beside the preview.
        private static void RefreshDetails()
        {
            ListingSnapshot s = ClientState.Current;
            if (s == null || titleLabel == null || detailLabel == null || selectedKey == null)
            {
                return;
            }
            int i = s.IndexOf(selectedKey);
            Item item;
            if (i < 0 || !StatsManager.instance.itemDictionary.TryGetValue(selectedKey, out item))
            {
                return;
            }

            ItemState state = (ItemState)s.States[i];
            int refund = Mathf.FloorToInt(s.Deposits[i] * s.RefundPercent / 100f);

            StringBuilder text = new StringBuilder();
            switch (state)
            {
                case ItemState.Available:
                    text.Append("<color=" + Gold + ">Click to order</color>\n");
                    text.Append("<color=#A0A0A0>Offered once, next visit.</color>");
                    break;
                case ItemState.Ordered:
                    text.Append("<color=" + Gold + ">Ordered - arrives next visit</color>\n");
                    text.Append("Click to cancel (refund $").Append(refund).Append("K)");
                    break;
                case ItemState.Ready:
                    text.Append("<color=" + Green + ">Waiting in the shop</color>\n");
                    text.Append("Buy it or the order is lost.");
                    break;
                default:
                    text.Append("<color=#A0A0A0>").Append(Reason(state, item)).Append("</color>");
                    break;
            }

            titleLabel.labelTMP.text = "<color=" + Gold + ">" + Notifier.ItemName(item) + "</color>";
            detailLabel.labelTMP.text = text.ToString();
        }

        private static void OnRowClicked(string key)
        {
            ListingSnapshot s = ClientState.Current;
            if (s == null || Time.unscaledTime < busyUntil)
            {
                return;
            }
            int i = s.IndexOf(key);
            if (i < 0)
            {
                return;
            }

            Select(key);
            ItemState state = (ItemState)s.States[i];
            if (state == ItemState.Available)
            {
                busyUntil = Time.unscaledTime + 2f;
                Net.Request(RequestType.Place, key);
            }
            else if (state == ItemState.Ordered)
            {
                busyUntil = Time.unscaledTime + 2f;
                Net.Request(RequestType.Cancel, key);
            }
            else
            {
                Item item;
                StatsManager.instance.itemDictionary.TryGetValue(key, out item);
                SetNote("Can't order that: " + Reason(state, item) + ".");
            }
        }

        private static string ShortReason(ItemState state, Item item)
        {
            switch (state)
            {
                case ItemState.InStock:
                    return "in stock";
                case ItemState.MaxOwned:
                    return "owned";
                case ItemState.MaxPurchased:
                    return "sold out";
                case ItemState.NeedsPlayers:
                    return item != null ? item.minPlayerCount + "+ players" : "more players";
                default:
                    return "unavailable";
            }
        }

        private static string Reason(ItemState state, Item item)
        {
            switch (state)
            {
                case ItemState.InStock:
                    return "It's already in stock in this shop";
                case ItemState.MaxOwned:
                    return "You already own the maximum";
                case ItemState.MaxPurchased:
                    return "Sold out for this run";
                case ItemState.NeedsPlayers:
                    return item != null ? "Needs " + item.minPlayerCount + " or more players" : "Needs more players";
                case ItemState.Ready:
                    return "Your order is already waiting in the shop";
                default:
                    return "Unavailable";
            }
        }

        private static string ResultMessage(ListingSnapshot s)
        {
            Item item;
            string name = StatsManager.instance.itemDictionary.TryGetValue(s.ResultKey, out item) ? Notifier.ItemName(item) : s.ResultKey;
            switch (s.Result)
            {
                case ResultCode.Placed:
                    return "Ordered: " + name + ". Arrives next visit.";
                case ResultCode.Cancelled:
                    return "Cancelled: " + name + ". Deposit refunded.";
                case ResultCode.NoMoney:
                    return "Not enough money for the deposit.";
                case ResultCode.Unavailable:
                    return "That can't be ordered right now.";
                case ResultCode.NotFound:
                    return "That item or order no longer exists.";
                case ResultCode.NotInShop:
                    return "Orders can only be placed in the shop.";
                default:
                    return null;
            }
        }

        private static string CategoryName(SemiFunc.itemType type)
        {
            switch (type)
            {
                case SemiFunc.itemType.gun: return "GUNS";
                case SemiFunc.itemType.melee: return "MELEE";
                case SemiFunc.itemType.grenade: return "GRENADES";
                case SemiFunc.itemType.mine: return "MINES";
                case SemiFunc.itemType.drone: return "DRONES";
                case SemiFunc.itemType.orb: return "ORBS";
                case SemiFunc.itemType.healthPack: return "HEALTH PACKS";
                case SemiFunc.itemType.item_upgrade: return "UPGRADES";
                case SemiFunc.itemType.player_upgrade: return "PLAYER UPGRADES";
                case SemiFunc.itemType.power_crystal: return "POWER CRYSTALS";
                case SemiFunc.itemType.tracker: return "TRACKERS";
                case SemiFunc.itemType.cart: return "CARTS";
                case SemiFunc.itemType.pocket_cart: return "POCKET CARTS";
                case SemiFunc.itemType.tool: return "TOOLS";
                case SemiFunc.itemType.launcher: return "LAUNCHERS";
                case SemiFunc.itemType.vehicle: return "VEHICLES";
                default: return "OTHER";
            }
        }

        private static void Tick()
        {
            if (!IsOpen)
            {
                return;
            }
            // Escape closes this page; without this the same key press would also open the pause menu.
            if (GameDirector.instance != null)
            {
                GameDirector.instance.SetDisableEscMenu(0.3f);
            }
            if (!SemiFunc.RunIsShop() || (!openedViaDebugHotkey && ShopkeeperTrigger.KeeperIsAsleep))
            {
                page.ClosePage(true);
                return;
            }
            if (!built && ClientState.Current == null && Time.unscaledTime - openedAt > 4f)
            {
                SetNote("No answer from the host. The host needs SpecialOrders too.");
            }

            // The item under the cursor is the one shown on the right; it stays selected when the cursor moves over the preview.
            foreach (KeyValuePair<string, Row> pair in rows)
            {
                REPOButton button = pair.Value.Button;
                if (button != null && button.menuButton != null && button.gameObject.activeInHierarchy && Refs.ButtonHovering(button.menuButton))
                {
                    if (pair.Key != selectedKey)
                    {
                        Select(pair.Key);
                    }
                    break;
                }
            }
        }

        private static void OnPageDestroyed()
        {
            ClientState.Changed -= OnSnapshot;
            page = null;
            note = null;
            titleLabel = null;
            detailLabel = null;
            preview = null;
            textStyle = null;
            rows.Clear();
            built = false;
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

        // The button tints its own label (grey, white on hover); the price/status texts next to it follow that colour.
        private sealed class ColorFollower : MonoBehaviour
        {
            internal TextMeshProUGUI source;
            private TextMeshProUGUI target;

            private void Awake()
            {
                target = GetComponent<TextMeshProUGUI>();
            }

            private void LateUpdate()
            {
                if (source != null && target != null)
                {
                    target.color = source.color;
                }
            }
        }
    }
}
