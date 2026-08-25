using Artsy.API.Models.Projects;
using Artsy.Data.Entities.Projects;

namespace Artsy.API.Services
{
    /// <summary>
    /// Describes a single placement within a seamless placement group.
    /// </summary>
    public class SeamlessGroupPlacement
    {
        public string Position { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
        public string CropX { get; set; } = "center";
        public string CropY { get; set; } = "center";
    }

    /// <summary>
    /// Describes a single placement that maps to a generation task.
    /// </summary>
    public class TaskPlacementInfo
    {
        public int BlueprintId { get; set; }
        public string BlueprintName { get; set; } = "";
        public string Position { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Describes a single image generation that must be performed for an artwork.
    /// An artwork may require multiple generations if it has placements with different aspect ratios.
    /// </summary>
    public class ArtworkGenerationTask
    {
        /// <summary>Width in pixels for the generation request (rounded to multiples of 16).</summary>
        public int Width { get; set; }

        /// <summary>Height in pixels for the generation request (rounded to multiples of 16).</summary>
        public int Height { get; set; }

        /// <summary>True if the generated image will need post-generation cropping (ratio > 3:1).</summary>
        public bool NeedsCrop { get; set; }

        /// <summary>Placement width for cropping (0 if no crop needed).</summary>
        public int PlacementWidth { get; set; }

        /// <summary>Placement height for cropping (0 if no crop needed).</summary>
        public int PlacementHeight { get; set; }

        /// <summary>Crop X alignment for post-generation cropping.</summary>
        public string CropX { get; set; } = "center";

        /// <summary>Crop Y alignment for post-generation cropping.</summary>
        public string CropY { get; set; } = "center";

        /// <summary>True if a mask is needed for this generation (when crop is required and input images exist).</summary>
        public bool NeedsMask { get; set; }

        /// <summary>0-based variant index for this task.</summary>
        public int VariantIndex { get; set; }

        /// <summary>If non-null, this task is a seamless placement group task. The generated image will be cut up into individual placements.</summary>
        public Guid? GroupId { get; set; }

        /// <summary>Placements within the seamless group (only set when GroupId is non-null).</summary>
        public List<SeamlessGroupPlacement> GroupPlacements { get; set; } = new();

        /// <summary>All placements that map to this task (for debugging/estimation).</summary>
        public List<TaskPlacementInfo> Placements { get; set; } = new();
    }

    /// <summary>
    /// A reference image that will be passed as input to the generation.
    /// </summary>
    public class ArtworkReferenceImage
    {
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public string Type { get; set; } = ""; // "artwork" or "custom"
        public string Id { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// The complete plan for generating an artwork, including all variant tasks,
    /// reference images, prompt, opacity settings, and mask requirements.
    /// </summary>
    public class ArtworkGenerationPlan
    {
        /// <summary>The item artwork entity.</summary>
        public ProjectItemArtwork Artwork { get; set; } = null!;

        /// <summary>The final prompt with answers and chroma key instructions appended.</summary>
        public string FinalPrompt { get; set; } = "";

        /// <summary>True if opacity/chroma key processing is needed.</summary>
        public bool HasOpacity { get; set; }

        /// <summary>Reference images to pass as input to the generation.</summary>
        public List<ArtworkReferenceImage> ReferenceImages { get; set; } = new();

        /// <summary>Individual generation tasks (one per variant/placement group, or a single task if no placements).</summary>
        public List<ArtworkGenerationTask> Tasks { get; set; } = new();

        /// <summary>Total number of placement variants (0 if no placements).</summary>
        public int TotalPlacements { get; set; }

        /// <summary>True if this artwork needs upscaling (has product placements or is not solely used as a reference).</summary>
        public bool NeedsUpscale { get; set; } = true;

        /// <summary>Base artwork width (from first task or aspect ratio).</summary>
        public int Width { get; set; }

        /// <summary>Base artwork height (from first task or aspect ratio).</summary>
        public int Height { get; set; }
    }

    public interface IArtworkGenerationPlanService
    {
        /// <summary>
        /// Builds a complete generation plan for an item's artwork, including:
        /// - Blueprint placement analysis and variant grouping
        /// - Dimension calculation per variant (using CalculateCustomResolution or aspect ratio)
        /// - Reference image collection (artwork refs + custom image refs)
        /// - Mask requirement detection (needed when crop is required)
        /// - Prompt building with answers + chroma key instructions
        /// </summary>
        Task<ArtworkGenerationPlan> BuildPlanAsync(
            Guid projectId,
            Guid collectionId,
            Guid itemId,
            string? requestedChanges = null,
            List<GenerateProjectItemPreviewAnswer>? answers = null,
            int resolutionTier = 1);
    }
}
