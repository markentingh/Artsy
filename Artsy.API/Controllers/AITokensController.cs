using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Services;

namespace Artsy.API.Controllers
{
    [Route("/api/ai-tokens")]
    [Authorize]
    public class AITokensController : ApiController
    {
        readonly IAITokenService _aiTokenService;

        public AITokensController(IAITokenService aiTokenService)
        {
            _aiTokenService = aiTokenService;
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var available = await _aiTokenService.GetAvailableTokensAsync(userId);
                return Json(new ApiResponse { success = true, data = available });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
