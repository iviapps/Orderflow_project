using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Overflow.Identity.Controllers
{
    [Route("api/profile")]
    [ApiController]
    [Authorize] // Requiere que el usuario esté autenticado <- JWT válido

    public class SelfService : ControllerBase
    {
        //esto es para obtener info del usuario autenticado
        private readonly UserManager<IdentityUser> _userManager;
        //esto es para logging que es importante en servicios para depuracion
        private readonly ILogger<SelfService> _logger;

        public SelfService(
            UserManager<IdentityUser> userManager,
            ILogger<SelfService> logger)
        {
            _userManager = userManager;
            _logger = logger;

        }
        /*[Authorize] → esto fuerza que solo entre quien tenga JWT válido.

        Inyectamos UserManager<IdentityUser> para consultar/actualizar el usuario.

        Inyectamos ILogger<ProfileController> para telemetría y trazas.
         
         */

        // GET: api/<SelfService>
        //GET /api/profile
        //PUT /api/profile
        //DTOS 
        public class ProfileResponse         {

            public string id { get; set; } = String.Empty;
            public string Email { get; set; } = String.Empty; 
        
        }

        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }


    }
}
