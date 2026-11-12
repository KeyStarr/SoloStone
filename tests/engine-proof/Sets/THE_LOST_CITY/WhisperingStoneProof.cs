using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class WhisperingStoneProof
{
    // Eye Beam's printed Outcast aura sets its Cost to (1) at either end of the hand; other Fel spells keep printed Cost.
    private static int Printed(IPlayable card) => card.Card.Id == "CORE_BT_801" && card.Zone == card.Controller.HandZone
        && (card.ZonePosition == 0 || card.ZonePosition == card.Controller.HandZone.Count - 1) ? 1 : card.Card.Cost;
    internal static void Run() => StandardSetsProof.Case("TLC_467", (g, p, id) => {
        Definition("TLC_467e");
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        var report = OfflinePoolAdmission.Evaluate(g.OfflinePools, new[] { declaration }); report.ValidateRegistered();
        Core2026Proof.Check(report.Cards.Single().PoolRequirements.Single().Count >= 3, "admitted Fel spells meet threshold");
        if (ClientExport.Oracle)
            Core2026Proof.Check(OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId).Select(c => c.Id).ToHashSet()
                .SetEquals(OfflinePoolExpectations.Ids(Filter(id),p.HeroClass.ToString(),Cards.FromId(id).AssetId)), "admitted Fel identities match eligibility/export");
        else ClientExport.Skip("Whispering Stone admitted identities against the independent derivation from the client export");
        var source = Summon(p, id); Core2026Proof.Check(source.HasTaunt && source.Health == 8 && source.AttackDamage == 0, "0/8 Taunt");
        var quiet = g.CloneExact(history: false); Kill(quiet, (Minion)quiet.IdEntityDic[source.Id]);
        Generated(quiet, (Minion)quiet.IdEntityDic[source.Id], quiet.Player1.HandZone, 2);
        foreach (var card in quiet.Player1.HandZone) Visible(card, Printed(card), CostPaymentKind.Health);
        End(quiet);
        Core2026Proof.Check(quiet.Player1.HandZone.Count == 2, "History=false retains both Fel spells across turns");
        foreach (var card in quiet.Player1.HandZone) Visible(card, Printed(card), CostPaymentKind.Health);
        var before = g.Random.DrawCount;
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        Core2026Proof.Check(p.HandZone.Count == 2 && g.Random.DrawCount - before == 2, "Deathrattle two independent picks");
        foreach (var card in p.HandZone) {
            Visible(card, Printed(card), CostPaymentKind.Health);
            Core2026Proof.Check(card.AppliedEnchantments.Single().Card.Id == "TLC_467e" && card[GameTag.DISPLAYED_CREATOR] == source.Id, "persistent verified Health actor/creator");
        }
        var spell = (Playable)p.HandZone.First();
        var copy = (Playable)Generic.Copy(p.Opponent, p.Hero, spell, Zone.HAND);
        Resume(g, End); Visible(spell, spell.Card.Cost, CostPaymentKind.Health); Visible(copy, copy.Card.Cost, CostPaymentKind.Health); End(g);
        Effects.SetCost(2).ApplyTo(spell); Visible(spell, 2, CostPaymentKind.Health);
        var backward = Generic.Copy(p, p.Hero, spell, Zone.DECK); Visible(backward, spell.Card.Cost);
        p[HeraldRules.Profile] = (int)CardClass.WARLOCK;
        Summon(p, "CS2_182");
        p.Hero.Health = 2; p.Hero.Armor = 20;
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source == spell), "Options excludes equal Health despite Mana");
        Core2026Proof.Check(!CostRules.CanPay(p, CostRules.Quote(p, spell)) && !Generic.PayPhase(p, spell) && p.Hero.Health == 2 && p.Hero.Armor == 20, "equal Health illegal; Armor irrelevant");
        p.Hero.Health = 3; p.Hero.IsImmune = true; p.Hero.HasDivineShield = true;
        p.BaseMana = 0;
        Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source == spell), "Options includes affordable Health with zero Mana");
        var playable = g.CloneExact(history: false);
        var option = playable.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source.Id == spell.Id);
        Core2026Proof.Check(playable.Process(option) && playable.Player1.Hero.Health == 1 && playable.Player1.RemainingMana == 0,
            "engine accepts generated Health spell from Options with zero Mana");
        int events = 0; TriggerManager.TriggerHandler listener = _ => events++; g.TriggerManager.DealDamageTrigger += listener;
        Core2026Proof.Check(Generic.PayPhase(p, spell) && p.Hero.Health == 1 && p.Hero.Armor == 20 && events == 0 && p.Hero.HasDivineShield, "Health payment bypasses damage/prevention");
        g.TriggerManager.DealDamageTrigger -= listener;
        spell.AppliedEnchantments.Single().Remove(); Visible(spell, 2); Visible(copy, copy.Card.Cost, CostPaymentKind.Health);
        EmptyHand(p); p.Hero.Health = 30;
        source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes Deathrattle");
        source = Summon(p, id);
        Pool(g, id, 1, () => { Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.Select(c => c.Card.Id).Distinct().Count() == 1, "independent singleton picks allow duplicates"); });
        EmptyHand(p); FillHand(p, 9); source = Summon(p, id);
        var graveyard = p.GraveyardZone.Select(c => c.Id).ToHashSet();
        before = g.Random.DrawCount; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Type == CardType.SPELL) == 1 && g.Random.DrawCount - before == 2, "one slot burns second spell but picks twice");
        Generated(g, source, p.GraveyardZone.Where(c => !graveyard.Contains(c.Id) && c.Card.Id != id), 1);
        source = Summon(p, id); graveyard = p.GraveyardZone.Select(c => c.Id).ToHashSet(); before = g.Random.DrawCount; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == 2, "full hand burns both; both picks consumed");
        Generated(g, source, p.GraveyardZone.Where(c => !graveyard.Contains(c.Id) && c.Card.Id != id), 2);
        EmptyHand(p); source = Summon(p, id);
        Pool(g, id, 0, () => { var rng = g.Random.CaptureState(); Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 0 && rng.Equals(g.Random.CaptureState()), "empty pool no cards or RNG"); });
    });
}
