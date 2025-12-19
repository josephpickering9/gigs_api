using System.ComponentModel;

namespace Gigs.Utils;

public static class EnumExtensions
{
    public static string ToDescriptionString(this Enum value)
    {
        var fi = value.GetType().GetField(value.ToString());
        if (fi == null) return value.ToString();

        var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : value.ToString();
    }

    public static T? FromDescriptionString<T>(string description) where T : Enum
    {
        foreach (var field in typeof(T).GetFields())
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                {
                    var value = field.GetValue(null);
                    return value != null ? (T)value : default;
                }
            }
            else
            {
                if (field.Name == description)
                {
                    var value = field.GetValue(null);
                    return value != null ? (T)value : default;
                }
            }

        throw new ArgumentException("Not found.", nameof(description));
    }
}
