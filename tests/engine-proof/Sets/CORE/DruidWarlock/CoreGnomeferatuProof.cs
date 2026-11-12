using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreGnomeferatuProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ICC_407", (g, p, id) => {
        var top=p.Opponent.DeckZone.TopCard; int deck=p.Opponent.DeckZone.Count; int own=p.DeckZone.Count;
        Play(g,p,id);
        Core2026Proof.Check(top.Zone.Type==Zone.GRAVEYARD && p.Opponent.DeckZone.Count==deck-1 && p.DeckZone.Count==own && p.Opponent.HandZone.Count==0,"remove exact opponent top card without drawing or touching own deck");
        Summon(p,id); Core2026Proof.Check(p.Opponent.DeckZone.Count==deck-1,"summoning skips battlecry");
        PoolPilotProof.ClassDeck(p.Opponent); Play(g,p,id);
        Core2026Proof.Check(p.Opponent.DeckZone.Count==0 && p.Opponent.Hero.Fatigue==0 && p.Opponent.Hero.Damage==0 && p.DeckZone.Count==own,"empty opponent deck neither fatigues nor falls back to own deck");
    }, legacyPoolMember: true);
}
