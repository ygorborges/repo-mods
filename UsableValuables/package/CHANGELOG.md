# Changelog

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
