using FactoryApi.Contracts.Requests.Auth;
using FactoryApi.Contracts.Responses.Auth;
using FactoryApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace FactoryApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly JwtTokenService _jwtTokenService;

        public AuthController(IConfiguration configuration, JwtTokenService jwtTokenService)
        {
            _configuration = configuration;
            _jwtTokenService = jwtTokenService;
        }

        [HttpPost("login")]
        public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
        {
            var adminUsername = _configuration["AdminAccount:Username"];
            var adminPassword = _configuration["AdminAccount:Password"];

            if (request.Username != adminUsername || request.Password != adminPassword)
            {
                return Unauthorized(new { message = "아이디 또는 비밀번호가 올바르지 않습니다." });
            }

            var (token, expiresAt) = _jwtTokenService.CreateToken(request.Username);

            return Ok(new LoginResponse
            {
                AccessToken = token,
                Username = request.Username,
                ExpiresAt = expiresAt
            });
        }
    }
}