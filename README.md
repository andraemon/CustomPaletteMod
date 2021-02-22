# Custom Palette Mod

Custom Palette Mod repository.

Credit to nkrapivin for the installer code, and to krzys-h and co. for UndertaleModLib.

Download here: https://github.com/andraemon/CustomPaletteMod/releases/latest

Thank you and enjoy!

## Specifics
This mod adds a customizable palette to the game which is unlocked at 600000 cumulative gems. The name and all color values of this palette can be altered at will through the palette.ini file, which is located in the game's save directory. On Windows machines, this should be %localappdata%\Downwell_v1_0_5.

## Keybinds
- **Ctrl + R** - Randomizes all color values of the custom palette. Be warned: this will overwrite any color values you had previously specified in palette.ini.
- **Ctrl + P** - Refreshes the custom palette based on changes made to palette.ini so you don't have to restart your game every time you want to make a minor change. You must save palette.ini prior to using this keybind—otherwise nothing will change.

## Palette File Entries
- **name** - The name of the palette.
- **palettexy** - A value between 0 and 255 inclusive. x determines what type of object this will change, while y determines whether this value represents red, green or blue.
  - **x-values**
    - **0** - normal objects (bricks, the player, etc.)
    - **1** - bright objects (gems, dangerous enemies, etc.)
    - **2** - background
    - **3** - water (this is barely noticeable—it just affects some color highlights in aquifer and the boss' third stage)
  - **y-values**
    - **0** - red
    - **1** - green
    - **2** - blue
  - **normal, bright, background, and water** - These four are just here to remind you of which x-value corresponds to which class of objects, they don't have any mechanical effect.

