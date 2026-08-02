using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.API.Models.Collections;

namespace Artsy.API.Controllers
{
    [Authorize]
    public partial class ProjectsController
    {
        [HttpPost("update-social-media-config")]
        public async Task<IActionResult> UpdateSocialMediaConfig([FromBody] UpdateSocialMediaConfigRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.Id, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectRepository.UpdateSocialMediaConfigAsync(request.Id, userId, request.SocialMediaPrompt, request.SocialMediaDescription);
                project.SocialMediaPrompt = request.SocialMediaPrompt;
                project.SocialMediaDescription = request.SocialMediaDescription;
                return Json(new ApiResponse { success = true, data = project });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("generate-social-media-description")]
        public async Task<IActionResult> GenerateSocialMediaDescription([FromBody] GenerateSocialMediaDescriptionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                if (!string.IsNullOrWhiteSpace(collection.Description))
                    return Json(new ApiResponse { success = true, data = new { description = collection.Description } });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var prompt = project.SocialMediaPrompt;
                var userDescription = project.SocialMediaDescription;

                string generatedDescription = "";

                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    var items = (await _projectItemRepository.GetByProjectIdAsync(project.Id)).ToList();
                    var artworkList = await _projectItemArtworkRepository.GetByProjectIdAsync(project.Id);
                    var customItemIds = artworkList.Where(a => a.ArtworkType == "custom").Select(a => a.ItemId).ToHashSet();
                    var aiItems = items.Where(i => !customItemIds.Contains(i.Id)).OrderBy(i => i.Index).ToList();

                    var itemTitles = aiItems.Select(i => i.Title).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                    var projectTitle = project.Title;

                    var systemPrompt = "You are a social media marketing expert. Generate an SEO-friendly, engaging rich description for an Instagram post about a collection of artwork. Use relevant hashtags. Keep it under 2000 characters.";
                    var userPrompt = $"Project: {projectTitle}\nArtworks: {string.Join(", ", itemTitles)}\nPrompt: {prompt}\n\nGenerate an engaging Instagram post description.";

                    try
                    {
                        generatedDescription = await Artsy.AI.OpenAI.Prompt(systemPrompt, "", userPrompt);
                    }
                    catch (Exception llmEx)
                    {
                        return Json(new ApiResponse { success = false, message = $"LLM generation failed: {llmEx.Message}" });
                    }
                }

                if (!string.IsNullOrWhiteSpace(userDescription))
                {
                    if (!string.IsNullOrWhiteSpace(generatedDescription))
                        generatedDescription += "\n\n" + userDescription;
                    else
                        generatedDescription = userDescription;
                }

                collection.Description = generatedDescription;
                await _projectCollectionRepository.UpdateAsync(collection);

                return Json(new ApiResponse { success = true, data = new { description = generatedDescription } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
