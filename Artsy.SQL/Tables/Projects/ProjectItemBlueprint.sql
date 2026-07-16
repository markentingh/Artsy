CREATE TABLE IF NOT EXISTS public."ProjectItemBlueprint"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "BlueprintId" INT NOT NULL,
    "Name" VARCHAR(64) NOT NULL,
    "BlueprintJson" TEXT NOT NULL DEFAULT ''
);
