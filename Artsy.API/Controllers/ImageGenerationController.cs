using Artsy.API.Models;
using Artsy.Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artsy.API.Controllers
{
    [Route("/api/image-generation")]
    [Authorize]
    public class ImageGenerationController : ApiController
    {
        readonly IImageGenerationModelRepository _repo;

        public ImageGenerationController(IImageGenerationModelRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("active-models")]
        public async Task<IActionResult> GetActiveModels()
        {
            try
            {
                var models = await _repo.GetActiveAsync();
                var result = models.Select(m => new
                {
                    id = m.Id,
                    modelKey = m.ModelKey,
                    name = m.Name,
                    model = m.Model,
                    cpmitTokens = m.CPMITTokens,
                    cpmiiTokens = m.CPMIITokens,
                    cpmoTokens = m.CPMOTokens
                }).ToList();

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
