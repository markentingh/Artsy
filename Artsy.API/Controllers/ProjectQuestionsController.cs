using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    public partial class ProjectsController
    {
        [HttpGet("get-questions")]
        public async Task<IActionResult> GetQuestions([FromQuery] Guid projectId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (projectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var questions = await _projectQuestionRepository.GetByProjectIdAsync(projectId);
                return Json(new ApiResponse { success = true, data = questions });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-question")]
        public async Task<IActionResult> CreateQuestion([FromBody] CreateProjectQuestionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (string.IsNullOrWhiteSpace(request.Question))
                return Json(new ApiResponse { success = false, message = "Question is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var question = new ProjectQuestion
                {
                    ProjectId = request.ProjectId,
                    Question = request.Question.Trim(),
                    Index = request.Index
                };
                var created = await _projectQuestionRepository.CreateAsync(question);
                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-question")]
        public async Task<IActionResult> UpdateQuestion([FromBody] UpdateProjectQuestionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Question ID is required." });

            if (string.IsNullOrWhiteSpace(request.Question))
                return Json(new ApiResponse { success = false, message = "Question is required." });

            try
            {
                var question = await _projectQuestionRepository.GetByIdAsync(request.Id);
                if (question == null)
                    return Json(new ApiResponse { success = false, message = "Question not found." });

                var project = await _projectRepository.GetByIdAsync(question.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                question.Question = request.Question.Trim();
                await _projectQuestionRepository.UpdateAsync(question);
                return Json(new ApiResponse { success = true, data = question });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-question")]
        public async Task<IActionResult> DeleteQuestion([FromBody] DeleteProjectQuestionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Question ID is required." });

            try
            {
                var question = await _projectQuestionRepository.GetByIdAsync(request.Id);
                if (question == null)
                    return Json(new ApiResponse { success = false, message = "Question not found." });

                var project = await _projectRepository.GetByIdAsync(question.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectQuestionRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
