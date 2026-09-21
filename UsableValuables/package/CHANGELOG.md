# Changelog

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
