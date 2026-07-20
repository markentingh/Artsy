CREATE TABLE IF NOT EXISTS public."ProjectBlueprints"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "BlueprintId" INT NOT NULL,
    "Name" VARCHAR(64) NOT NULL,
    "BlueprintJson" TEXT NOT NULL DEFAULT '',
    "PlacementJson" TEXT NOT NULL DEFAULT ''
);
