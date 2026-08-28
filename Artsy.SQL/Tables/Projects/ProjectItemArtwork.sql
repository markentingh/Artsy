CREATE TABLE IF NOT EXISTS public."ProjectItemArtwork"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "ImageModel" VARCHAR(16) NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "ArtworkType" VARCHAR(16) NOT NULL DEFAULT 'ai',
    "CustomImageId" UUID NULL,
    "IgnoredQuestions" TEXT NULL,
    "OpacityJson" TEXT NULL,
    "AspectRatio" VARCHAR(16) NOT NULL DEFAULT '1:1'
);

ALTER TABLE public."ProjectItemArtwork" ADD COLUMN IF NOT EXISTS "ArtworkType" VARCHAR(16) NOT NULL DEFAULT 'ai';
ALTER TABLE public."ProjectItemArtwork" ADD COLUMN IF NOT EXISTS "CustomImageId" UUID NULL;
ALTER TABLE public."ProjectItemArtwork" ADD COLUMN IF NOT EXISTS "IgnoredQuestions" TEXT NULL;
ALTER TABLE public."ProjectItemArtwork" ADD COLUMN IF NOT EXISTS "OpacityJson" TEXT NULL;
ALTER TABLE public."ProjectItemArtwork" ADD COLUMN IF NOT EXISTS "AspectRatio" VARCHAR(16) NOT NULL DEFAULT '1:1';
ALTER TABLE public."ProjectItemArtwork" ADD COLUMN IF NOT EXISTS "Design" VARCHAR(16) NOT NULL DEFAULT 'artwork';
ALTER TABLE public."ProjectItemArtwork" ADD COLUMN IF NOT EXISTS "OptionalPrompt" TEXT NULL;

CREATE INDEX IF NOT EXISTS "IX_ProjectItemArtwork_ProjectId" ON public."ProjectItemArtwork" ("ProjectId");
CREATE INDEX IF NOT EXISTS "IX_ProjectItemArtwork_ItemId" ON public."ProjectItemArtwork" ("ItemId");
