CREATE TABLE IF NOT EXISTS public."ProjectItemArtwork"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "ImageModel" VARCHAR(16) NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "ArtworkType" VARCHAR(16) NOT NULL DEFAULT 'ai',
    "CustomImageId" UUID NULL
);

ALTER TABLE public."ProjectItemArtwork" ADD COLUMN IF NOT EXISTS "ArtworkType" VARCHAR(16) NOT NULL DEFAULT 'ai';
ALTER TABLE public."ProjectItemArtwork" ADD COLUMN IF NOT EXISTS "CustomImageId" UUID NULL;
