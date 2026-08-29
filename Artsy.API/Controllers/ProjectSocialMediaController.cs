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
                    var collectionTitle = collection.Title;

                    var projectQuestions = (await _projectQuestionRepository.GetByProjectIdAsync(project.Id)).ToList();
                    var itemQuestions = (await _projectItemQuestionRepository.GetByProjectIdAsync(project.Id)).ToList();
                    var answers = (await _projectCollectionAnswerRepository.GetByCollectionIdAsync(request.CollectionId)).ToList();

                    var projectQa = projectQuestions
                        .Where(q => !string.IsNullOrWhiteSpace(q.Question))
                        .Select(q => {
                            var ans = answers.FirstOrDefault(a => a.QuestionId == q.Id && a.ItemId == null);
                            return new { Q = q.Question, A = ans?.Answer };
                        })
                        .Where(qa => !string.IsNullOrWhiteSpace(qa.A))
                        .ToList();

                    var itemQa = aiItems.Select(item => {
                        var itemQs = itemQuestions.Where(q => q.ItemId == item.Id && !string.IsNullOrWhiteSpace(q.Question)).ToList();
                        var itemAns = answers.Where(a => a.ItemId == item.Id).ToList();
                        var pairs = itemQs.Select(q => {
                            var ans = itemAns.FirstOrDefault(a => a.QuestionId == q.Id);
                            return new { Q = q.Question, A = ans?.Answer };
                        }).Where(qa => !string.IsNullOrWhiteSpace(qa.A)).ToList();
                        return new { ItemTitle = item.Title, QAs = pairs };
                    }).Where(x => x.QAs.Count > 0).ToList();

                    var qaBuilder = new System.Text.StringBuilder();
                    if (projectQa.Count > 0)
                    {
                        qaBuilder.AppendLine("Project Q&A:");
                        foreach (var qa in projectQa)
                            qaBuilder.AppendLine($"  Q: {qa.Q} A: {qa.A}");
                    }
                    if (itemQa.Count > 0)
                    {
                        qaBuilder.AppendLine("Artwork Q&A:");
                        foreach (var item in itemQa)
                        {
                            qaBuilder.AppendLine($"  {item.ItemTitle}:");
                            foreach (var qa in item.QAs)
                                qaBuilder.AppendLine($"    Q: {qa.Q} A: {qa.A}");
                        }
                    }

                    var systemPrompt = "You are a social media marketing expert. Generate an SEO-friendly, engaging rich description for an Instagram post about a collection of artwork. Use relevant hashtags. Keep it under 2000 characters. URL links are not allowed.";
                    var userPrompt = $"Project: {projectTitle}\nCollection: {collectionTitle}\nArtworks: {string.Join(", ", itemTitles)}\n";
                    if (qaBuilder.Length > 0)
                        userPrompt += qaBuilder.ToString();
                    userPrompt += $"\nPrompt: {prompt}\n\nGenerate an engaging Instagram post description.";

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
                await _projectCollectionRepository.UpdateDescriptionAsync(collection.Id, collection.Description);

                return Json(new ApiResponse { success = true, data = new { description = generatedDescription } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
