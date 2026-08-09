CREATE TABLE IF NOT EXISTS public."ProjectCollectionProducts"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ProjectBlueprintId" UUID NOT NULL REFERENCES public."ProjectBlueprints"("Id"),
    "BlueprintId" INT NOT NULL,
    "Name" VARCHAR(64) NOT NULL DEFAULT '',
    "Description" TEXT NOT NULL DEFAULT '',
    "SafetyInfo" TEXT NOT NULL DEFAULT '',
    "PricingJson" TEXT NOT NULL DEFAULT '[]',
    "Active" BOOLEAN NOT NULL DEFAULT TRUE
);

ALTER TABLE public."ProjectCollectionProducts" ADD COLUMN IF NOT EXISTS "Active" BOOLEAN NOT NULL DEFAULT TRUE;

CREATE INDEX IF NOT EXISTS "IX_ProjectCollectionProducts_CollectionId_ProjectBlueprintId" ON public."ProjectCollectionProducts" ("CollectionId", "ProjectBlueprintId");
