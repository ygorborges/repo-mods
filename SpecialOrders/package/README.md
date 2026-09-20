# Special Orders

Talk to the shopkeeper and order anything the shop doesn't have in stock. You pay a deposit now, and the item is waiting for you the next time you visit the shop - at a markup.

## How it works

1. In the shop, walk up to the shopkeeper robot (it has to be switched on) and press **Interact** while looking at it (the prompt shows the key). A list of every item in the game opens.
2. Point at an item to see a 3D preview on the right (hold the mouse button on the preview and drag to spin it). The list shows, for each item, the **deposit** you pay now and what you pay on **arrival**. Click the item to order it. The deposit is taken from your money immediately.
3. The next time you visit a shop, the item is on display like any other, priced at the order price **minus your deposit**.
4. Buy it the normal way (put it in the extraction point). An order is used up by its delivery: if you leave that shop without buying the item, the order is over, the item does not come back in the next shop (unless the game's own stock rolls it) and the deposit is not refunded.
5. Changed your mind before it arrives? Click an ordered item (shown in gold) to cancel and get your deposit back.

Items you can't order are greyed out with the reason: already in stock in this shop, you already own the maximum, sold out for this run, or the item needs more players.

Orders are saved with your run and disappear when a new run starts.

## Prices

| Setting | Default | Meaning |
| --- | --- | --- |
| `MarkupPercent` | 50 | How much more an ordered item costs than its normal shop price. |
| `DepositPercent` | 25 | Share of the order price paid up front. It is deducted from the price when the item arrives, and lost if you don't buy the item there. |
| `CancelRefundPercent` | 100 | Share of the deposit you get back when you cancel. |
| `InteractDistance` | 2 | How close, in meters, you have to be to the shopkeeper's body to place orders. You also have to look at it. |
| `ShowItemPreview` | true | Shows the rotating 3D preview of the selected item. |
| `PreviewBrightness` | 0.8 | Brightness of the preview lights. Lower it if the preview looks washed out. |
| `DebugHotkey` | None | For testing: opens the menu from anywhere in the shop, ignoring distance and whether the shopkeeper is switched on. |

Edit them in `BepInEx/config/vibez.SpecialOrders.cfg` (or with REPOConfig). In multiplayer only the host's prices are used.

## Multiplayer

The host must have the mod - orders, money and delivery are handled by the host. Everyone who wants to open the order menu needs it too. Players without the mod can't place orders, but ordered items are ordinary shop items for them.

## Limits

- One order per item at a time.
- Items sold only in the secret shop room can't be ordered.
- If the next shop has no free slot of the right size for an ordered item (or already stocks it), the order waits for the following shop; it is only used up once the item is actually put on display.

## Compatibility

Items added by other mods (for example through REPOLib) should show up in the list like any other item. Nothing is written into the game files.

This is an early version - please report anything odd, especially in multiplayer.

## Source and license

Source: [github.com/ygorborges/repo-mods](https://github.com/ygorborges/repo-mods/tree/main/SpecialOrders). Licensed under PolyForm Noncommercial 1.0.0.

Written with the help of AI (Claude), hence the *AI Generated* tag.
