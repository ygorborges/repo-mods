# Changelog

## 0.1.6

- New icon, in the style of the game: a low-poly Semibot peeking over a crate painted like the R.E.P.O. logo, eyeing a glowing deposit coin.
- No changes to how the mod works.

## 0.1.5

- Fixed text that was still cut off. The note under Close and the category headings started in the middle of the list, so the page's edge cut off the end of the note and hid the DEPOSIT / ARRIVAL column titles; they now start at the left edge like the rows.
- The details next to the preview no longer end in "...": the item name is bigger, the lines are shorter, and the text shrinks to fit its box instead of being cut. The item's own description was removed from this panel because it never fit next to the preview.

## 0.1.4

- **An order is used up by its delivery.** The item is offered in the shop it arrives in; if you leave without buying it, the order is over and it does not come back in the next shop (unless the game's own stock rolls it). The deposit is not refunded in that case. A pending order that could not be delivered yet (no free slot, item already in stock, ...) still waits, and can still be cancelled for a refund.
- The default deposit is now 25% of the order price (was 50%). Existing config files keep their own value; orders already placed keep the deposit they were placed with.
- The note under Close is now our own text whose row is as tall as the wrapped text needs, so it is never cut off (it was still cut in 0.1.3). It also says that unbought orders are lost.

## 0.1.3

- The list now has two price columns, **DEPOSIT** (paid now) and **ARRIVAL** (paid when the item arrives), instead of one order price. Ordered items show "paid" and what is left. The details next to the preview no longer repeat prices.
- The note under Close wraps instead of being cut off.
- The 3D preview is 30% larger.
- The whole menu is centered on the screen.
- "Needs more players" rows now say how many ("3+ players").

## 0.1.2

- Interaction is now predictable: the shopkeeper prompt appears whenever you are within `InteractDistance` of its body and looking at it. Before, it was a physics trigger with dead spots (for example right under the robot) and a zone that was too small to be reliable. The reach is slightly larger than in 0.1.1.
- List rows no longer overlap: the name is cut with "..." before the price/status column, which is now right-aligned and never truncated. "Upgrade" is dropped from names under the UPGRADES heading.
- The details panel fits on screen (title on one line, compact details, long descriptions are cut with "...").
- Preview lights are dimmer by default; new `PreviewBrightness` setting.
- No longer skips the prompt while you hold an item or are spectating.

## 0.1.1

- The order list no longer runs off the page: rows are short with a fixed width and a price column, and the details moved to a panel on the right.
- New: details of the item under the cursor plus a rotating 3D preview (drag with the mouse to spin it). Can be turned off with `ShowItemPreview`.
- The shopkeeper can only be used while it is switched on, and the menu closes if it is switched off.
- The interaction zone is much smaller: you have to stand close (`InteractDistance`, default 2 m).
- The F8 hotkey is now `DebugHotkey` and off by default.

## 0.1.0

- First version: order any shop item from the shopkeeper for a deposit; it is delivered to the next shop at a markup.
- Order menu built with MenuLib, opened by looking at the shopkeeper and pressing Interact (or with a hotkey).
- Orders are saved with the run and reset with a new run.
- Multiplayer: host-authoritative, clients ask the host over Photon events.
