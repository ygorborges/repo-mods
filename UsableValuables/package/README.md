# Usable Valuables

Some valuables do things - the flashlight lights up, the ice saw spins, the boombox plays music, the star wand fires spells - but only on their own terms: when you grab them, swing them or break them. With this mod you can **hold one and press Interact (E)** to use it.

While you hold a valuable the key works on, a hint shows what it will do.

## What the key does

| Valuable | Press E to |
| --- | --- |
| Flashlight | Switch the light off and on. It still lights up by itself when you pick it up. |
| Boombox | Mute it and start it again. Muted, it makes no music, you don't dance and it doesn't draw enemies. |
| Candle | Blow the flame out and light it again. |
| Ice Saw | Switch it off and on. It still spins up by itself when you pick it up. |
| Blender | Switch it off and on, the same way. |
| Jackhammer | Switch it off and on, the same way. |
| Scream Doll | Silence it, and wake it again. |
| Fan | Switch it off and on. It still starts blowing by itself when you pick it up. |
| Radio | Turn it on at once: the light flickers on and it blares, drawing enemies. |
| Gramophone | Start it at once: the record spins and the music plays, drawing enemies. |
| Television | Switch it on at once: static, then the cat and the mouse cartoon, drawing enemies. |
| Toy Monkey | Wind it up at once: it bangs its cymbals and hops about, drawing enemies. |
| Grandfather Clock | Ring it at once: the bell sounds, the screen shakes, the pendulum speeds up and enemies come. |
| Flamethrower | Start the flames and stop them again. They burn the same fuel the trigger does, so it still runs dry; let go of the valuable and they stop. |
| Fire Extinguisher | Start the spray and stop it again, the same way. |
| Star Wand | Cast a spell, as if you had swung it. Like a swing, every cast costs the wand part of its value. |
| Wizard Staff | Fire its laser. |
| Camera | Set off the flash (the stunning burst it gives when it takes damage). |
| Levitation Potion | Release the levitation sphere, without smashing the potion. |

Anything you switch off goes back to normal when you let go of it (it starts by itself again the next time you pick it up); the candle stays as you left it.

**Traps don't stay off.** The boombox, ice saw, blender, jackhammer, scream doll and fan are hazards in the game, and one you switched off can switch itself back on while you are still holding it: every second there is a chance that it does (30% by default, see the settings). The flashlight and the candle are not traps and stay as you left them.

**The radio, gramophone, television, toy monkey and grandfather clock are one-shot traps.** In the game they go off once, when the dice say so while somebody holds them, and never again. With this mod, each second you hold one that has not gone off yet there is a chance it goes off by itself (30% by default, see the settings), and E sets it off right away; either way it uses the game's own trap trigger, so the effect is the game's, your screen glitches as it does in the game, and everybody sees it. Once one has gone off the hint disappears, because there is nothing left to press. Set the chance to 0 and they only go off when you press E (or when the game's own dice fire).

Only the valuables listed above are affected. Nothing is written into the game files.

## Multiplayer

The **host must have the mod**: it checks every request (you have to be holding the valuable, and it has to be ready again) and tells everyone what happened. Everyone else needs it to press the key, and to *see and hear* the flashlight, boombox and candle switches, the camera flash and the potion. The star wand, the staff and the ice saw, blender, jackhammer, scream doll, fan, flamethrower and fire extinguisher work through the game's own networking (the host runs them), so everybody sees them. So do the one-shot traps (radio, gramophone, television, toy monkey, grandfather clock): the host sets them off with the game's own trap trigger.

Only the host's settings are used.

## Settings

Edit them in `BepInEx/config/vibez.UsableValuables.cfg` (or with REPOConfig).

| Setting | Default | Meaning |
| --- | --- | --- |
| `General/Enabled` | true | Turns the whole mod on or off. |
| `General/ShowPrompt` | true | Shows the "Press E to ..." hint while you hold a usable valuable. |
| `<Valuable>/Enabled` | true | Whether the key works on that valuable (Flashlight, Boombox, Candle, IceSaw, Blender, Jackhammer, ScreamDoll, Fan, Radio, Gramophone, Television, ToyMonkey, GrandfatherClock, Flamethrower, FireExtinguisher, StarWand, WizardStaff, Camera, LevitationPotion). |
| `<Trap>/ReactivateChancePerSecond` | 30 | For Boombox, IceSaw, Blender, Jackhammer, ScreamDoll and Fan: the chance, in percent, that a switched-off one switches itself back on each second you keep holding it. 0 = it stays off. |
| `<OneShotTrap>/SelfStartChancePerSecond` | 30 | For Radio, Gramophone, Television, ToyMonkey and GrandfatherClock: the chance, in percent, that one you hold and that has not gone off yet goes off by itself each second. It only ever goes off once. 0 = only with the key. |
| `StarWand/CooldownSeconds` | 1.5 | Seconds between casts. |
| `WizardStaff/CooldownSeconds` | 3 | Seconds between shots. |
| `Camera/CooldownSeconds` | 6 | Seconds between flashes. |
| `LevitationPotion/CooldownSeconds` | 30 | Seconds between uses. |

## Notes

- The key is the game's Interact key, so it follows your key bindings.
- This is an early version - please report anything odd, especially in multiplayer.

## Source and license

Source: [github.com/ygorborges/repo-mods](https://github.com/ygorborges/repo-mods/tree/main/UsableValuables). Licensed under PolyForm Noncommercial 1.0.0.

Written with the help of AI (Claude), hence the *AI Generated* tag.
