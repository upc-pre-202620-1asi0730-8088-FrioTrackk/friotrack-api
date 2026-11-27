using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
namespace FrioTrack.Api.Interfaces;
public sealed class PublicOperationFilter:IOperationFilter {
    public void Apply(OpenApiOperation operation,OperationFilterContext context){if(context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()||context.ApiDescription.RelativePath is "api/auth/login" or "api/auth/register" or "api/health" or "api/capabilities")operation.Security=[];}
}
