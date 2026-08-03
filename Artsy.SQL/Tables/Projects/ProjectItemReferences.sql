CREATE TABLE IF NOT EXISTS public."ProjectItemReferences"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CustomImageId" UUID NULL REFERENCES public."CustomImages"("Id"),
    "ArtworkId" UUID NULL REFERENCES public."ProjectItems"("Id"),
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE public."ProjectItemReferences" ADD COLUMN IF NOT EXISTS "ArtworkId" UUID NULL REFERENCES public."ProjectItems"("Id");
ALTER TABLE public."ProjectItemReferences" ADD COLUMN IF NOT EXISTS "CustomImageId" UUID NULL REFERENCES public."CustomImages"("Id");
ALTER TABLE public."ProjectItemReferences" DROP COLUMN IF EXISTS "FileName";
ALTER TABLE public."ProjectItemReferences" DROP COLUMN IF EXISTS "Extension";
