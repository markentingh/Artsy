CREATE TABLE IF NOT EXISTS public."ProjectBlueprints"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "BlueprintId" INT NOT NULL,
    "Name" VARCHAR(64) NOT NULL,
    "BlueprintJson" TEXT NOT NULL DEFAULT '',
    "PlacementJson" TEXT NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "Status" INT NOT NULL DEFAULT 1,
    "Description" TEXT NOT NULL DEFAULT '',
    "SafetyInfo" TEXT NOT NULL DEFAULT '',
    "PricingJson" TEXT NOT NULL DEFAULT '[]'
);

ALTER TABLE public."ProjectBlueprints" ADD COLUMN IF NOT EXISTS "Status" INT NOT NULL DEFAULT 1;
ALTER TABLE public."ProjectBlueprints" ADD COLUMN IF NOT EXISTS "Description" TEXT NOT NULL DEFAULT '';
ALTER TABLE public."ProjectBlueprints" ADD COLUMN IF NOT EXISTS "SafetyInfo" TEXT NOT NULL DEFAULT '';
ALTER TABLE public."ProjectBlueprints" ADD COLUMN IF NOT EXISTS "PricingJson" TEXT NOT NULL DEFAULT '[]';
ALTER TABLE public."ProjectBlueprints" ADD COLUMN IF NOT EXISTS "PrintProviderId" INT NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS "IX_ProjectBlueprints_ProjectId_Status" ON public."ProjectBlueprints" ("ProjectId", "Status");
