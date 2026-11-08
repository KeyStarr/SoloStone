using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enums;
using SabberStoneCore.Actions;

namespace SabberStoneCore.Loader
{
    internal static class ChooseOneOptions
    {
        internal static EntityData OptionData(Controller controller, int parentId, Card parent)
        {
            var data = new EntityData { [GameTag.CREATOR] = parentId, [GameTag.PARENT_CARD] = parentId };
            if (parent.Id == "JAIL_EVENT_100t" && parent[GameTag.MODULAR_ENTITY_PART_1] != 0)
                parent = Cards.FromAssetId(parent[GameTag.MODULAR_ENTITY_PART_1]);
            if (EitherSideRules.Supports(parent)) data[Generic.PrivateDecisionOfferTag] = controller.PlayerId;
            switch (parent.Id)
            {
                case "CORE_AT_037": case "CORE_EX1_154": case "CORE_EX1_160": case "CORE_OG_047":
                case "CORE_ONY_018": case "CORE_TSC_650": case "Core_LOE_115":
                    data[Generic.PrivateDecisionOfferTag] = controller.PlayerId;
                    break;
            }
            return data;
        }

        internal static Card[] Resolve(Card parent)
        {
            if (parent.Id == "JAIL_EVENT_100t" && parent[GameTag.MODULAR_ENTITY_PART_1] != 0)
                parent = Cards.FromAssetId(parent[GameTag.MODULAR_ENTITY_PART_1]);
            switch (parent.Id)
            {
                case "CORE_AT_037": return new[] { Cards.FromId("AT_037a"), Cards.FromId("AT_037b") };
                case "CORE_EX1_154": return new[] { Cards.FromId("EX1_154a"), Cards.FromId("EX1_154b") };
                case "CORE_EX1_160": return new[] { Cards.FromId("EX1_160a"), Cards.FromId("EX1_160b") };
                case "CORE_OG_047": return new[] { Cards.FromId("OG_047a"), Cards.FromId("OG_047b") };
                case "CORE_ONY_018": return new[] { Cards.FromId("ONY_018t"), Cards.FromId("ONY_018t2") };
                case "CORE_TSC_650": return new[] { Cards.FromId("TSC_650a"), Cards.FromId("TSC_650d") };
                case "Core_LOE_115": return new[] { Cards.FromId("LOE_115a"), Cards.FromId("LOE_115b") };
                case "EDR_525": return new[] { Cards.FromId("EDR_525A"), Cards.FromId("EDR_525B") };
                case "EDR_570": return new[] { Cards.FromId("EDR_570A"), Cards.FromId("EDR_570B") };
                case "EDR_872": return new[] { Cards.FromId("EDR_872A"), Cards.FromId("EDR_872B") };
                default: return new[] { Cards.FromId(parent.Id + "a"), Cards.FromId(parent.Id + "b") };
            }
        }
    }
}
