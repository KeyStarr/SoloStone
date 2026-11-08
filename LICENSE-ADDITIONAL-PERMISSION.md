# Additional permission for SoloStone's own files

This is **not** the main licence. The main licence is [LICENSE](LICENSE), the GNU Affero General Public License
version 3, unmodified. This file only adds a permission on top of it, for SoloStone's own files.

## Which files

This permission covers the files in these directories, and nothing else:

- `plugin/`
- `installer/`

It does **not** cover anything under `engine/` (the SabberStone rules engine and our changes to it), anything
listed in [NOTICE](NOTICE) as someone else's work, or the licence texts. Those stay under their own terms.

## The permission

As an additional permission under section 7 of the GNU Affero General Public License version 3, the copyright
holders of the files above give you permission to link or combine those files with the **Game Libraries**, and to
convey the result, without the Game Libraries having to be licensed under the AGPL or offered as Corresponding
Source.

The Game Libraries are:

1. the program libraries of the Hearthstone client you run the mod with (`Assembly-CSharp.dll` and the
   `blizzard.*` and `Blizzard.T5.*` libraries);
2. the Unity engine libraries (`UnityEngine*.dll`);
3. the Mono and .NET class libraries that the game or the mod loader provide;
4. the mod loader and its patching library (BepInEx, Unity Doorstop, Harmony).

You must still follow the AGPL for the covered files and for any part of the combination that is not a Game
Library. This permission does not give you any right in the Game Libraries themselves, and it does not change
anything in the licences of other people's files.

If you modify a covered file, you may keep this permission for your version or remove it. If you remove it, remove
it for your whole version of that file.
