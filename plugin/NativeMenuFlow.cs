internal enum NativeHomeControl
{
    Unsupported,
    Traditional,
    Arena,
    Collection,
    OpenPacks,
    Store,
    Modes
}

internal enum NativeMenuTarget
{
    None,
    Picker,
    Arena,
    Collection,
    Pack,
    // The local shop already opened the store (NativeShop fires the STORE press itself): nothing to route or block.
    LocalShop,
    // SoloStone's modes (Dungeon Run, ...) in the client's Modes screen (NativeModesMenu).
    Modes
}

internal static class NativeMenuFlow
{
    internal static NativeMenuTarget HomeRoute(NativeHomeControl control)
    {
        if (control == NativeHomeControl.Arena) return NativeMenuTarget.Arena;
        if (control == NativeHomeControl.Traditional) return NativeMenuTarget.Picker;
        if (control == NativeHomeControl.Collection) return NativeMenuTarget.Collection;
        if (control == NativeHomeControl.OpenPacks) return NativeMenuTarget.Pack;
        if (control == NativeHomeControl.Store) return NativeMenuTarget.LocalShop;
        if (control == NativeHomeControl.Modes) return NativeMenuTarget.Modes;
        return NativeMenuTarget.None;
    }

    internal static bool CollectionExitAllowed(bool loggedIn, bool localActive, bool pickerOrigin) =>
        loggedIn || (localActive && pickerOrigin);

    internal static bool FatalPackDisconnectReturnsHome(bool replayActive) => !replayActive;
}
