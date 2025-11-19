
//using Microsoft.AspNetCore.Builder;

//namespace Overflow.Identity.Features.Auth
//{
//    public static class RegisterUser
//    {
//       public record Response(string UserId);
//        public record Request(string nombre, string email,  string Password, string ConfirmPassword);


//        public static IEndpointRouteBuilder MapRegisterUser(this IEndpointRouteBuilder group)
//        {
//        var AuthGroup = group.MapAuthGroup();

//            AuthGroup.MapPost("/register", HandlerAsync)
//            .WithName("RegisterUser")
//            .AllowAnonymous();
//        }

//        private static async Task HandlerAsync(HttpContext context)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}

//PENDIENTE DE CORREGIR 