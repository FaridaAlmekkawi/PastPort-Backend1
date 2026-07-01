using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace PastPort.API.Extensions;

public class SwaggerFileOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var formFileParameters = context.MethodInfo.GetParameters()
            .Where(p => p.GetCustomAttribute<FromFormAttribute>() != null)
            .ToList();

        if (!formFileParameters.Any())
            return;

        var fileParams = formFileParameters
            .Where(p => p.ParameterType == typeof(IFormFile) ||
                       (p.ParameterType.IsGenericType &&
                        p.ParameterType.GetGenericArguments().Contains(typeof(IFormFile))))
            .ToList();

        if (!fileParams.Any())
            return;

        IDictionary<string, OpenApiMediaType> content = new Dictionary<string, OpenApiMediaType>
        {
            ["multipart/form-data"] = new OpenApiMediaType
            {
                Schema = CreateFormDataSchema(context, formFileParameters)
            }
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = content
        };

        operation.Parameters?.Clear();
    }

    private static OpenApiSchema CreateFormDataSchema(OperationFilterContext _, List<ParameterInfo> formFileParameters)
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(),
            Required = new HashSet<string>()
        };

        foreach (var param in formFileParameters)
        {
            var isFile = param.ParameterType == typeof(IFormFile);
            var isRequired = param.GetCustomAttribute<RequiredAttribute>() != null;

            if (isFile)
            {
                schema.Properties[param.Name!] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Format = "binary",
                    Description = $"Upload file - {param.Name}"
                };

                if (isRequired)
                {
                    schema.Required.Add(param.Name!);
                }
            }
            else
            {
                schema.Properties[param.Name!] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = $"Field - {param.Name}"
                };

                if (isRequired)
                {
                    schema.Required.Add(param.Name!);
                }
            }
        }

        return schema;
    }
}
