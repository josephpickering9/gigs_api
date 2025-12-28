using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gigs.Types;

public interface IId
{
    Guid Value { get; }
}

[TypeConverter(typeof(GuidIdTypeConverter<GigId>))]
public readonly record struct GigId(Guid Value): IId
{
    public override string ToString() => this.Value.ToString();
    public static GigId New() => new (Guid.NewGuid());
}

[TypeConverter(typeof(GuidIdTypeConverter<ArtistId>))]
public readonly record struct ArtistId(Guid Value): IId
{
    public override string ToString() => this.Value.ToString();
    public static ArtistId New() => new (Guid.NewGuid());
}

[TypeConverter(typeof(GuidIdTypeConverter<VenueId>))]
public readonly record struct VenueId(Guid Value): IId
{
    public override string ToString() => this.Value.ToString();
    public static VenueId New() => new (Guid.NewGuid());
}

[TypeConverter(typeof(GuidIdTypeConverter<PersonId>))]
public readonly record struct PersonId(Guid Value): IId
{
    public override string ToString() => this.Value.ToString();
    public static PersonId New() => new (Guid.NewGuid());
}

[TypeConverter(typeof(GuidIdTypeConverter<GigArtistId>))]
public readonly record struct GigArtistId(Guid Value): IId
{
    public override string ToString() => this.Value.ToString();
    public static GigArtistId New() => new (Guid.NewGuid());
}

[TypeConverter(typeof(GuidIdTypeConverter<SongId>))]
public readonly record struct SongId(Guid Value): IId
{
    public override string ToString() => this.Value.ToString();
    public static SongId New() => new (Guid.NewGuid());
}

[TypeConverter(typeof(GuidIdTypeConverter<FestivalId>))]
public readonly record struct FestivalId(Guid Value): IId
{
    public override string ToString() => this.Value.ToString();
    public static FestivalId New() => new (Guid.NewGuid());
}

public class GuidIdTypeConverter<TId> : TypeConverter
    where TId : struct, IId
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is string s && Guid.TryParse(s, out var guid))
        {
            return IdFactory.Create<TId>(guid);
        }

        return base.ConvertFrom(context, culture, value);
    }
}

public static class IdFactory
{
    public static TId Create<TId>(Guid value)
        where TId : struct, IId
        =>
        (TId)Activator.CreateInstance(typeof(TId), value) !;
}

public class IdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeof(IId).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(IdJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType) !;
    }

    private class IdJsonConverter<TId> : JsonConverter<TId>
        where TId : struct, IId
    {
        public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var guid = reader.TokenType switch
            {
                JsonTokenType.String when Guid.TryParse(reader.GetString(), out var g) => g,
                _ => throw new JsonException($"Invalid id for {typeToConvert.Name}")
            };
            return IdFactory.Create<TId>(guid);
        }

        public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }
}

public static class IdPropertyBuilderExtensions
{
    public static PropertyBuilder<TId> HasGuidIdConversion<TId>(this PropertyBuilder<TId> builder)
        where TId : struct, IId
    {
        var converter = new ValueConverter<TId, Guid>(
            id => id.Value,
            guid => IdFactory.Create<TId>(guid));

        var comparer = new ValueComparer<TId>(
            (l, r) => l.Value == r.Value,
            id => id.Value.GetHashCode(),
            id => IdFactory.Create<TId>(id.Value));

        builder.HasConversion(converter);
        builder.Metadata.SetValueConverter(converter);
        builder.Metadata.SetValueComparer(comparer);
        return builder;
    }

    public static PropertyBuilder<TId?> HasNullableGuidIdConversion<TId>(this PropertyBuilder<TId?> builder)
        where TId : struct, IId
    {
        var converter = new ValueConverter<TId?, Guid?>(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            guid => guid.HasValue ? IdFactory.Create<TId>(guid.Value) : (TId?)null);

        var comparer = new ValueComparer<TId?>(
            (l, r) => l.GetValueOrDefault().Value == r.GetValueOrDefault().Value && l.HasValue == r.HasValue,
            id => id.HasValue ? id.Value.Value.GetHashCode() : 0,
            id => id.HasValue ? IdFactory.Create<TId>(id.Value.Value) : (TId?)null);

        builder.HasConversion(converter);
        builder.Metadata.SetValueConverter(converter);
        builder.Metadata.SetValueComparer(comparer);
        return builder;
    }
}
