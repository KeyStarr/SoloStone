using EngineZone = SabberStoneCore.Enums.Zone;

// The one rule for which entities a history replay shows the human: a minion or card in play, or a card in the human's hand.
// NativeMatch (at a replay position), the aura transport bridge (at the engine's current state) and the match event log (to stay
// consistent with what the client is sent) each supply the zone and controller from their own source and share this predicate.
internal static class ReplayVisibility
{
    internal static bool IsPublicPlace(EngineZone zone, int controller, int humanPlayer) =>
        zone == EngineZone.PLAY || zone == EngineZone.HAND && controller == humanPlayer;
}
