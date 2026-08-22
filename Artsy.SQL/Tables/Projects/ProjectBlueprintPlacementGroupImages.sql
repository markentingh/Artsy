CREATE TABLE IF NOT EXISTS public."ProjectBlueprintPlacementGroupImages"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id") ON DELETE CASCADE,
    "BlueprintId" INT NOT NULL,
    "GroupId" UUID NOT NULL REFERENCES public."ProjectBlueprintPlacementGroups"("Id") ON DELETE CASCADE,
    "Index" INT NOT NULL DEFAULT 0,
    "ArtworkId" UUID,
    "CustomId" UUID,
    "Position" TEXT,
    "Flipped" BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS "IX_ProjectBlueprintPlacementGroupImages_GroupId"
    ON public."ProjectBlueprintPlacementGroupImages" ("GroupId");

CREATE INDEX IF NOT EXISTS "IX_ProjectBlueprintPlacementGroupImages_ProjectId_BlueprintId"
    ON public."ProjectBlueprintPlacementGroupImages" ("ProjectId", "BlueprintId");

ALTER TABLE public."ProjectBlueprintPlacementGroupImages" ADD COLUMN IF NOT EXISTS "Flipped" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public."ProjectBlueprintPlacementGroupImages" ADD COLUMN IF NOT EXISTS "Position" TEXT;
