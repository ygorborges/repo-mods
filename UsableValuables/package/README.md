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

Only the valuables listed on this page are affected. Nothing is written into the game files.

## Valuables that act up by themselves

Four valuables do nothing at all in the game (the piano does, but only when you grab its keys). With this mod they misbehave, with no key involved. When one of them goes off, whoever holds it (or, for the creeping handface, a player within range) also gets the screen glitch the game shows when a trap goes off, the one you get when the TV turns on.

| Valuable | What happens |
| --- | --- |
| Banana Bow | Each second you hold it there is a small chance (3% by default) that a bomb fuse (the game's own, from the Bang enemy) starts burning. Two seconds later it says "uh-oh", which is your chance to react, and about a second after that it blows up and is gone. The explosion is the game's own (like the barrel's): it hurts players and enemies and throws things around. Once the fuse is lit, holding on or letting go makes no difference. |
| Handface (held) | Each second you hold it there is a chance (15% by default) that it starts laughing, loudly, and shaking like a cursed doll, with short harder spasms, which draws enemies within about 35 metres every second. It keeps going for as long as somebody holds it, and stops 2 seconds after it was let go of (pick it up again in time and it keeps going), or at once if it is put in the cart. |
| Handface (not held) | While a player moves near it (5 metres by default) it slowly turns to face them, faster and faster the closer it gets to the throw, without falling or leaving its place, and a creeping sound plays, fading in when the movement starts and out when it stops, and the noise draws enemies. If the player keeps moving until the sound is over (about 10 seconds), the handface is flung at the nearest player, hurts them on impact (20 damage by default), knocks them into ragdoll (for 2 seconds by default) and laughs. That happens once: after it, the handface does nothing any more. Stop moving before then and the sound fades out and starts over next time. It does nothing while somebody holds it or while it is in the cart. Between its two behaviours it rests for 4 seconds: after it stops laughing it cannot start creeping, and after it stops creeping it cannot start laughing, for that long. |
| Horse | It whinnies the first time somebody picks it up, and never again. |
| Piano | While somebody holds it by anything but its keys there is a chance (10% by default) each second that it starts playing a song by itself: the slow part (the Lassan) of Liszt's Hungarian Rhapsody No. 2, in Rachmaninoff's 1919 recording. The song plays for as long as the piano is held, and stops 2 seconds after it was let go of. Grab the keys and it plays notes as usual. |

## Multiplayer

The **host must have the mod**: it checks every request (you have to be holding the valuable, and it has to be ready again) and tells everyone what happened. Everyone else needs it to press the key, and to *see and hear* the flashlight, boombox and candle switches, the camera flash and the potion. The star wand, the staff and the ice saw, blender, jackhammer, scream doll, fan, flamethrower and fire extinguisher work through the game's own networking (the host runs them), so everybody sees them. So do the one-shot traps (radio, gramophone, television, toy monkey, grandfather clock): the host sets them off with the game's own trap trigger. For the banana bow and the handface the host rolls the dice, shakes the handface and alerts the enemies, and removes the banana bow; the fuse, the "uh-oh", the laugh, the creeping sound, the horse's whinny, the piano's song, the screen glitch and the explosion effect are played on each player's own machine, so a player without the mod does not hear or see those (they do see the handface shake, turn and fly, and the banana bow disappear). The impact of the flung handface is checked on each player's own machine against that player, as the game does for its own hazards.

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
| `BananaBow/Enabled` | true | Whether a held banana bow can light its fuse and blow up. |
| `BananaBow/ExplodeChancePerSecond` | 3 | Chance, in percent, that a held banana bow lights its fuse each second. |
| `BananaBow/WindupSeconds` | 2 | Seconds the fuse burns before the "uh-oh"; the explosion follows as it ends. |
| `BananaBow/ExplosionSize`, `PlayerDamage`, `EnemyDamage` | 1, 50, 100 | The explosion's size and its damage to players and to enemies (the game's barrel: 1, 50, 100). |
| `BananaBow/WarningVolume`, `WarningFalloff` | 1, 1.5 | How loud the "uh-oh" is (0-1) and how far it carries, on your machine. |
| `Handface/Enabled` | true | Whether a held handface can start laughing and shaking. |
| `Handface/ActivateChancePerSecond` | 15 | Chance, in percent, that a held handface starts laughing and shaking each second. |
| `Handface/StopSecondsAfterDrop` | 2 | Seconds after it was let go of before it stops. |
| `Handface/ShakeStrength` | 1 | How hard it shakes, held or not: 1 = a cursed doll's convulsions (about eight times the game's toy monkey), 0.25 = a gentle tremble, 0 = none. |
| `Handface/AlertRange` | 35 | Metres around it within which enemies are drawn to the noise while it shakes or creeps. 0 = none. |
| `Handface/LaughVolume`, `LaughFalloff` | 1, 2 | How loud the laugh is (0-1) and how far it carries (1 = like the game's sounds), on your machine. |
| `Handface/TwitchEnabled` | true | Whether a handface nobody holds twitches and creeps when a player moves near it. |
| `Handface/TwitchRange` | 5 | Metres within which a moving player sets it off. |
| `Handface/TwitchTurn` | 1 | How fast it turns to face the player while it creeps: 1 = slowly at first and quicker and quicker until the throw (15 to 240 degrees per second), 0 = it does not turn. |
| `Handface/FrontOffsetDegrees` | 0 | Which side of the handface is its front, the side that turns to face the player: 0 = the way the model faces, 90, 180 or 270 = a quarter, half or three quarters of a turn further round. Change it if the wrong side ends up facing you. |
| `Handface/LaunchSpeed`, `LaunchDamage` | 14, 20 | Speed (m/s) at which it is flung at the player, and the damage it does on impact. |
| `Handface/RagdollSeconds`, `RagdollForce` | 2, 10 | How long the player it hits stays in ragdoll (0 = none) and how hard the hit shoves them along the way the hand was flying (0 = they just fall). |
| `Handface/BehaviourCooldownSeconds` | 4 | The rest between the handface's two behaviours (laughing when held, creeping when not): after one stops, the other cannot start for this many seconds. |
| `Handface/TwitchVolume`, `TwitchFalloff` | 1, 1.5 | How loud the creeping sound is (0-1) and how far it carries, on your machine. |
| `BananaBow/ScreenGlitch`, `Handface/ScreenGlitch`, `Horse/ScreenGlitch`, `Piano/ScreenGlitch` | true | Whether the trap-style screen glitch plays on your machine when that valuable goes off. |
| `Horse/Enabled` | true | Whether the horse whinnies when picked up. |
| `Horse/Volume`, `Horse/Falloff` | 1, 1.5 | How loud the whinny is (0-1) and how far it carries, on your machine. |
| `Piano/Enabled` | true | Whether a piano held by anything but its keys can start its song. |
| `Piano/StartChancePerSecond` | 10 | Chance, in percent, that it starts its song each second. |
| `Piano/StopSecondsAfterDrop` | 2 | Seconds after it was let go of before the song stops. |
| `Piano/Volume`, `Piano/Falloff` | 0.8, 1.5 | How loud the song is (0-1) and how far it carries, on your machine. |
| `StarWand/CooldownSeconds` | 1.5 | Seconds between casts. |
| `WizardStaff/CooldownSeconds` | 3 | Seconds between shots. |
| `Camera/CooldownSeconds` | 6 | Seconds between flashes. |
| `LevitationPotion/CooldownSeconds` | 30 | Seconds between uses. |

## Notes

- The key is the game's Interact key, so it follows your key bindings.
- This is an early version - please report anything odd, especially in multiplayer.

## Credits

The piano's song is Sergei Rachmaninoff's 1919 Edison recording of Liszt's Second Hungarian Rhapsody, part I (Edison Diamond Disc 82169-R), from the Library of Congress via [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Second_Hungarian_Rhapsody.ogg) (the MP3 version Commons provides). It is in the public domain in the United States (a sound recording first published before 1926); Liszt died in 1886.

## Source and license

Source: [github.com/ygorborges/repo-mods](https://github.com/ygorborges/repo-mods/tree/main/UsableValuables). Licensed under PolyForm Noncommercial 1.0.0.

Written with the help of AI (Claude), hence the *AI Generated* tag.
