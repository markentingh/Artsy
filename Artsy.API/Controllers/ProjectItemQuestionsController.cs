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
        [HttpGet("get-item-questions")]
        public async Task<IActionResult> GetItemQuestions([FromQuery] Guid itemId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (itemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(itemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var questions = await _projectItemQuestionRepository.GetByItemIdAsync(itemId);
                return Json(new ApiResponse { success = true, data = questions });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-item-question")]
        public async Task<IActionResult> CreateItemQuestion([FromBody] CreateProjectItemQuestionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            if (string.IsNullOrWhiteSpace(request.Question))
                return Json(new ApiResponse { success = false, message = "Question is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var existingQuestions = await _projectItemQuestionRepository.GetByItemIdAsync(request.ItemId);
                var nextIndex = existingQuestions.Any() ? existingQuestions.Max(q => q.Index) + 1 : 1;

                var question = new ProjectItemQuestion
                {
                    ItemId = request.ItemId,
                    ProjectId = item.ProjectId,
                    Question = request.Question.Trim(),
                    Index = nextIndex
                };
                var created = await _projectItemQuestionRepository.CreateAsync(question);
                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-item-question")]
        public async Task<IActionResult> UpdateItemQuestion([FromBody] UpdateProjectItemQuestionRequest request)
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
                var question = await _projectItemQuestionRepository.GetByIdAsync(request.Id);
                if (question == null)
                    return Json(new ApiResponse { success = false, message = "Question not found." });

                var project = await _projectRepository.GetByIdAsync(question.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                question.Question = request.Question.Trim();
                await _projectItemQuestionRepository.UpdateAsync(question);
                return Json(new ApiResponse { success = true, data = question });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-item-question")]
        public async Task<IActionResult> DeleteItemQuestion([FromBody] DeleteProjectItemQuestionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Question ID is required." });

            try
            {
                var question = await _projectItemQuestionRepository.GetByIdAsync(request.Id);
                if (question == null)
                    return Json(new ApiResponse { success = false, message = "Question not found." });

                var project = await _projectRepository.GetByIdAsync(question.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectItemQuestionRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
