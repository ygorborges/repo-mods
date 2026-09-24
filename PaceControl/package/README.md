# Pace Control

How long a level takes comes down to one number: how much of the map you have to carry to the extraction points. The game works that out as a share of everything the level is worth, then splits it evenly between however many extraction points the level got - and it gets more of those the bigger it is, which is to say the further into a run you are. By level 15 that is five of them and most of the map on your back, and an evening gone.

This mod makes some of them **optional**. Every extraction point is still built, still works and still asks for exactly what it always asked for - the level just stops needing all of them. Ask for 3 of the 5 and the level is over after the third, with the two you skipped still standing there, unlocked, for anyone who wants to keep hauling for the money.

Nothing else about the level changes: same size, same rooms, same loot, same enemies, same difficulty curve. And what you carry is still what you earn - the quota is what you *must* bring in, never a cap.

**The level stays calm while you do them.** Finishing the last needed extraction normally flips the level into its endgame - a scare, every light in the map going out, enemies spawning between you and the truck and all of them coming for you. That is no way to go and do the extraction points this mod just left standing, so it is held back until the truck is actually pulling out. The full heal everyone gets in the truck, and anyone dead being revived there, hang off that same moment and still happen exactly as they always did.

## Settings

Edit them in `BepInEx/config/vibez.PaceControl.cfg` (or with REPOConfig). **The host's settings are the ones that count** - the host works out what a level needs and tells everyone else.

| Setting | Default | Meaning |
| --- | --- | --- |
| `General/Enabled` | true | Turns the whole mod on or off. Off gives you the game's own pacing back. |
| `Extractions/RequiredExtractions` | 0 | How many of a level's extraction points you have to complete before the level is done. 0 means all of them, the way the game plays normally (one more every time levels get bigger, reaching 5 by level 15). The ones past this number are optional, not missing. Asking for more than the level has changes nothing. |
| `General/HaulGoalPercent` | 100 | A further multiplier on what each extraction asks for, in percent (60 = every extraction wants 60% of what it otherwise would). Useful on its own if you would rather keep every extraction but make each one lighter. |

## Multiplayer

Everyone in the room should have this installed. The truck's healer decides entirely on its own machine whether the level is finished before it opens up to heal you, and so does the counter on your HUD - so a player without the mod would see the level end around them while their own truck never opened. The host is the one whose settings are used; the others are just told the number.

## A note on balance

The game's difficulty - bigger levels, worse enemies, the moon - climbs with how many levels you have **completed**, not with how much you have earned. Finish levels faster while bringing in less and you will meet that curve with fewer upgrades than usual. The optional extractions are exactly how you make that up on a night when you do have the time.

## Source and license

Source: [github.com/ygorborges/repo-mods](https://github.com/ygorborges/repo-mods/tree/main/PaceControl). Licensed under PolyForm Noncommercial 1.0.0.

Written with the help of AI (Claude), hence the *AI Generated* tag.
