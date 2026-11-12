using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("b14-k3-mage-native", ProofKind.Native)]
    private static void B14K3MageNative()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "EDR_874", "CATA_979", "BE_150" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var game = match.Game; var owner = game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            owner.BaseMana = 10; owner.UsedMana = 0;
            IPlayable retired = id == "CATA_979" ? EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.HandZone) : null;
            var source = Generic.DrawCard(owner, Cards.FromId(id));
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var prior = game.IdEntityDic.Values.Select(c => c.Id).ToHashSet();
            InvokeProcess(match, PlayCardTask.Any(owner, source));
            if (owner.Choice != null)
                InvokeProcess(match, ChooseTask.Pick(owner, owner.Choice.Choices[0]));
            FlushFieldHistory(match);
            var hidden = game.IdEntityDic.Values.Where(c => !prior.Contains(c.Id) &&
                (c.Zone == owner.HandZone || c.Zone == owner.DeckZone)).ToArray();
            Check(hidden.Length > 0, "privacy fixture creates/draws hidden cards " + id);
            var actors = hidden.SelectMany(c => c.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>()).ToArray();
            var data = History(capture, cursor);
            if (bot)
            {
                if (retired != null)
                    Check((bool)typeof(NativeMatch).GetMethod("IsHiddenFromHuman", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(match, new object[] { retired.Id, null, null }), "retired spell remains hidden in live/reset projection");
                if (retired != null)
                    Check(!data.Any(e => SnapshotEntity(e) == retired.Id && !string.IsNullOrEmpty(SnapshotName(e))),
                        "retired hidden hand spell identity must remain private");
                Check(!data.Any(e => hidden.Any(c => c.Id == SnapshotEntity(e)) && !string.IsNullOrEmpty(SnapshotName(e))),
                    "opponent generated/drawn card identities remain private " + id);
                Check(!data.Any(e => actors.Any(c => c.Id == SnapshotEntity(e)) &&
                    (!string.IsNullOrEmpty(SnapshotName(e)) || SnapshotTag(e, GameTag.ATTACHED) != 0)),
                    "opponent named spell attachments stay private");
            }
            if (id == "EDR_874" && !bot)
            {
                Check(hidden.All(c => match.SnapshotVisibleTransportTags(c).TryGetValue(291, out int bonus) && bonus == 1),
                    "owner sees retail spell-local bonus on both generated spells");
                Check(actors.All(c => data.Any(e => SnapshotEntity(e) == c.Id && SnapshotName(e) == "EDR_874e")),
                    "owner sees both exported named attachments");
            }
            Check(!data.Any(e => e.HasTagChange && e.TagChange.Tag == (int)ShatterRules.PrivateHandOwner)
                && data.All(e => SnapshotTag(e, ShatterRules.PrivateHandOwner) == 0), "private retirement marker never reaches native packets");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS b14-k3 native: generated hand/topdeck/draw privacy and spell-local attachment projection");
    }
}
