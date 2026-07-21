namespace Artsy.API.Models.Projects
{
    public class ProjectChecklistResponse
    {
        public bool ImageGenerationSetup { get; set; }
        public int ImageGenerationSetupCompleted { get; set; }
        public int ImageGenerationSetupTotal { get; set; }

        public bool ItemQuestionsAdded { get; set; }
        public int ItemQuestionsAddedCompleted { get; set; }
        public int ItemQuestionsAddedTotal { get; set; }

        public bool ProductBlueprintsAdded { get; set; }
        public int ProductBlueprintsAddedCompleted { get; set; }
        public int ProductBlueprintsAddedTotal { get; set; }

        public bool QuestionsAdded { get; set; }
        public int QuestionsAddedCompleted { get; set; }
        public int QuestionsAddedTotal { get; set; }

        public bool CollectionsAdded { get; set; }
        public int CollectionsAddedCompleted { get; set; }
        public int CollectionsAddedTotal { get; set; }
    }
}
