CREATE TABLE IF NOT EXISTS public."ProjectCollectionProductImages"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ProjectBlueprintId" UUID NOT NULL REFERENCES public."ProjectBlueprints"("Id"),
    "Variant" INT NOT NULL DEFAULT 0,
    "Placement" INT NOT NULL DEFAULT 0,
    "ImageModel" VARCHAR(16) NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "Width" INT NOT NULL DEFAULT 0,
    "Height" INT NOT NULL DEFAULT 0,
    "Accepted" BOOLEAN NOT NULL DEFAULT FALSE,
    "ResponseId" VARCHAR(64) NOT NULL DEFAULT ''
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProjectCollectionProductImages_CollectionId_BlueprintId_Variant_Placement"
    ON public."ProjectCollectionProductImages" ("CollectionId", "ProjectBlueprintId", "Variant", "Placement");
