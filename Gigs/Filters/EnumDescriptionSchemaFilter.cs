using System.ComponentModel;
using System.Reflection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Gigs.Filters;

public class EnumDescriptionSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;

        var enumDescriptions = new List<string>();
        foreach (var enumValue in Enum.GetValues(context.Type))
        {
            var value = enumValue.ToString();
            if (string.IsNullOrEmpty(value)) continue;

            var memberInfo = context.Type.GetMember(value).FirstOrDefault();
            var descriptionAttribute = memberInfo?.GetCustomAttribute<DescriptionAttribute>();
            enumDescriptions.Add(descriptionAttribute?.Description ?? value);
        }

        schema.Enum.Clear();
        foreach (var enumDescription in enumDescriptions) schema.Enum.Add(new OpenApiString(enumDescription));
        schema.Type = "string";
    }
}
