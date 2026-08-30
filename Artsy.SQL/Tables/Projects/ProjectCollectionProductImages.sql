CREATE TABLE IF NOT EXISTS public."ProjectCollectionProductImages"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ProjectBlueprintId" UUID REFERENCES public."ProjectBlueprints"("Id"),
    "PrintifyImageId" VARCHAR(32) NOT NULL DEFAULT '',
    "ProductImageId" UUID,
    "VariantColor" VARCHAR(64) NOT NULL DEFAULT '',
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
ALTER TABLE public."ProjectCollectionProductImages" ADD COLUMN IF NOT EXISTS "ProductImageId" UUID;
ALTER TABLE public."ProjectCollectionProductImages" ADD COLUMN IF NOT EXISTS "VariantColor" VARCHAR(64) NOT NULL DEFAULT '';
ALTER TABLE public."ProjectCollectionProductImages" ADD COLUMN IF NOT EXISTS "SelectedMockups" TEXT NOT NULL DEFAULT '';
ALTER TABLE public."ProjectCollectionProductImages" ADD COLUMN IF NOT EXISTS "Generated" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public."ProjectCollectionProductImages" ALTER COLUMN "ProjectBlueprintId" DROP NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProjectCollectionProductImages_CollectionId_BlueprintId_ProductImageId"
    ON public."ProjectCollectionProductImages" ("CollectionId", "ProjectBlueprintId", "ProductImageId");


CREATE INDEX IF NOT EXISTS "IX_ProjectCollectionProductImages_ProjectId" ON public."ProjectCollectionProductImages" ("ProjectId");

