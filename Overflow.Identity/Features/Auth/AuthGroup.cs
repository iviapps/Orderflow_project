//namespace Overflow.Identity.Features.Auth
//{
//    public static class AuthGroup
//    {
//        public static RouteGroupBuilder MapAuthGroup(this IEndpointRouteBuilder routes)
//        {
//            var versionSet = routes
//                .NewVersionSet()
//                .HasApiVersion(new Asp.Versioning.ApiVersion(1, 0))
//                .ReportApiVersions()
//                .Build();

//            // Grupo versionado
//            var group = routes.MapGroup("/api/v{version:apiVersion}/auth")
//                              .WithApiVersionSet(versionSet)
//                              .WithTags("Auth");

//            return group;
//        }
//    }
//}
