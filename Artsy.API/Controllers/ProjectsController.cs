using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Route("/api/projects")]
    [Authorize]
    public class ProjectsController : ApiController
    {
        readonly IProjectRepository _projectRepository;

        public ProjectsController(IProjectRepository projectRepository)
        {
            _projectRepository = projectRepository;
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var projects = await _projectRepository.GetAllAsync(userId);
                return Json(new ApiResponse { success = true, data = projects });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                    return Json(new ApiResponse { success = false, message = "Title is required." });

                if (string.IsNullOrWhiteSpace(request.Key))
                    return Json(new ApiResponse { success = false, message = "Key is required." });

                if (!System.Text.RegularExpressions.Regex.IsMatch(request.Key, @"^[a-zA-Z0-9-]+$"))
                    return Json(new ApiResponse { success = false, message = "Key can only contain letters, numbers, and dashes." });

                var existing = await _projectRepository.GetByKeyAsync(request.Key);
                if (existing != null)
                    return Json(new ApiResponse { success = false, message = "A project with that key already exists." });

                var project = new Project
                {
                    AppUserId = userId,
                    Title = request.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    Key = request.Key.Trim().ToLower(),
                    Color = string.IsNullOrWhiteSpace(request.Color) ? "#000000" : request.Color.Trim()
                };

                var created = await _projectRepository.CreateAsync(project);
                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
