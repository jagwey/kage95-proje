using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kage95.Api.Middleware;

/// <summary>
/// Swagger UI'da yazma (POST/PUT/PATCH/DELETE) uçları için gerekli X-Kage95: 1 başlığını otomatik ekler.
/// </summary>
public class Kage95HeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.ApiDescription.HttpMethod?.ToUpperInvariant();
        if (method is null or "GET" or "HEAD" or "OPTIONS") return;

        operation.Parameters ??= new List<IOpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Kage95",
            In = ParameterLocation.Header,
            Required = true,
            Description = "CSRF doğrulama başlığı (varsayılan değer: 1)",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }
}
