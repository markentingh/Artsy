using System.Text;
using System.Text.Json;
using Artsy.API.Models.Collections;
using Artsy.API.Models.Projects;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Services
{
    public class ArtworkGenerationPlanService : IArtworkGenerationPlanService
    {
        readonly IProjectItemRepository _projectItemRepository;
        readonly IProjectItemArtworkRepository _projectItemArtworkRepository;
        readonly IProjectBlueprintsRepository _projectBlueprintRepository;
        readonly IProjectItemReferenceRepository _projectItemReferenceRepository;
        readonly IProjectCollectionArtworkRepository _projectCollectionArtworkRepository;
        readonly ICustomImageRepository _customImageRepository;
        readonly IProjectQuestionRepository _projectQuestionRepository;
        readonly IProjectItemQuestionRepository _projectItemQuestionRepository;
        readonly IImageService _imageService;
        readonly IOpacityService _opacityService;

        public ArtworkGenerationPlanService(
            IProjectItemRepository projectItemRepository,
            IProjectItemArtworkRepository projectItemArtworkRepository,
            IProjectBlueprintsRepository projectBlueprintRepository,
            IProjectItemReferenceRepository projectItemReferenceRepository,
            IProjectCollectionArtworkRepository projectCollectionArtworkRepository,
            ICustomImageRepository customImageRepository,
            IProjectQuestionRepository projectQuestionRepository,
            IProjectItemQuestionRepository projectItemQuestionRepository,
            IImageService imageService,
            IOpacityService opacityService)
        {
            _projectItemRepository = projectItemRepository;
            _projectItemArtworkRepository = projectItemArtworkRepository;
            _projectBlueprintRepository = projectBlueprintRepository;
            _projectItemReferenceRepository = projectItemReferenceRepository;
            _projectCollectionArtworkRepository = projectCollectionArtworkRepository;
            _customImageRepository = customImageRepository;
            _projectQuestionRepository = projectQuestionRepository;
            _projectItemQuestionRepository = projectItemQuestionRepository;
            _imageService = imageService;
            _opacityService = opacityService;
        }

        public async Task<ArtworkGenerationPlan> BuildPlanAsync(
            Guid projectId,
            Guid collectionId,
            Guid itemId,
            string? requestedChanges = null,
            List<GenerateProjectItemPreviewAnswer>? answers = null)
        {
            var artworkList = await _projectItemArtworkRepository.GetByItemIdAsync(itemId);
            var artwork = artworkList.FirstOrDefault();
            if (artwork == null)
                throw new InvalidOperationException("No artwork configured for this item.");

            // --- Build the final prompt ---
            var promptBuilder = new StringBuilder(artwork.Prompt ?? "");

            if (answers != null && answers.Count > 0)
            {
                var projectQuestions = await _projectQuestionRepository.GetByProjectIdAsync(projectId);
                var itemQuestions = await _projectItemQuestionRepository.GetByItemIdAsync(itemId);

                var ignoredQuestionIds = new HashSet<Guid>();
                if (!string.IsNullOrWhiteSpace(artwork.IgnoredQuestions))
                {
                    try
                    {
                        var ignoredList = JsonSerializer.Deserialize<List<Guid>>(artwork.IgnoredQuestions);
                        if (ignoredList != null)
                            ignoredQuestionIds = new HashSet<Guid>(ignoredList);
                    }
                    catch { }
                }

                var questionLookup = new Dictionary<Guid, string>();
                foreach (var q in projectQuestions)
                {
                    if (ignoredQuestionIds.Contains(q.Id))
                        continue;
                    questionLookup[q.Id] = q.Question;
                }
                foreach (var q in itemQuestions)
                    questionLookup[q.Id] = q.Question;

                foreach (var answer in answers)
                {
                    if (string.IsNullOrWhiteSpace(answer.Answer))
                        continue;
                    if (questionLookup.TryGetValue(answer.QuestionId, out var questionText))
                    {
                        promptBuilder.AppendLine();
                        promptBuilder.AppendLine($"Question: {questionText}");
                        promptBuilder.AppendLine($"Answer: {answer.Answer}");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(requestedChanges))
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine($"Requested Changes: {requestedChanges}");
            }

            var finalPrompt = promptBuilder.ToString().Trim();

            // Append chroma key background instruction if OpacityJson has chroma keys
            var opacitySettings = _opacityService.ParseOpacityJson(artwork.OpacityJson);
            var hasOpacity = opacitySettings != null && opacitySettings.ChromaKeys.Count > 0;
            if (hasOpacity)
            {
                var firstColor = opacitySettings!.ChromaKeys[0];
                var hexColor = $"#{firstColor.R:X2}{firstColor.G:X2}{firstColor.B:X2}";
                finalPrompt += $" the background for this image must be a completely flat, uniform, solid color using {hexColor} hex color with no gradients, textures, shadows, or objects, filling the entire background area, so that we can apply a chroma key to the image later";
            }

            // --- Collect blueprint placements for this item ---
            var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(projectId);
            var itemPlacements = new List<(int W, int H, string CropX, string CropY)>();
            foreach (var bp in blueprints)
            {
                if (string.IsNullOrWhiteSpace(bp.PlacementJson)) continue;
                try
                {
                    var placements = JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson);
                    if (placements == null) continue;
                    foreach (var p in placements)
                    {
                        if (p.GetItemId() == itemId)
                        {
                            var (pw, ph) = p.GetDimensions();
                            if (pw > 0 && ph > 0)
                                itemPlacements.Add((pw, ph, p.CropX ?? "center", p.CropY ?? "center"));
                        }
                    }
                }
                catch { }
            }

            // --- Group placements by aspect ratio into variant groups ---
            var variantGroups = new List<(int W, int H, string CropX, string CropY)>();
            var seenRatios = new HashSet<string>();
            foreach (var (w, h, cx, cy) in itemPlacements)
            {
                var ratio = (double)w / h;
                var key = $"{ratio:F4}";
                if (seenRatios.Contains(key)) continue;
                seenRatios.Add(key);
                variantGroups.Add((w, h, cx, cy));
            }

            // --- Build generation tasks ---
            var tasks = new List<ArtworkGenerationTask>();
            int baseWidth, baseHeight;

            if (variantGroups.Count > 0)
            {
                for (var i = 0; i < variantGroups.Count; i++)
                {
                    var (pw, ph, cx, cy) = variantGroups[i];
                    var (genW, genH, needsCrop) = ImageGenerationForOpenAI.CalculateCustomResolution(pw, ph);

                    tasks.Add(new ArtworkGenerationTask
                    {
                        Width = genW,
                        Height = genH,
                        NeedsCrop = needsCrop,
                        PlacementWidth = pw,
                        PlacementHeight = ph,
                        CropX = cx,
                        CropY = cy,
                        NeedsMask = needsCrop, // Mask needed when crop is required
                        VariantIndex = i
                    });
                }

                baseWidth = tasks[0].Width;
                baseHeight = tasks[0].Height;
            }
            else
            {
                // No placements: use the artwork's aspect ratio at 2K
                var (aw, ah) = ImageGenerationForOpenAI.GetDimensionsFromAspectRatio(artwork.AspectRatio, 2);
                tasks.Add(new ArtworkGenerationTask
                {
                    Width = aw,
                    Height = ah,
                    NeedsCrop = false,
                    NeedsMask = false,
                    VariantIndex = 0
                });
                baseWidth = aw;
                baseHeight = ah;
            }

            // --- Collect reference images ---
            var referenceImages = new List<ArtworkReferenceImage>();
            var references = await _projectItemReferenceRepository.GetByItemIdAsync(itemId);
            if (references != null && references.Any())
            {
                foreach (var reference in references)
                {
                    byte[]? imageBytes = null;

                    if (reference.ArtworkId.HasValue)
                    {
                        var refCollectionArtwork = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(collectionId, reference.ArtworkId.Value);
                        if (refCollectionArtwork != null)
                        {
                            if (refCollectionArtwork.Opacity)
                            {
                                imageBytes = await _imageService.GetProjectCollectionArtworkPngAsync(reference.ProjectId, collectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                                if (imageBytes == null || imageBytes.Length == 0)
                                    imageBytes = await _imageService.GetProjectCollectionArtworkFullSizePngAsync(reference.ProjectId, collectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                            }
                            else
                            {
                                imageBytes = await _imageService.GetProjectCollectionArtworkImageAsync(reference.ProjectId, collectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                                if (imageBytes == null || imageBytes.Length == 0)
                                    imageBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(reference.ProjectId, collectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                            }
                        }
                    }
                    else if (reference.CustomImageId.HasValue)
                    {
                        var customImg = await _customImageRepository.GetByIdAsync(reference.CustomImageId.Value);
                        if (customImg != null)
                            imageBytes = await _imageService.GetCustomImageAsync(customImg.AppUserId, customImg.Id, customImg.Extension);
                    }

                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        var dims = await _imageService.GetImageDimensionsAsync(imageBytes);
                        referenceImages.Add(new ArtworkReferenceImage
                        {
                            ImageBytes = imageBytes,
                            Type = reference.ArtworkId.HasValue ? "artwork" : "custom",
                            Id = (reference.ArtworkId ?? reference.CustomImageId).ToString(),
                            Width = dims?.width ?? 0,
                            Height = dims?.height ?? 0
                        });
                    }
                }
            }

            // Determine if this artwork needs upscaling.
            // An artwork needs upscaling if it has product placements,
            // OR if it's not solely used as a reference by other items.
            var needsUpscale = variantGroups.Count > 0;
            if (!needsUpscale)
            {
                // Check if any other item references this item's artwork
                var allReferences = await _projectItemReferenceRepository.GetByProjectIdAsync(projectId);
                var isReferencedByOthers = allReferences.Any(r => r.ArtworkId == itemId && r.ItemId != itemId);
                // If not referenced by others, it's a standalone artwork that needs upscaling
                needsUpscale = !isReferencedByOthers;
            }

            return new ArtworkGenerationPlan
            {
                Artwork = artwork,
                FinalPrompt = finalPrompt,
                HasOpacity = hasOpacity,
                ReferenceImages = referenceImages,
                Tasks = tasks,
                TotalPlacements = variantGroups.Count,
                NeedsUpscale = needsUpscale,
                Width = baseWidth,
                Height = baseHeight
            };
        }
    }
}
