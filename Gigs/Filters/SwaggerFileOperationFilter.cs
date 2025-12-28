using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Gigs.Filters;

public class SwaggerFileOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var formParameters = context.MethodInfo.GetParameters()
            .Where(p => p.GetCustomAttributes(typeof(FromFormAttribute), false).Length > 0
                        || p.ParameterType == typeof(IFormFile)
                        || p.ParameterType == typeof(IFormFileCollection)
                        || p.ParameterType == typeof(List<IFormFile>));

        var parameterInfos = formParameters.ToList();
        if (!parameterInfos.Any()) return;

        operation.RequestBody.Content.Clear();
        var schema = new OpenApiSchema();

        foreach (var p in parameterInfos)
        {
            if (p.ParameterType == typeof(IFormFile))
            {
                schema.Properties.Add(p.Name, new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary",
                });
            }
            else if (p.ParameterType == typeof(IFormCollection) || p.ParameterType == typeof(List<IFormFile>))
            {
                schema.Properties.Add(p.Name, new OpenApiSchema
                {
                    Type = "array",
                    Items = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary",
                    },
                });
            }
            else
            {
                var reference = context.SchemaGenerator.GenerateSchema(p.ParameterType, context.SchemaRepository);
                schema.Properties.Add(p.Name, reference);
            }
        }

        operation.RequestBody.Content.Add("multipart/form-data", new OpenApiMediaType
        {
            Schema = schema,
        });
    }
}
