CREATE TABLE IF NOT EXISTS public."ProjectBlueprints"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "BlueprintId" INT NOT NULL,
    "Name" VARCHAR(64) NOT NULL,
    "BlueprintJson" TEXT NOT NULL DEFAULT '',
    "PlacementJson" TEXT NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "Status" INT NOT NULL DEFAULT 1
);

ALTER TABLE public."ProjectBlueprints" ADD COLUMN IF NOT EXISTS "Status" INT NOT NULL DEFAULT 1;
