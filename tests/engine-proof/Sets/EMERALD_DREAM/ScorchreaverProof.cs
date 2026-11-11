using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static PoolPilotProof;
using static SymbiosisProof;

internal static class ScorchreaverProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_952", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_10"));
        var filter = Filter(id);
        Core2026Proof.Check(filter.ClassMode == PoolClassMode.ControllerAndNeutral && filter.Type == CardType.SPELL && filter.SpellSchool == 7, "printed Fel spell class pool");
        foreach (bool full in new[] { false, true })
        {
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
            LocationProofs.Location(p);
            if (full) while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
            var fel = Add(p, "TLC_901"); var ordinary = Add(p, "CS2_029"); var opposing = Add(p.Opponent, "TLC_901");
            int cost = fel.Cost, otherCost = ordinary.Cost, enemyCost = opposing.Cost;
            var twin = g.CloneExact(true);
            var source = Play(g, p, id); Play(twin, twin.Player1, id);
            var offers = Offers(g, id, filter, 3);
            Core2026Proof.Check(g.Resolution.HasSuspendedEffect && Generic.HasDecisionPlay(p) && fel.Cost == cost, "Battlecry waits; reduction after choice");
            Core2026Proof.Check(Vector(g) == Vector(twin) && g.Random.CaptureState().Equals(twin.Random.CaptureState()), "same seed same offers/RNG");
            // Avoid intrinsic Outcast cost when checking the newly generated entity.
            var selected = offers.First(o => o.Card.Id != "CORE_BT_801");
            if (full) FillHand(p);
            int slots = p.BoardZone.SlotCount;
            Resume(g, next => {
                Answer(next, selected.Id);
                var owner = next.Player1; var generated = next.IdEntityDic[selected.Id]; var discounted = next.IdEntityDic[fel.Id];
                Core2026Proof.Check(discounted.Cost == Math.Max(0, cost - 1) && discounted[GameTag.COST] == discounted.Cost, "existing Fel cost reduced visibly");
                Core2026Proof.Check(next.IdEntityDic[ordinary.Id].Cost == otherCost && next.IdEntityDic[opposing.Id].Cost == enemyCost, "non-Fel and opponent hand untouched");
                Core2026Proof.Check(generated.Zone.Type == (full ? Zone.GRAVEYARD : Zone.HAND)
                    && (full || generated.Cost == Math.Max(0, generated.Card.Cost - 1) && generated[GameTag.COST] == generated.Cost)
                    && owner.HandZone.Count == (full ? 10 : 3) && owner.BoardZone.SlotCount == slots && slots <= 7, "new Fel discounted after grant; full hand burns, board unchanged");
            });
            ((Minion)source).Silence(); Kill(g, (Minion)source); End(g); End(g);
            Core2026Proof.Check(fel.Cost == Math.Max(0, cost - 1), "discount survives Silence, source death, turn end");
            if (!p.HandZone.IsFull) {
                var later = Add(p, "TLC_901");
                Core2026Proof.Check(later.Cost == later.Card.Cost, "later Fel spells receive no discount");
            }
        }
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        foreach (int count in new[] { 0, 1, 2 })
        {
            var fel = Add(p, "TLC_901"); int old = fel.Cost;
            Pool(g, id, count, () => {
                Play(g, p, id);
                if (count == 0) Core2026Proof.Check(p.Choice == null && fel.Cost == Math.Max(0, old - 1), "empty pool still discounts existing Fel");
                else { var offers = Offers(g, id, filter, count); Answer(g, offers[0].Id); Core2026Proof.Check(fel.Cost == Math.Max(0, old - 1), "tiny pool completes then discounts"); }
            });
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
        }
        var zero = Add(p, "TLC_901"); CostRules.AddModifier((Playable)zero, SabberStoneCore.Enchants.EffectOperator.SET, 0);
        // Foreign Battlecry source must retain Scorchreaver's declared recipe identity.
        var foreign = Entity.FromCard(p, Cards.FromId("GIL_820"), zone: p.SetasideZone);
        var recipe = (DecisionBattlecryTask)Cards.FromId(id).Power.PowerTask;
        Core2026Proof.Check(recipe.Recipe.Process(g, p, foreign, null, new TaskStack()) == TaskState.SUSPENDED, "foreign Battlecry opens declared pool");
        var picked = Offers(g, id, filter, 3)[0];
        Resume(g, next => { Answer(next, picked.Id); Core2026Proof.Check(next.IdEntityDic[zero.Id].Cost == 0, "zero cost floor after foreign recipe"); });
        EmptyHand(p); Summon(p, "LOE_077");
        var twice = Add(p, "TLC_901"); int initial = twice.Cost;
        Play(g, p, id);
        for (int repetition = 1; repetition <= 2; repetition++)
        {
            var offers = Offers(g, id, filter, 3);
            int expected = Math.Max(0, initial - repetition);
            Resume(g, next => {
                Answer(next, offers[0].Id);
                Core2026Proof.Check(next.IdEntityDic[twice.Id].Cost == expected, "each Brann Battlecry discounts after its own choice");
            });
        }
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 3, "Brann grants exactly two Fel spells");
    });
}
