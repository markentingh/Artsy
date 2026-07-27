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
    "PricingJson" TEXT NOT NULL DEFAULT '[]'
);
