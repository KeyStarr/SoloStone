using System.Collections;
using System.Reflection;
using System.Xml.Serialization;

internal static class ProfileCopyTests
{
    internal static void Run(Action<bool, string> check)
    {
        Type[] types = {
            typeof(LocalCraftReceipt), typeof(LocalCraftCard), typeof(LocalLadderState), typeof(LocalCardTokens),
            typeof(LocalDeckKit), typeof(LocalWelcome), typeof(LocalOwnedCardGrant), typeof(LocalTrophy), typeof(LocalLegendDeck), typeof(LocalSideboard),
            typeof(LocalMilestoneCatchUp), typeof(LocalTierReward), typeof(LocalTierGrant),
            typeof(LocalPurchaseReceipt), typeof(LocalPurchaseGrants), typeof(LocalPurchaseCard),
            typeof(LocalHeroFavorite), typeof(LocalMatchRecord), typeof(LocalPackResult),
            typeof(LocalCustomDeck), typeof(LocalDeckCard), typeof(LocalClassRewardGrant), typeof(LocalPortraitGrant)
        };
        foreach (var type in types) {
            var source = Populate(type);
            var copyMethod = typeof(LocalProfile).GetMethod("Copy", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { type }, null) ?? throw new Exception(type.Name + " needs an explicit copy helper");
            var copy = copyMethod.Invoke(null, new[] { source })!;
            check(Xml(source) == Xml(copy), type.Name + " copy retains every serialized member");
            Detached(source, copy, check, type.Name);
            check(copyMethod.Invoke(null, new object[] { null }) == null, type.Name + " null copy");
        }
    }

    // Populate every public member so future fields fail unless the explicit copy also handles them.
    private static object Populate(Type type)
    {
        if (type == typeof(object)) return Populate(typeof(LocalDeckCard)); // CardsXml accepts legacy strings or premium entries.
        if (type == typeof(string)) return "non-default";
        if (type == typeof(bool)) return true;
        if (type.IsEnum) return Enum.GetValues(type).GetValue(Enum.GetValues(type).Length - 1)!;
        if (Nullable.GetUnderlyingType(type) is Type underlying) return Populate(underlying);
        if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(37, type);
        if (type.IsArray) {
            var array = Array.CreateInstance(type.GetElementType()!, 1);
            array.SetValue(Populate(type.GetElementType()!), 0);
            return array;
        }
        var value = Activator.CreateInstance(type)!;
        if (value is IList list) {
            list.Add(Populate(type.GetGenericArguments()[0]));
            return value;
        }
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            field.SetValue(value, Populate(field.FieldType));
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
            property.SetValue(value, Populate(property.PropertyType));
        return value;
    }

    private static string Xml(object value)
    {
        using var writer = new StringWriter();
        new XmlSerializer(value.GetType()).Serialize(writer, value);
        return writer.ToString();
    }

    private static void Detached(object source, object copy, Action<bool, string> check, string path)
    {
        var type = source.GetType();
        if (type.IsValueType || source is string) return;
        check(!ReferenceEquals(source, copy), path + " must be detached");
        if (source is IList list) {
            var copied = (IList)copy;
            check(list.Count == copied.Count, path + " list count");
            for (int i = 0; i < list.Count; i++) Detached(list[i]!, copied[i]!, check, path + "[" + i + "]");
            return;
        }
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (field.GetValue(source) is object member) Detached(member, field.GetValue(copy)!, check, path + "." + field.Name);
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
            if (property.GetValue(source) is object member) Detached(member, property.GetValue(copy)!, check, path + "." + property.Name);
    }
}
