CREATE TABLE IF NOT EXISTS public."ProjectCollectionProductImages"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ProjectBlueprintId" UUID NOT NULL REFERENCES public."ProjectBlueprints"("Id"),
    "PrintifyImageId" VARCHAR(32) NOT NULL DEFAULT '',
    "ProductImageId" UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
    "ImageModel" VARCHAR(16) NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "Width" INT NOT NULL DEFAULT 0,
    "Height" INT NOT NULL DEFAULT 0,
    "Accepted" BOOLEAN NOT NULL DEFAULT FALSE,
    "ResponseId" VARCHAR(64) NOT NULL DEFAULT '',
    "Active" BOOLEAN NOT NULL DEFAULT TRUE
);
ALTER TABLE public."ProjectCollectionProductImages" ADD COLUMN IF NOT EXISTS "Active" BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE public."ProjectCollectionProductImages" ADD COLUMN IF NOT EXISTS "PrintifyImageId" VARCHAR(32) NOT NULL DEFAULT '';
ALTER TABLE public."ProjectCollectionProductImages" ADD COLUMN IF NOT EXISTS "ProductImageId" UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProjectCollectionProductImages_CollectionId_BlueprintId_ProductImageId"
    ON public."ProjectCollectionProductImages" ("CollectionId", "ProjectBlueprintId", "ProductImageId");

