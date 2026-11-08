using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class CardStateBoundary
    {
        // Retired replacements keep their former hand/deck visibility, including Choose One branches.
        public const GameTag ReplacedHiddenZone = (GameTag)31143;

        public static bool HasPrivateAttachments(IEntity card) => card.Card.Id == "CATA_615" || card.Card.Id == "CATA_615t" || PayloadRules.IsStored(card) || card.Card.Shatter != null || card.Card.HandProgress != null
            || card is Spell && card[GameTag.SPELLPOWER] != 0
            || card[TemporaryRules.Temporary] != 0 || card[HandProgressRules.PrivateAttachmentTarget] != 0;

        internal static void EnrollAttachments(IEntity card)
        {
            card[HandProgressRules.PrivateAttachmentTarget] = card.Id;
            if (card.AppliedEnchantments != null)
                foreach (var actor in card.AppliedEnchantments) actor[HandProgressRules.PrivateAttachmentTarget] = card.Id;
        }

        internal static void ChangingDefinition(IPlayable card, Card target)
        {
            if (card.Card.Id == target.Id) return;
            // Clear before replacement: cross-type CHANGE and later SHOW may omit old definition tags.
            if (card.Card.Shatter != null)
                foreach (var tag in new[] { (GameTag)4239, (GameTag)4240, (GameTag)401, GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2 })
                {
                    card[tag] = target[tag];
                    // The clear is historical; later definitions must read their own defaults.
                    card.NativeTags.Remove(tag);
                }
            else if (card.Card.HandProgress != null)
            {
                card[GameTag.TAG_SCRIPT_DATA_NUM_1] = target[GameTag.TAG_SCRIPT_DATA_NUM_1];
                card.NativeTags.Remove(GameTag.TAG_SCRIPT_DATA_NUM_1);
            }
        }

        internal static void ResetHeldModifiers(IPlayable card, Card target)
        {
            foreach (var tag in card.NativeTags.Keys.ToArray())
            {
                if (tag == GameTag.ENTITY_ID || tag == GameTag.CONTROLLER || tag == GameTag.ZONE || tag == GameTag.ZONE_POSITION ||
                    tag == GameTag.CREATOR || tag == GameTag.DISPLAYED_CREATOR || tag == GameTag.REVEALED ||
                    tag == PayloadRules.CopySourceController ||
                    tag == HandProgressRules.PrivateAttachmentTarget || tag == HandProgressRules.Residence || tag == HandProgressRules.HeldForm ||
                    tag == TemporaryRules.Temporary || TemporaryRules.IsPrivateTag(tag)) continue;
                // CHANGE_ENTITY publishes replacement stats; a preceding increase looks like a hand buff.
                if (card is Character body && (tag == GameTag.ATK || tag == GameTag.HEALTH))
                {
                    if (tag == GameTag.ATK) body._modifiedATK = target[tag];
                    else body._modifiedHealth = target[tag];
                }
                else card[tag] = target[tag];
                card.NativeTags.Remove(tag);
            }
            if (card is Character character) character._modifiedImmune = target[GameTag.IMMUNE] != 0;
        }

        public static void ControllerEnd(Controller c)
        {
            HandProgressRules.CaptureControllerEnd(c);
            TemporaryRules.CaptureControllerEnd(c);
            DiscoverRealRules.ControllerEnd(c);
        }
    }
}
