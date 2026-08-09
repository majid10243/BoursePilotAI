using System.Globalization;
using System.Text.Json;

namespace BoursePilotAI.Services;

internal static class JsonElementExtensions
{
    public static bool TryGetPropertyIgnoreCase(this JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    public static string ReadString(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetPropertyIgnoreCase(name, out var value))
                continue;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString()?.Trim() ?? "",
                JsonValueKind.Number => value.GetRawText(),
                _ => ""
            };
        }

        return "";
    }

    public static double ReadDouble(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetPropertyIgnoreCase(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String &&
                double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
                return number;
        }

        return 0;
    }

    public static long ReadLong(this JsonElement element, params string[] names)
    {
        var number = element.ReadDouble(names);
        if (number > long.MaxValue)
            return long.MaxValue;
        if (number < long.MinValue)
            return long.MinValue;
        return Convert.ToInt64(number);
    }

    public static int ReadInt(this JsonElement element, params string[] names)
    {
        var number = element.ReadLong(names);
        return number is > int.MaxValue or < int.MinValue ? 0 : (int)number;
    }

    public static IEnumerable<JsonElement> FindArrayItems(this JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in propertyNames)
            {
                if (!element.TryGetPropertyIgnoreCase(propertyName, out var candidate))
                    continue;

                if (candidate.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in candidate.EnumerateArray())
                        yield return item;
                    yield break;
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                foreach (var item in property.Value.FindArrayItems(propertyNames))
                    yield return item;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                foreach (var item in child.FindArrayItems(propertyNames))
                    yield return item;
            }
        }
    }
}
