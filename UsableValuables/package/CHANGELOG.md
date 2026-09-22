# Changelog

## 0.2.0

- New: the **dish sponge**, a valuable this mod adds (a yellow kitchen sponge with a green scrubbing layer, about 16 cm, light, hard to break, worth $100 to $300). It turns up in levels like any other small valuable. Moved (carried, shaken, thrown), it blows soap bubbles and drips, more the faster it goes; it stays quiet in the cart.
- The sponge needs every player in the room to have this mod (a player who cannot create a valuable the host spawned waits at the loading screen for ever), so the host only lets it into a level when everybody has said they have it. Nothing else in the mod needs that: the other features still work with only the host having it.
- The sponge's first pick-up plays a bubbling sound (`audio/bubbles.mp3`, made louder when it is loaded because it was recorded quietly) with a burst of bubbles for as long as it lasts; it blows more bubbles when moved; its drips are a short "plink" of water; a `sponge_drip.mp3` in the `audio` folder replaces them. It is made with a plain shader (the first version borrowed the shiny metal one of the valuable it is built on and came out dark and colourless).
- Fixed: holding a valuable the key works on made the game stutter. The hint ("Press E to ...") was being rebuilt every frame, and putting the key names into it asks the input system about every one of the game's key tags each time; it is now done once per text.
- New setting `WizardStaff/EnemyDamageMultiplier` (1 = the game's own damage, the default) to make the wizard staff's laser hurt enemies more. The mod never lowered that damage: it is a fixed number the game sets on the laser, however the laser is fired.
- The mod now needs [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) (the Mod Manager installs it). New `Sponge` section in the settings.

## 0.1.5

- New: four valuables that do nothing (or hardly anything) in the game now act up by themselves (no key involved).
- The **banana bow**: while somebody holds it there is a 3% chance each second that a bomb fuse (the game's own) starts burning; two seconds later it says "uh-oh" (your chance to react) and it blows up as that ends, with the game's own explosion, and is gone.
- The **handface**, held: a 15% chance each second to start laughing loudly (the mod's own laugh recording) and shaking like a cursed doll, which draws enemies while it goes on; it stops 2 seconds after it was let go of, or at once if it is put in the cart.
- The **handface**, not held: while a player moves within 5 metres it slowly turns to face them, faster and faster the closer it gets to the throw, without falling or leaving its place, and a creeping sound plays (fading in when the movement starts and out when it stops), drawing enemies. If the player keeps moving until the sound is over, the handface is flung at the nearest player, hurts them on impact, knocks them into ragdoll and laughs; that happens once, and after it the handface does nothing any more. It does nothing while held or in the cart. Between its two behaviours it rests for 4 seconds.
- The **horse** whinnies the first time somebody picks it up, and never again.
- The **piano**, held by anything but its keys, has a 10% chance each second to start playing the Lassan of Liszt's Hungarian Rhapsody No. 2 (Rachmaninoff's 1919 recording, public domain); it stops 2 seconds after it was let go of.
- When any of these goes off, whoever holds it (or a player within range of the creeping handface) gets the screen glitch the game shows when a trap goes off (the one you get when the TV turns on).
- The sounds are files in the new `audio` folder of the package. Everything is configurable (`BananaBow`, `Handface`, `Horse` and `Piano` sections).

## 0.1.4

- New: the arctic fan (the one that starts blowing as soon as you grab it). The key switches it off and on, like the ice saw, and it can switch itself back on while you hold it (`ReactivateChancePerSecond`, 30% by default).
- New: the radio, the gramophone, the television, the toy monkey and the grandfather clock. They are the game's one-shot traps (they go off once and never again). The key sets one off right away, and each second you hold one that has not gone off yet there is a chance it goes off by itself (`SelfStartChancePerSecond`, 30% by default, 0 = only with the key). Both use the game's own trap trigger, so the effect and the multiplayer sync are the game's.
- The default chance per second for all the trap-like valuables (the ones that switch themselves back on and the ones that go off by themselves) is now 30% instead of 50%. A config file made by an earlier version keeps its old value until you change it.

## 0.1.3

- New: the flamethrower and the fire extinguisher. In the game they are fired by grabbing their trigger and burn fuel until they are empty; now the key starts and stops the flames (or the spray) too, using the same fuel. They stop if you let go of the valuable while they are firing.

## 0.1.2

- The trap-like valuables (boombox, ice saw, blender, jackhammer, scream doll) can now switch themselves back on while you still hold them after you switched them off, the way the game's traps go off by themselves: every second there is a chance, 50% by default. Set `ReactivateChancePerSecond` in each one's section (0 = it stays off). The flashlight and the candle are not affected.

## 0.1.1

- New: the ice saw (the one that spins up as soon as you grab it, for example in McJannek Station) can be switched off and on with the key. So can the blender, the jackhammer and the scream doll, which start the same way.

## 0.1.0

- First version: hold a valuable that does something and press Interact (E) to use it.
- Switches: flashlight (light off/on), boombox (mute/play), candle (blow out/light).
- Actions: star wand (cast a spell), wizard staff (fire the laser), camera (flash), levitation potion (release the sphere), each with a configurable cooldown.
- Multiplayer: the host checks each request (you must be holding the valuable, it must be ready) and tells everyone.
