using Artsy.API.Models;
using Artsy.API.Models.ImageGeneration;
using Artsy.Auth.Policies;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artsy.API.Controllers.Admin
{
    [Route("/api/admin/image-generation")]
    [Authorize(Policy = nameof(AuthConstants.Policy.ManageUsers))]
    public class ImageGenerationController : ApiController
    {
        readonly IImageGenerationModelRepository _repo;

        public ImageGenerationController(IImageGenerationModelRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("get-models")]
        public async Task<IActionResult> GetModels()
        {
            try
            {
                var dbModels = (await _repo.GetAllAsync()).ToList();
                var result = dbModels.Select(m => new
                {
                    id = m.Id,
                    modelKey = m.ModelKey,
                    name = m.Name,
                    model = m.Model,
                    cpmitTokens = m.CPMITTokens,
                    cpmiiTokens = m.CPMIITokens,
                    cpmoTokens = m.CPMOTokens,
                    active = m.Active,
                    tokenConversion = m.TokenConversion
                }).ToList();

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("save-model")]
        public async Task<IActionResult> SaveModel([FromBody] SaveImageGenerationModelRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ModelKey))
                    return Json(new ApiResponse { success = false, message = "Model key is required." });

                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Model))
                    return Json(new ApiResponse { success = false, message = "Name and Model are required." });

                if (request.Id > 0)
                {
                    var existing = await _repo.GetByIdAsync(request.Id);
                    if (existing != null)
                    {
                        existing.ModelKey = request.ModelKey;
                        existing.Name = request.Name;
                        existing.Model = request.Model;
                        existing.CPMITTokens = request.CPMITTokens;
                        existing.CPMIITokens = request.CPMIITokens;
                        existing.CPMOTokens = request.CPMOTokens;
                        existing.Active = request.Active;
                        existing.TokenConversion = request.TokenConversion;
                        await _repo.UpdateAsync(existing);
                    }
                }
                else
                {
                    var model = new ImageGenerationModel
                    {
                        ModelKey = request.ModelKey,
                        Name = request.Name,
                        Model = request.Model,
                        CPMITTokens = request.CPMITTokens,
                        CPMIITokens = request.CPMIITokens,
                        CPMOTokens = request.CPMOTokens,
                        Active = request.Active,
                        TokenConversion = request.TokenConversion
                    };
                    await _repo.CreateAsync(model);
                }

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("toggle-active")]
        public async Task<IActionResult> ToggleActive([FromBody] ToggleActiveRequest request)
        {
            try
            {
                if (request.Id <= 0)
                    return Json(new ApiResponse { success = false, message = "ID is required." });

                await _repo.ToggleActiveAsync(request.Id, request.Active);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-model")]
        public async Task<IActionResult> DeleteModel([FromBody] DeleteImageGenerationModelRequest request)
        {
            try
            {
                if (request.Id <= 0)
                    return Json(new ApiResponse { success = false, message = "ID is required." });

                await _repo.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
