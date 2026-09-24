# Moon Control

The game raises the moon on its own, one step every 5 levels, and once it does the moon never goes back down for the rest of the run - it makes things progressively harder (and stranger) whether you want that or not. This mod takes that away: **the moon stays off until you turn one on yourself**, by grabbing the moon button in the truck, and only a moon your progress has actually earned can be chosen. Turning one on also gives every valuable a bonus to what it is worth, so choosing a harder moon is a choice, not just something that happens to you.

## The moon button

A little moon on a pedestal stands at a fixed spot in the truck. Grab it - the same way you hold the extraction point's own button - and a menu opens: a list on the left with every moon your run has unlocked so far ("none", the game's own baseline; each moon level you have reached, one every 5 levels completed; and "Random"), and a preview on the right showing whatever row your mouse is over - its icon, its real effects (the same descriptions the game's own "a moon just changed" screen shows), and the value bonus it gives, so you can see what a moon does before picking it. Click a row to apply it. Only the host can pick - everyone else's copy of the menu is read-only, and still shows the current pick and follows whatever the host chooses.

**"Random" is a standing choice, not an instant roll.** Picking it does not immediately apply some moon - it keeps things a surprise: a moon is rolled among whatever is unlocked right as each level starts, a different one potentially each time, and you only find out which by playing it. It stays selected (surviving between levels, and even the game being closed and reopened) until you pick something else.

Applying a moon turns on everything the game itself ties to that moon level - the real thing, not an imitation of it - and adds to every valuable's worth: **10% per level, added together** (moon 2 is +20%, moon 3 is +30%, and so on). Both take effect for what you find in the very next level. While holding a valuable that got a bonus, its price tag shows the base price plus the bonus amount in red, instead of just the combined total.

## Multiplayer

Only the host can choose a moon - clients can open the menu and look, but picking does nothing for them; the menu still shows whatever the host has applied. Everyone needs the mod for their own moon to stay in step with the host's (without it, a player's own game keeps rising the moon on its own on their machine) and to see the moon button at all (it is a real networked object, spawned by the host - a player without the mod simply will not have it to grab); the value bonus reaches everyone regardless, since the host's price is what is sent out.

## Settings

Edit them in `BepInEx/config/vibez.MoonControl.cfg` (or with REPOConfig).

| Setting | Default | Meaning |
| --- | --- | --- |
| `General/Enabled` | true | Turns the whole mod on or off. Off gives you the game's own moon behaviour back (it rises on its own, permanently, every 5 levels), and the moon button does not appear. |
| `General/ValueBonusPercent` | 10 | How much applying a moon adds to what every valuable is worth, in percent per level applied, added together. Only the host's setting is used. |
| `General/ButtonPositionX`, `ButtonPositionY`, `ButtonPositionZ` | -13.40, 0.69, -1.90 | The moon button's fixed world position in the truck. Only the host's setting is used (the host is the one who places it). The default is where the button sits by default; if the truck ever looks different, or you just prefer another spot, find a new one with DevTools' coordinate probe (aim at the floor spot you want and read its "World XYZ" off the on-screen readout). |
| `General/ButtonYawDegrees` | 0 | Which way the moon button faces, in degrees around the vertical axis. Only cosmetic. |
| `General/ButtonScale` | 1.8 | How big the moon button is, as a multiplier of its normal size. Turn it way up (5, 10...) while hunting for it so it is impossible to miss, then bring it back down once you know where it is. Grabbing works at any size. Host-only display - other players in multiplayer always see it at normal size. A changed value only applies the next time the button is placed. |
| `General/LiveTuneSeconds` | 0 | Debug convenience: above 0, the button keeps re-placing itself on this interval (seconds) while you stay in the truck, picking up any position/yaw/scale change made through REPOConfig without leaving and coming back. 0 = off (placed once per visit, the normal behaviour). Leave it at 0 outside of a tuning session - re-placing pulls the button out from under anyone holding it. |
| `General/MenuScale` | 0.65 | How big the moon menu popup is, as a multiplier of its normal size (1 = the size MenuLib gives it). Takes effect the next time you open the menu. |

## Notes

- This is an early version - please report anything odd, especially in multiplayer.

## Source and license

Source: [github.com/ygorborges/repo-mods](https://github.com/ygorborges/repo-mods/tree/main/MoonControl). Licensed under PolyForm Noncommercial 1.0.0.

Written with the help of AI (Claude), hence the *AI Generated* tag.
