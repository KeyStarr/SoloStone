using SabberStoneCore.Enums;
using static Core2026Proof;
internal static class GargantuanSlithererProof
{
    internal static void Run() => StandardSetsProof.Case("BE_204",Unpinned_BaseBattlecry);
    private static void Unpinned_BaseBattlecry(SabberStoneCore.Model.Game g,SabberStoneCore.Model.Entities.Controller p,string id)
    {
        var source = (SabberStoneCore.Model.Entities.Minion)Play(g,p,id);
        Core2026Proof.Check(source.HasTaunt && source.AttackDamage == 6 && source.Health == 7 && p.Choice.Choices.Count == 4,"printed Taunt 6/7 offers base abilities without C'Thun");
        CthunRulesProof.Pick(g,"BE_013t4");
        Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.Count(m => m.Card.Id == "BE_013t5" && m.AttackDamage == 2 && m.Health == 3 && m.HasTaunt) == 2,"Slitherer Battlecry summons exact base Fanatics");
        int ledger = SabberStoneCore.Actions.CthunRules.Progress(p);
        Kill(g,source);
        Core2026Proof.Check(p.Choice == null && SabberStoneCore.Actions.CthunRules.Progress(p) == ledger,"Assemble split marker does not grant a Deathrattle");
    }
}
