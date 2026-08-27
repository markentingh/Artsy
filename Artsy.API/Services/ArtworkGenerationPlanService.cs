using System.Text;
using System.Text.Json;
using Artsy.API.Models.Collections;
using Artsy.API.Models.Projects;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces;
using Artsy.Data.Interfaces.Projects;
using Artsy.Data.Entities.Projects;

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
        readonly IProjectBlueprintPlacementGroupRepository _placementGroupRepository;
        readonly IProjectBlueprintPlacementGroupImageRepository _placementGroupImageRepository;
        readonly IProjectCollectionProductRepository _projectCollectionProductRepository;
        readonly IProjectCollectionArtworkReferenceRepository _projectCollectionArtworkReferenceRepository;

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
            IOpacityService opacityService,
            IProjectBlueprintPlacementGroupRepository placementGroupRepository,
            IProjectBlueprintPlacementGroupImageRepository placementGroupImageRepository,
            IProjectCollectionProductRepository projectCollectionProductRepository,
            IProjectCollectionArtworkReferenceRepository projectCollectionArtworkReferenceRepository)
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
            _placementGroupRepository = placementGroupRepository;
            _placementGroupImageRepository = placementGroupImageRepository;
            _projectCollectionProductRepository = projectCollectionProductRepository;
            _projectCollectionArtworkReferenceRepository = projectCollectionArtworkReferenceRepository;
        }

        public async Task<ArtworkGenerationPlan> BuildPlanAsync(
            Guid projectId,
            Guid collectionId,
            Guid itemId,
            string? requestedChanges = null,
            List<GenerateProjectItemPreviewAnswer>? answers = null,
            int resolutionTier = 1)
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
            var allBlueprints = await _projectBlueprintRepository.GetByProjectIdAsync(projectId);
            // Filter to only active collection products when a collectionId is provided
            IEnumerable<Data.Entities.Projects.ProjectBlueprints> blueprints = allBlueprints;
            if (collectionId != Guid.Empty)
            {
                var collectionProducts = await _projectCollectionProductRepository.GetByCollectionIdAsync(collectionId);
                var activeBlueprintIds = collectionProducts.Where(cp => cp.Active).Select(cp => cp.ProjectBlueprintId).ToHashSet();
                if (activeBlueprintIds.Count > 0)
                {
                    blueprints = allBlueprints.Where(bp => activeBlueprintIds.Contains(bp.Id));
                }
            }
            var itemPlacements = new List<(string Position, int W, int H, string CropX, string CropY, int BlueprintId, string BlueprintName)>();
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
                                itemPlacements.Add((p.Position ?? "", pw, ph, p.CropX ?? "center", p.CropY ?? "center", bp.BlueprintId, bp.Name ?? ""));
                        }
                    }
                }
                catch { }
            }

            // --- Check for seamless placement groups ---
            // A placement group defines a set of placements that should share one seamless artwork.
            // We collect all group images that reference this item's artwork, grouped by group ID.
            var seamlessGroups = new Dictionary<Guid, List<(string Position, int W, int H, string CropX, string CropY, bool FlipX, bool FlipY, int BlueprintId, string BlueprintName)>>();
            // Track grouped positions per blueprint so positions from one blueprint's group
            // don't exclude the same position name from other blueprints
            var groupedPositions = new HashSet<(int BlueprintId, string Position)>();
            foreach (var bp in blueprints)
            {
                var groups = await _placementGroupRepository.GetByProjectAndBlueprintAsync(projectId, bp.BlueprintId);
                foreach (var group in groups)
                {
                    var groupImages = await _placementGroupImageRepository.GetByGroupIdAsync(group.Id);
                    // Only include groups where at least one image references this item
                    if (!groupImages.Any(gi => gi.ArtworkId == itemId)) continue;

                    var placements = new List<(string Position, int W, int H, string CropX, string CropY, bool FlipX, bool FlipY, int BlueprintId, string BlueprintName)>();
                    foreach (var gi in groupImages.OrderBy(g => g.Index))
                    {
                        if (gi.ArtworkId != itemId) continue;
                        // Find the placement dimensions for this position within this blueprint
                        var placement = itemPlacements.FirstOrDefault(ip => ip.Position == gi.Position && ip.BlueprintId == bp.BlueprintId);
                        if (placement.W > 0 && placement.H > 0)
                        {
                            placements.Add((gi.Position, placement.W, placement.H, placement.CropX, placement.CropY, gi.FlipX, gi.FlipY, bp.BlueprintId, bp.Name ?? ""));
                            groupedPositions.Add((bp.BlueprintId, gi.Position));
                        }
                    }
                    if (placements.Count > 0)
                        seamlessGroups[group.Id] = placements;
                }
            }

            // --- Non-grouped placements: group by aspect ratio into variant groups ---
            var nonGroupedPlacements = itemPlacements.Where(ip => !groupedPositions.Contains((ip.BlueprintId, ip.Position))).ToList();
            var variantGroups = new List<(int W, int H, string CropX, string CropY, List<TaskPlacementInfo> Placements)>();
            var seenRatios = new HashSet<string>();
            foreach (var (pos, w, h, cx, cy, bpId, bpName) in nonGroupedPlacements)
            {
                var ratio = (double)w / h;
                var key = $"{ratio:F4}";
                if (seenRatios.Contains(key))
                {
                    // Add to existing variant group's placements
                    var existing = variantGroups.First(vg => $"{(double)vg.W / vg.H:F4}" == key);
                    existing.Placements.Add(new TaskPlacementInfo { BlueprintId = bpId, BlueprintName = bpName, Position = pos, Width = w, Height = h });
                    continue;
                }
                seenRatios.Add(key);
                variantGroups.Add((w, h, cx, cy, new List<TaskPlacementInfo>
                {
                    new() { BlueprintId = bpId, BlueprintName = bpName, Position = pos, Width = w, Height = h }
                }));
            }

            // --- Build generation tasks ---
            var tasks = new List<ArtworkGenerationTask>();
            int baseWidth, baseHeight;
            int variantIdx = 0;

            // Seamless group tasks: one task per group, with combined dimensions
            foreach (var (groupId, groupPlacements) in seamlessGroups)
            {
                // Combined dimensions: width = max width, height = sum of all heights
                var combinedWidth = groupPlacements.Max(p => p.W);
                var combinedHeight = groupPlacements.Sum(p => p.H);

                // Calculate generation dimensions at 2K, clamped to 3:1 ratio
                var ratio = (double)combinedWidth / combinedHeight;
                double genRatio;
                if (ratio > 3.0) genRatio = 3.0;
                else if (ratio < 1.0 / 3.0) genRatio = 1.0 / 3.0;
                else genRatio = ratio;

                double targetArea;
                if (resolutionTier >= 4) targetArea = 4096.0 * 4096;
                else if (resolutionTier >= 2) targetArea = 2048.0 * 2048;
                else targetArea = 1024.0 * 1024;
                var w = Math.Sqrt(targetArea * genRatio);
                var h = Math.Sqrt(targetArea / genRatio);
                var genW = (int)Math.Round(w / 16) * 16;
                var genH = (int)Math.Round(h / 16) * 16;
                if (genW < 64) genW = 64;
                if (genH < 64) genH = 64;

                tasks.Add(new ArtworkGenerationTask
                {
                    Width = genW,
                    Height = genH,
                    NeedsCrop = false,
                    PlacementWidth = combinedWidth,
                    PlacementHeight = combinedHeight,
                    CropX = "center",
                    CropY = "center",
                    NeedsMask = false,
                    VariantIndex = variantIdx++,
                    GroupId = groupId,
                    GroupPlacements = groupPlacements.Select(p => new SeamlessGroupPlacement
                    {
                        Position = p.Position,
                        Width = p.W,
                        Height = p.H,
                        FlipX = p.FlipX,
                        FlipY = p.FlipY,
                        CropX = p.CropX,
                        CropY = p.CropY
                    }).ToList(),
                    Placements = groupPlacements.Select(p => new TaskPlacementInfo
                    {
                        BlueprintId = p.BlueprintId,
                        BlueprintName = p.BlueprintName,
                        Position = p.Position,
                        Width = p.W,
                        Height = p.H
                    }).ToList()
                });
            }

            // Non-grouped variant tasks
            if (variantGroups.Count > 0)
            {
                for (var i = 0; i < variantGroups.Count; i++)
                {
                    var (pw, ph, cx, cy, taskPlacements) = variantGroups[i];
                    var (genW, genH, needsCrop) = ImageGenerationForOpenAI.CalculateCustomResolution(pw, ph, resolutionTier);

                    tasks.Add(new ArtworkGenerationTask
                    {
                        Width = genW,
                        Height = genH,
                        NeedsCrop = needsCrop,
                        PlacementWidth = pw,
                        PlacementHeight = ph,
                        CropX = cx,
                        CropY = cy,
                        NeedsMask = needsCrop,
                        VariantIndex = variantIdx++,
                        Placements = taskPlacements
                    });
                }
            }

            if (tasks.Count > 0)
            {
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

            // Load collection artwork references (custom images only, resized to 1024 max)
            if (collectionId != Guid.Empty)
            {
                var collectionRefs = await _projectCollectionArtworkReferenceRepository.GetByCollectionAndItemIdAsync(collectionId, itemId);
                if (collectionRefs != null && collectionRefs.Any())
                {
                    foreach (var colRef in collectionRefs)
                    {
                        var customImg = await _customImageRepository.GetByIdAsync(colRef.CustomImageId);
                        if (customImg == null) continue;

                        var rawBytes = await _imageService.GetCustomImageAsync(customImg.AppUserId, customImg.Id, customImg.Extension);
                        if (rawBytes == null || rawBytes.Length == 0) continue;

                        // Resize to 1024 max width/height
                        var resizedBytes = await _imageService.ResizeImageMaxAsync(rawBytes, 1024);
                        var dims = await _imageService.GetImageDimensionsAsync(resizedBytes);
                        referenceImages.Add(new ArtworkReferenceImage
                        {
                            ImageBytes = resizedBytes,
                            Type = "custom",
                            Id = customImg.Id.ToString(),
                            Width = dims?.width ?? 0,
                            Height = dims?.height ?? 0
                        });
                    }
                }
            }

            // Determine if this artwork needs upscaling.
            // An artwork needs upscaling if it has product placements or seamless groups,
            // OR if it's not solely used as a reference by other items.
            var needsUpscale = variantGroups.Count > 0 || seamlessGroups.Count > 0;
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
                TotalPlacements = variantGroups.Count + seamlessGroups.Sum(g => g.Value.Count),
                NeedsUpscale = needsUpscale,
                Width = baseWidth,
                Height = baseHeight
            };
        }
    }
}
