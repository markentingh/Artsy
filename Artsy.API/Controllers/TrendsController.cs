using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Trends;
using Artsy.Data.Interfaces;

namespace Artsy.API.Controllers
{
    [Route("/api/trends")]
    [Authorize]
    public class TrendsController : ApiController
    {
        readonly ITrendRepository _trendRepository;

        public TrendsController(ITrendRepository trendRepository)
        {
            _trendRepository = trendRepository;
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent([FromQuery] int limit = 20)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var trends = await _trendRepository.GetRecentAsync(limit);
                return Json(new ApiResponse { success = true, data = trends });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] DeleteTrendRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Trend ID is required." });

            try
            {
                await _trendRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
