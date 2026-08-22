CREATE TABLE IF NOT EXISTS public."ProjectBlueprintPlacementGroups"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id") ON DELETE CASCADE,
    "BlueprintId" INT NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_ProjectBlueprintPlacementGroups_ProjectId_BlueprintId"
    ON public."ProjectBlueprintPlacementGroups" ("ProjectId", "BlueprintId");
