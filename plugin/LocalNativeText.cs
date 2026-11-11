using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model;
using EngineCard = SabberStoneCore.Model.Card;

internal static class LocalNativeText
{
    // CardTextBuilder.IsAssembledCard and TextUtils.ParseTextForCraftableSplit read component dbfIds.
    internal const int FirstCompositeHalfTag = 4572;
    internal const int SecondCompositeHalfTag = 4573;
    private const string ObsoleteSpellDamageSuffix = " @spelldmg";
    private static readonly string[] AdditionalEngineIds = {
        "GAME_005",
        "NEW1_012o", "EX1_019e", "EX1_603e", "CS2_222o", "CS2_004e", "EX1_561e",
        "EX1_411e", "EX1_414e", "CS1_129e", "CS2_236e", "CS2_087e", "CS2_092e",
        "EX1_360e", "EX1_382e", "EX1_619e", "EX1_366e", "EX1_355e"
    };
    // Native (transport) ID -> engine ID, for the card file's locale texts, which are keyed by engine ID.
    private static readonly Dictionary<string, string> EngineIdByNativeId = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> TextByNativeId = BuildTextByNativeId();
    private static string clientLocale;

    internal static IReadOnlyCollection<string> SupportedIds => TextByNativeId.Keys;

    internal static bool TryGetNameOverride(bool localContext, string cardId, Func<string, string> transform,
        out string name)
    {
        name = null;
        if (!localContext || cardId != "EX1_084e" || transform == null) return false;
        name = transform(Cards.FromId(cardId).Name);
        return true;
    }

    internal static bool TryGetOverride(bool staged, string cardId, Func<string, string> defaultVariant,
        out string text) => TryGetOverride(staged, cardId, defaultVariant, ClientLocale(), out text);

    // The client's language, as the client reports it (Localization.GetActualLocale); the Options > Language choice when the client
    // cannot answer. Null for English and for anything that is not one of the client's locales. Remembered once it is settled:
    // never while the client says English and Options started it in another language (it can answer before its language is set up).
    internal static string ClientLocale() => ClientLocale(ReadClientLocale, () => NativeLanguageOption.Running);

    internal static string ClientLocale(Func<string> client, Func<string> running)
    {
        string known = clientLocale;
        if (known != null) return known.Length == 0 ? null : known;
        string reported = null, started = null;
        try { reported = client(); } catch (Exception) { reported = null; }
        try { started = running(); } catch (Exception) { started = null; }
        string locale = LocalizedCardText.Resolve(reported, started, out bool cacheable);
        if (cacheable) clientLocale = locale ?? "";
        return locale;
    }

    private static System.Reflection.MethodInfo actualLocale;
    private static bool actualLocaleLooked;

    private static string ReadClientLocale()
    {
        if (!actualLocaleLooked)
        {
            // Looked up once, found or not.
            actualLocaleLooked = true;
            try
            {
                var localization = Type.GetType("Localization, Blizzard.T5.Localization", false);
                actualLocale = localization?.GetMethod("GetActualLocale", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null);
            }
            catch (Exception) { actualLocale = null; }
        }
        return actualLocale?.Invoke(null, null)?.ToString();
    }

    internal static void ResetLocaleCache() { clientLocale = null; }

    // The engine id a native (transport) id stands for, or null when SoloStone has no text for it.
    internal static string EngineIdFor(string nativeId) =>
        nativeId != null && EngineIdByNativeId.TryGetValue(nativeId, out string engineId) ? engineId : null;

    // Raw text before the variant pick: the locale's own text for this card while the engine still plays the English it was made
    // from, else the engine's English. `status` says which and why.
    internal static string RawFor(string cardId, string locale) => RawFor(cardId, locale, out _);

    internal static string RawFor(string cardId, string locale, out LocalizedCardText.Status status)
    {
        status = LocalizedCardText.Status.English;
        if (string.IsNullOrEmpty(cardId) || !TextByNativeId.TryGetValue(cardId, out string english)) return null;
        if (locale != null && EngineIdByNativeId.TryGetValue(cardId, out string engineId))
        {
            status = LocalizedCardText.Lookup(locale, engineId, english, out string localized);
            if (status == LocalizedCardText.Status.Localized) return localized;
        }
        return english;
    }

    internal static bool TryGetOverride(bool staged, string cardId, Func<string, string> defaultVariant,
        string locale, out string text)
    {
        text = null;
        if (!staged || string.IsNullOrEmpty(cardId) || defaultVariant == null) return false;
        try
        {
            string rawText = RawFor(cardId, locale);
            if (string.IsNullOrEmpty(rawText)) return false;
            text = NormalizeRawText(defaultVariant(rawText));
            return !string.IsNullOrEmpty(text);
        }
        catch
        {
            text = null;
            return false;
        }
    }

    internal static string NormalizeRawText(string text) =>
        text != null && text.EndsWith(ObsoleteSpellDamageSuffix, StringComparison.Ordinal)
            ? text.Substring(0, text.Length - ObsoleteSpellDamageSuffix.Length)
            : text;

    private static Dictionary<string, string> BuildTextByNativeId()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var card in LocalEconomy.Catalog) Add(result, card);
        }
        catch { }
        var heroesAndPowers = LocalDecks.PlayerHeroes.SelectMany(id => new[] {
            id, Cards.FromAssetId(Cards.FromId(id)[SabberStoneCore.Enums.GameTag.HERO_POWER]).Id
        });
        foreach (string id in heroesAndPowers.Concat(AdditionalEngineIds).Concat(LocalDecks.TestStandardCardIds).Concat(LocalCardCatalog.SupplementalGeneratedIds)
                     .Concat(LocalDecks.TestLoaners.SelectMany(deck => deck.CardIds)).Concat(LocalCardCatalog.LegacyGeneratedPublicIds).Concat(LocalCardCatalog.CardStateGeneratedIds))
        {
            try { Add(result, Cards.FromId(id)); }
            catch { }
        }
        // Every other card the engine plays: tokens, generated cards and enchantments (Kazakus' trial parts, the cracked
        // Eggs of Khelos). A newer client has their post-patch text, but the engine plays the fixed set. Cards the
        // transport renames keep the lists above.
        foreach (EngineCard card in Cards.All)
        {
            try
            {
                if (card.Implemented && !result.ContainsKey(card.Id) && NativeMatch.TransportCardId(card.Id) == card.Id)
                {
                    result[card.Id] = card.Text;
                    EngineIdByNativeId[card.Id] = card.Id;
                }
            }
            catch { }
        }
        return result;
    }

    private static void Add(Dictionary<string, string> result, EngineCard card)
    {
        if (card == null) return;
        string native = NativeMatch.TransportCardId(card.Id);
        result[native] = card.Text;
        EngineIdByNativeId[native] = card.Id;
    }
}
