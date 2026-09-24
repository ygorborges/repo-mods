# Changelog

## 1.0.0

- First full release. Everything below has been played through in game.
- The moon button now appears on its own without having to turn `LiveTuneSeconds` on. It used to be placed the instant the run switched to the truck, which is before the truck itself has actually loaded, so it was thrown away along with the old level and only the repeats a tuning interval made ever survived. It now waits for the level to really be up, and replaces the button if it ever goes missing.
- The menu's Close button actually closes the menu. The game's menus have no EventSystem-driven input at all - every one of its own buttons polls the mouse itself - so a plain Unity button there never received a single click, however right it looked.
- The preview panel is now a twin of the popup's own panel: the same size, beside it, instead of a smaller box floating next to a taller one. Close sits in a footer strip of its own rather than on top of the explanation text, and there is breathing room between the page title and the first row.
- The value bonus no longer eats into the level generator's own budget. The generator fills a level until its total value passes an allowance and then stops, so counting the bonus in meant a moon simply bought you FEWER valuables, largely cancelling itself out. Levels are now generated exactly as they would be with no moon at all, and everything in them is then worth the bonus more.
- The moon looks like a moon: craters are drawn in the sphere's own coordinates (so they stay round at the poles instead of smearing, and wrap across the seam), placed evenly over the surface, each with a sunlit rim and a shadowed floor, over a few large dark plains.
- The moon gives off a pale white glow, both on itself and as real light cast over the pedestal and the floor around it. New `ButtonGlow` setting to turn it up, down or off.
- The pedestal is brushed steel with panel seams, a bolted gold band, a polished collar under the moon and grime around its foot, instead of the near-black tube it was.
- `MenuScale` now defaults to 0.9, which suits the wider two-column menu.

## 0.1.0

- First version: the moon no longer rises on its own. It stays off until you grab the moon button that stands at a fixed spot in the truck (the same way you hold the extraction point's own button), capped by how many your run has actually unlocked (the same one-every-5-levels the game uses). Applying a moon turns on its real effects and adds 15% to every valuable's worth per level applied, added together.
- The moon button's position is a plain fixed world spot (`ButtonPositionX/Y/Z`) rather than an offset from a landmark in the truck - the truck's own level-number screen only shows anything once the next level has already started, so it was never reliably there to measure from during the intermission itself.
- Grabbing the button opens a menu that now stays open properly (an earlier build closed it again the instant it opened).
- Added `ButtonScale`, to size the button up while hunting for it and back down once you know where it is.
- Added `LiveTuneSeconds`: above 0, the button keeps re-placing itself on that interval while you're in the truck, so a position/scale tweak in REPOConfig shows up without leaving and coming back.
- The moon applied now persists with the run's own save (it used to live only in memory, so it read back as "none" the next time the game was reopened even mid-run).
- The panel that shows what is coming up (hold Tab in a level) now shows the moon actually applied through Moon Control - it used to show what the game's own automatic schedule would have picked, ignoring "none" or any other choice made here.
- The moon menu no longer sits squashed against the left edge of the screen with its text clipped - it is centered, and rows (with the "+X% valuables" suffix) wrap instead of getting cut off.
- Fixed the value bonus being applied twice to every valuable (the game rolls a valuable's price through the same method twice during level generation; the bonus was being multiplied in on both passes instead of just the first).
- The bonus per level is now 10% (was 15%).
- Only the host can pick a moon now - it used to also work for anyone who clicked a row, applying without the host's say. Everyone else's copy of the menu is read-only, still shows the current pick (with its icon, the same one the Tab indicator uses) and a "Random" row lets the host pick that instead of a specific moon.
- While holding a valuable priced under an applied moon, its floating price tag now shows the base price plus the bonus amount in red, instead of just the already-combined total.
- Added `MenuScale` (default 0.65), to shrink the whole popup - it was taking up most of the screen.
- The menu is now two columns instead of one long list: moons (and Random) on the left, and a preview of whatever row the mouse is over on the right - its icon, its real effects (the same text the game's own "a moon just changed" screen shows), and the value bonus, before you commit to picking it.
- Every row now stays exactly one line tall (the font shrinks to fit instead of wrapping), the preview panel has a background to read against instead of floating over the 3D scene, its title is centered, and it now spans the same height as the list instead of a smaller box within it.
- "Random" no longer rolls and applies a specific moon the instant you click it - it is now a standing choice (surviving between levels, same as any other pick) that keeps the actual moon a surprise: a fresh one is rolled among whatever is unlocked right as each level starts, so you only find out which by playing it, and it can come up differently level to level. Its preview shows every unlocked moon's icon side by side instead of one.
- The gap between the lines in the preview panel's effects list is smaller.
- The moon button's own moon looks more like a moon (proper craters with a raised rim, dark "maria" patches, a faint warm/cool tint instead of flat grey) and its pedestal is now a dark blue-grey brushed metal with a gold trim band, instead of plain grey.
