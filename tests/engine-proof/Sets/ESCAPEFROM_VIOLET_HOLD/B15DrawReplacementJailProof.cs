using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static B15DrawReplacementProof;

internal static class B15DrawReplacementJailProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_398", Unpinned_ImpfernalOffboard);
    private static void Unpinned_ImpfernalOffboard(Game g, Controller p, string id)
    {
        var card = Add(p, id);
        Generic.DiscardBlock(p, card);
        Core2026Proof.Check(p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 3 && card.Zone == p.GraveyardZone,
            "pinned discard triggers offboard IMPFERNAL once");
        foreach (bool deck in new[] { false, true })
        {
            var fresh = Entity.FromCard(p, Cards.FromId(id), zone: deck ? p.DeckZone : p.HandZone);
            int before = p.Hero.Damage; fresh.Destroy(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(p.Hero.Damage == before + 3 && fresh.Zone == p.GraveyardZone,
                "Unpinned_ImpfernalExplicitDestroy qualifies hand/deck exactly once");
        }
        p.Hero.Damage = p.Opponent.Hero.Damage = 0;
        var removed = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
        p.SetasideZone.Add(p.DeckZone.Remove(removed));
        Core2026Proof.Check(p.Hero.Damage == 0, "non-destructive deck removal never triggers");
        foreach (bool backlog in new[] { false, true })
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var owner = next.Player1;
            while (!owner.HandZone.IsFull) Add(owner, "CS2_182");
            if (backlog) Enchantment.GetInstance(owner, owner.Hero, owner, Cards.FromId("JAIL_509e"));
            var burned = Entity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone);
            int damage = owner.Hero.Damage, deaths = next.HistoryFacts.Query.Deaths().Count();
            Generic.Draw(owner, burned); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(owner.Hero.Damage == damage + 3 && owner.Opponent.Hero.Damage == damage + 3
                && burned.Zone.Type == (backlog ? Zone.SETASIDE : Zone.GRAVEYARD)
                && next.HistoryFacts.Query.Deaths().Count() == deaths,
                "Unpinned_ImpfernalOverdraw triggers once for ordinary/backlog burn without a board death");
        }
        var board = Summon(p, id);
        Kill(g, board);
        Core2026Proof.Check(p.Hero.Damage == 3, "board Deathrattle uses same all-other-character damage");
        p.Hero.Damage = p.Opponent.Hero.Damage = 0;
        board = Summon(p, id); board.Silence(); Kill(g, board);
        Core2026Proof.Check(p.Hero.Damage == 0, "Silenced board IMPFERNAL has no Deathrattle");
        Unpinned_ImpfernalExistingDestroyPaths();
        var ordinary = Add(p, "EX1_029"); Generic.DiscardBlock(p, ordinary);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "ordinary discarded Deathrattles do not activate");
    }
    private static void Unpinned_ImpfernalExistingDestroyPaths()
    {
        foreach (int source in new[] { 121199, 122635, 121160 })
        {
            var next = NewGame(); var owner = next.Player1;
            var victim = Entity.FromCard(owner.Opponent, Cards.FromId("JAIL_398"),
                zone: source == 121160 ? owner.Opponent.HandZone : owner.Opponent.DeckZone);
            if (source == 121199) EmptyDeck(owner);
            if (source == 122635) victim.Cost = 2;
            if (source == 121160) Kill(next, Summon(owner, Cards.FromAssetId(source).Id));
            else Play(next, owner, Cards.FromAssetId(source).Id);
            Core2026Proof.Check(owner.Hero.Damage == 3 && owner.Opponent.Hero.Damage == 3
                && victim.Zone == owner.Opponent.GraveyardZone,
                "Unpinned_Impfernal existing explicit hand/deck destroy path: " + source);
        }
    }
}
