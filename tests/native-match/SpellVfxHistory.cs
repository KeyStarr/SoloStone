using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Characterization of real SendHistory, not proof that Unity loaded or rendered the prefab.
    private static void SpellVfxHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (var scenario in new[] {
            (Card: "END_000", Hits: 1, Amount: 2, School: 6),
            (Card: "TIME_001", Hits: 3, Amount: 2, School: 0),
            (Card: "CORE_CS2_029", Hits: 1, Amount: 6, School: 2),
            (Card: "CS2_024", Hits: 1, Amount: 3, School: 0),
            (Card: "CS2_072", Hits: 1, Amount: 2, School: 0),
            (Card: "EX1_277", Hits: 3, Amount: 1, School: 0) })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: "showcase-rogue-imbue");
            var owner = match.Game.CurrentPlayer;
            Character victim = owner.Opponent.Hero;
            if (scenario.Card == "CS2_072")
                victim = (Character)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId(scenario.Card), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id
                && (t.Target == null || t.Target.Id == victim.Id)));
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            if (scenario.Card is not ("END_000" or "TIME_001"))
                Check(!data.Any(e => e.HasSubSpellStart || e.HasSubSpellEnd), "Authored legacy effects keep their native route without duplicate sub-spells");
            var powers = data.Where(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == 3).ToArray();
            Check(powers.Length == 1, scenario.Card + " has one native play-effect activation");
            var power = powers[0].PowerStart;
            Check(power.HasEffectIndex && power.EffectIndex == 0 && power.SubOption == -1
                && string.IsNullOrEmpty(power.EffectCardId) && !power.HasTriggerKeyword,
                scenario.Card + " selects source CardDef primary play effect");
            bool targeted = scenario.Hits == 1;
            Check(power.Target == (targeted ? victim.Id : 0), scenario.Card + " header retains chosen target");
            var show = data.Single(e => e.HasShowEntity && e.ShowEntity.Entity == source.Id);
            Check(data.IndexOf(show) < data.IndexOf(powers[0]) && show.ShowEntity.Name == scenario.Card
                && SnapshotTag(show, GameTag.CARDTYPE) == 5, scenario.Card + " identity/type available before POWER");
            if (targeted) Check(SnapshotTag(show, GameTag.CARD_TARGET) == victim.Id, scenario.Card + " CARD_TARGET at play time");
            if (scenario.School != 0) Check(SnapshotTag(show, (GameTag)1635) == scenario.School, scenario.Card + " spell school at play time");
            if (scenario.Card == "END_000")
                Check(SnapshotTag(show, (GameTag)3692) == 1 && owner.Hero.HeroPower.Card.Id == "END_000p"
                    && owner[GameTag.IMBUE] == 1, "Eventuality Imbue presentation tag and Rogue power replacement");
            var metadata = data.Where(e => e.HasMetaData).Select(e => e.MetaData).ToArray();
            Check(metadata.Length == scenario.Hits * 2, scenario.Card + " each hit has TARGET and DAMAGE");
            for (int hit = 0; hit < scenario.Hits; hit++)
            {
                var target = metadata[hit * 2]; var damage = metadata[hit * 2 + 1];
                Check((int)target.Type == 0 && target.Data == source.Id && target.Info.SequenceEqual(new[] { victim.Id })
                    && (int)damage.Type == 1 && damage.Data == scenario.Amount && damage.Info.SequenceEqual(target.Info),
                    scenario.Card + " preserves ordered target range and per-hit amount");
            }
            Check(victim.Damage == scenario.Hits * scenario.Amount, scenario.Card + " gameplay damage unchanged");
            if (scenario.Card == "CS2_024") Check(victim.IsFrozen, "Frostbolt side effect preserved");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS spell VFX wire contracts: Eventuality, Chrono Daggers, Fireball, Frostbolt, Backstab, Arcane Missiles; both players");
    }
}
