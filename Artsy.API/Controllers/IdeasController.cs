using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Artsy.API.Models;
using Artsy.API.Models.Ideas;
using Artsy.AI;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Route("/api/projects/{projectId}/ideas")]
    [Authorize]
    public class IdeasController : ApiController
    {
        readonly IProjectRepository _projectRepository;
        readonly IProjectQuestionRepository _projectQuestionRepository;
        readonly IProjectItemRepository _projectItemRepository;
        readonly IProjectItemQuestionRepository _projectItemQuestionRepository;
        readonly IProjectItemArtworkRepository _projectItemArtworkRepository;
        readonly IProjectIdeaRepository _projectIdeaRepository;
        readonly IProjectIdeaVariationRepository _projectIdeaVariationRepository;
        readonly IProjectCollectionRepository _projectCollectionRepository;
        readonly IProjectCollectionAnswerRepository _projectCollectionAnswerRepository;

        public IdeasController(
            IProjectRepository projectRepository,
            IProjectQuestionRepository projectQuestionRepository,
            IProjectItemRepository projectItemRepository,
            IProjectItemQuestionRepository projectItemQuestionRepository,
            IProjectItemArtworkRepository projectItemArtworkRepository,
            IProjectIdeaRepository projectIdeaRepository,
            IProjectIdeaVariationRepository projectIdeaVariationRepository,
            IProjectCollectionRepository projectCollectionRepository,
            IProjectCollectionAnswerRepository projectCollectionAnswerRepository)
        {
            _projectRepository = projectRepository;
            _projectQuestionRepository = projectQuestionRepository;
            _projectItemRepository = projectItemRepository;
            _projectItemQuestionRepository = projectItemQuestionRepository;
            _projectItemArtworkRepository = projectItemArtworkRepository;
            _projectIdeaRepository = projectIdeaRepository;
            _projectIdeaVariationRepository = projectIdeaVariationRepository;
            _projectCollectionRepository = projectCollectionRepository;
            _projectCollectionAnswerRepository = projectCollectionAnswerRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetIdeas(Guid projectId)
        {
            var userId = GetUserId();
            var project = await _projectRepository.GetByIdAsync(projectId, userId);
            if (project == null) return Json(new ApiResponse { success = false, message = "Project not found" });

            var ideas = await _projectIdeaRepository.GetByProjectIdAsync(projectId);
            var result = new List<object>();

            foreach (var idea in ideas)
            {
                var variations = await _projectIdeaVariationRepository.GetByIdeaIdAsync(idea.Id);
                result.Add(new
                {
                    idea.Id,
                    idea.Title,
                    idea.Prompt,
                    idea.Created,
                    Variations = variations.Select(v => new
                    {
                        v.Id,
                        v.Title,
                        v.Description
                    })
                });
            }

            return Json(new ApiResponse { success = true, data = result });
        }

        [HttpGet("{ideaId}")]
        public async Task<IActionResult> GetIdea(Guid projectId, Guid ideaId)
        {
            var userId = GetUserId();
            var project = await _projectRepository.GetByIdAsync(projectId, userId);
            if (project == null) return Json(new ApiResponse { success = false, message = "Project not found" });

            var idea = await _projectIdeaRepository.GetByIdAsync(ideaId);
            if (idea == null || idea.ProjectId != projectId) return Json(new ApiResponse { success = false, message = "Idea not found" });

            var variations = await _projectIdeaVariationRepository.GetByIdeaIdAsync(ideaId);
            return Json(new ApiResponse
            {
                success = true,
                data = new
                {
                    idea.Id,
                    idea.Title,
                    idea.Prompt,
                    idea.Created,
                    Variations = variations.Select(v => new
                    {
                        v.Id,
                        v.Title,
                        v.Description,
                        v.IdeaJson
                    })
                }
            });
        }

        [HttpPost("create-idea")]
        public async Task<IActionResult> CreateIdea(Guid projectId, [FromBody] CreateIdeaRequest request)
        {
            var userId = GetUserId();
            var project = await _projectRepository.GetByIdAsync(projectId, userId);
            if (project == null) return Json(new ApiResponse { success = false, message = "Project not found" });

            if (string.IsNullOrWhiteSpace(request?.Prompt))
                return Json(new ApiResponse { success = false, message = "Idea prompt is required" });

            var projectQuestions = (await _projectQuestionRepository.GetByProjectIdAsync(projectId)).ToList();
            var items = (await _projectItemRepository.GetByProjectIdAsync(projectId)).ToList();
            var allItemQuestions = (await _projectItemQuestionRepository.GetByProjectIdAsync(projectId)).ToList();
            var allArtwork = (await _projectItemArtworkRepository.GetByProjectIdAsync(projectId)).ToList();

            var itemQuestionMap = allItemQuestions.GroupBy(q => q.ItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var artworkPrompts = allArtwork
                .Where(a => a.ArtworkType == "ai" && !string.IsNullOrWhiteSpace(a.Prompt))
                .GroupBy(a => a.ItemId)
                .ToDictionary(g => g.Key, g => g.First().Prompt);

            var artworkPromptList = new List<object>();
            foreach (var item in items)
            {
                if (!artworkPrompts.ContainsKey(item.Id) || string.IsNullOrWhiteSpace(artworkPrompts[item.Id]))
                    continue;
                if (!itemQuestionMap.ContainsKey(item.Id) || itemQuestionMap[item.Id].Count == 0)
                    continue;

                artworkPromptList.Add(new
                {
                    id = item.Id.ToString(),
                    title = item.Title ?? "",
                    prompt = artworkPrompts[item.Id]
                });
            }

            var titleSystemPrompt = "You are a creative assistant. Given a user idea, the project title, and the artwork titles and prompts, create a concise main idea title and a short description for each artwork based on its prompt.\n" +
                "The main title must be 4 words or less and describe the user's idea.\n" +
                "Each artwork description should be a short, plain English summary of the artwork based on its prompt.\n" +
                "Return ONLY a JSON object with no markdown formatting, in the following structure:\n" +
                "{\"title\":\"\",\"artworkDescriptions\":{\"<artwork-id-1>\":\"<description-1>\",\"<artwork-id-2>\":\"<description-2>\"}}";

            var titleUserPrompt = $"Idea: {request.Prompt}\n\nProject title: {project.Title}\n\nArtworks: {JsonSerializer.Serialize(artworkPromptList, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })}";

            string titleLlmOutput;
            try
            {
                titleLlmOutput = await OpenAI.Prompt(titleSystemPrompt, "", titleUserPrompt, seed: (long)Random.Shared.Next(1, int.MaxValue));
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = $"LLM title generation failed: {ex.Message}" });
            }

            var titleRawJson = ExtractFirstJsonObject(titleLlmOutput) ?? titleLlmOutput.Trim();

            if (string.IsNullOrWhiteSpace(titleRawJson))
                return Json(new ApiResponse { success = false, message = "LLM returned an empty title response" });

            IdeaTitleResult? titleResult;
            try
            {
                titleResult = JsonSerializer.Deserialize<IdeaTitleResult>(titleRawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = $"Failed to parse LLM title response: {ex.Message}" });
            }

            if (titleResult == null || string.IsNullOrWhiteSpace(titleResult.Title))
                return Json(new ApiResponse { success = false, message = "LLM did not return an idea title" });

            var artworkList = new List<object>();
            foreach (var item in items)
            {
                if (!artworkPrompts.ContainsKey(item.Id) || string.IsNullOrWhiteSpace(artworkPrompts[item.Id]))
                    continue;
                if (!itemQuestionMap.ContainsKey(item.Id) || itemQuestionMap[item.Id].Count == 0)
                    continue;

                var artworkDescription = titleResult.ArtworkDescriptions != null && titleResult.ArtworkDescriptions.TryGetValue(item.Id.ToString(), out var d) && !string.IsNullOrWhiteSpace(d)
                    ? d
                    : artworkPrompts[item.Id];

                artworkList.Add(new
                {
                    id = item.Id.ToString(),
                    title = item.Title ?? "",
                    description = artworkDescription,
                    questions = itemQuestionMap[item.Id].Select(q => new { id = q.Id.ToString(), question = q.Question }).ToList()
                });
            }

            var context = new
            {
                project = new
                {
                    title = project.Title,
                    questions = projectQuestions.Select(q => new { id = q.Id.ToString(), question = q.Question }).ToList()
                },
                artworks = artworkList
            };

            var idea = await _projectIdeaRepository.CreateAsync(new ProjectIdea
            {
                ProjectId = projectId,
                Title = titleResult.Title,
                Prompt = request.Prompt
            });

            var answerSystemPrompt = "You are a creative assistant. Given the user's idea and project context, you must answer all the given questions creatively.\n" +
                "Every question MUST have an answer that fits the requirements of the user's idea.\n" +
                "The project answers should influence and affect each artwork answer.\n" +
                "The answer id must match the question id exactly.\n" +
                "The project.answers array must contain one answer object for every single question in project.questions.\n" +
                "The artworks.answers array must contain one answer object for every single question in every artwork's questions array.\n" +
                "Do not omit any artwork or any question.\n" +
                "Generate a unique title for the idea based on the answers you came up with. Do not use any of the user-provided idea titles that have already been used.\n" +
                "Return ONLY a JSON object with no markdown formatting, in the following structure (the ellipses indicate you must continue with the same pattern for every question):\n" +
                "{\"title\":\"<idea title>\",\"project\":{\"answers\":[{\"id\":\"<question-id-1>\",\"answer\":\"<answer-1>\"},...]},\"artworks\":{\"answers\":[{\"id\":\"<artwork-question-id-1>\",\"answer\":\"<artwork-answer-1>\"},...]}}";

            var serializedContext = JsonSerializer.Serialize(context, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var usedTitles = new List<string>();

            for (var i = 0; i < 5; i++)
            {
                var previousTitlesText = usedTitles.Count > 0
                    ? $"\n\nUsed idea titles: [{string.Join(", ", usedTitles.Select(t => $"\"{t}\""))}]"
                    : "";
                var variationUserPrompt = $"Idea: {request.Prompt}\n\nProject context:\n{serializedContext}\n\nThere are {projectQuestions.Count} project questions and {allItemQuestions.Count} artwork questions in total.{previousTitlesText}";

                string variationLlmOutput;
                try
                {
                    variationLlmOutput = await OpenAI.Prompt(answerSystemPrompt, "", variationUserPrompt, seed: (long)Random.Shared.Next(1, int.MaxValue));
                }
                catch (Exception ex)
                {
                    await _projectIdeaRepository.DeleteAsync(idea.Id);
                    return Json(new ApiResponse { success = false, message = $"LLM idea generation failed for idea {i + 1}: {ex.Message}" });
                }

                var variationRawJson = ExtractFirstJsonObject(variationLlmOutput) ?? variationLlmOutput.Trim();

                if (string.IsNullOrWhiteSpace(variationRawJson))
                {
                    await _projectIdeaRepository.DeleteAsync(idea.Id);
                    return Json(new ApiResponse { success = false, message = $"LLM returned an empty response for idea {i + 1}" });
                }

                IdeaVariationResult? variationResult;
                try
                {
                    variationResult = JsonSerializer.Deserialize<IdeaVariationResult>(variationRawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true });
                }
                catch (Exception ex)
                {
                    await _projectIdeaRepository.DeleteAsync(idea.Id);
                    return Json(new ApiResponse { success = false, message = $"Failed to parse LLM response for idea {i + 1}: {ex.Message}" });
                }

                if (variationResult == null)
                {
                    await _projectIdeaRepository.DeleteAsync(idea.Id);
                    return Json(new ApiResponse { success = false, message = $"LLM returned an invalid response for idea {i + 1}" });
                }

                variationResult.Project ??= new IdeaAnswersResult();
                variationResult.Artworks ??= new IdeaAnswersResult();

                var variationTitle = variationResult.Title ?? $"Idea {i + 1}";
                usedTitles.Add(variationTitle);

                var variationEntity = new ProjectIdeaVariation
                {
                    ProjectIdeaId = idea.Id,
                    Title = variationTitle,
                    Description = "",
                    IdeaJson = JsonSerializer.Serialize(variationResult, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                };

                await _projectIdeaVariationRepository.CreateManyAsync(new[] { variationEntity });
            }

            return await GetIdea(projectId, idea.Id);
        }

        [HttpDelete("{ideaId}")]
        public async Task<IActionResult> DeleteIdea(Guid projectId, Guid ideaId)
        {
            var userId = GetUserId();
            var project = await _projectRepository.GetByIdAsync(projectId, userId);
            if (project == null) return Json(new ApiResponse { success = false, message = "Project not found" });

            var idea = await _projectIdeaRepository.GetByIdAsync(ideaId);
            if (idea == null || idea.ProjectId != projectId) return Json(new ApiResponse { success = false, message = "Idea not found" });

            var variations = await _projectIdeaVariationRepository.GetByIdeaIdAsync(ideaId);
            foreach (var v in variations)
            {
                await _projectIdeaVariationRepository.DeleteAsync(v.Id);
            }

            await _projectIdeaRepository.DeleteAsync(ideaId);
            return Json(new ApiResponse { success = true });
        }

        [HttpPost("{ideaId}/collection")]
        public async Task<IActionResult> MakeCollection(Guid projectId, Guid ideaId, [FromBody] MakeCollectionRequest request)
        {
            var userId = GetUserId();
            var project = await _projectRepository.GetByIdAsync(projectId, userId);
            if (project == null) return Json(new ApiResponse { success = false, message = "Project not found" });

            var idea = await _projectIdeaRepository.GetByIdAsync(ideaId);
            if (idea == null || idea.ProjectId != projectId) return Json(new ApiResponse { success = false, message = "Idea not found" });

            var variations = await _projectIdeaVariationRepository.GetByIdeaIdAsync(ideaId);
            var variation = variations.FirstOrDefault(v => v.Id == request.VariationId);
            if (variation == null) return Json(new ApiResponse { success = false, message = "Variation not found" });

            if (string.IsNullOrWhiteSpace(variation.IdeaJson))
                return Json(new ApiResponse { success = false, message = "Variation has no answer data" });

            IdeaVariationResult? variationData;
            try
            {
                variationData = JsonSerializer.Deserialize<IdeaVariationResult>(variation.IdeaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = $"Failed to parse variation data: {ex.Message}" });
            }

            if (variationData == null)
                return Json(new ApiResponse { success = false, message = "Invalid variation data" });

            var collection = await _projectCollectionRepository.CreateAsync(new ProjectCollection
            {
                ProjectId = projectId,
                Title = idea.Title,
                Description = "",
                Created = DateTime.UtcNow,
                Status = 1
            });

            var allItemQuestions = (await _projectItemQuestionRepository.GetByProjectIdAsync(projectId)).ToList();
            var questionToItem = allItemQuestions.ToDictionary(q => q.Id, q => (Guid?)q.ItemId);

            var answers = new List<ProjectCollectionAnswer>();

            if (variationData.Project?.Answers != null)
            {
                foreach (var answer in variationData.Project.Answers)
                {
                    if (!Guid.TryParse(answer.Id, out var questionId)) continue;
                    answers.Add(new ProjectCollectionAnswer
                    {
                        ProjectId = projectId,
                        CollectionId = collection.Id,
                        QuestionId = questionId,
                        ItemId = null,
                        Answer = answer.Answer ?? ""
                    });
                }
            }

            if (variationData.Artworks?.Answers != null)
            {
                foreach (var answer in variationData.Artworks.Answers)
                {
                    if (!Guid.TryParse(answer.Id, out var questionId)) continue;
                    if (!questionToItem.TryGetValue(questionId, out var itemId)) continue;
                    answers.Add(new ProjectCollectionAnswer
                    {
                        ProjectId = projectId,
                        CollectionId = collection.Id,
                        QuestionId = questionId,
                        ItemId = itemId,
                        Answer = answer.Answer ?? ""
                    });
                }
            }

            foreach (var answer in answers)
            {
                await _projectCollectionAnswerRepository.CreateAsync(answer);
            }

            return Json(new ApiResponse { success = true, data = new { collection.Id, collection.Title } });
        }

        private static string? ExtractFirstJsonObject(string input)
        {
            var start = input.IndexOf('{');
            if (start < 0) return null;

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < input.Length; i++)
            {
                var c = input[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return input[start..(i + 1)];
                    }
                }
            }

            return null;
        }
    }

}
