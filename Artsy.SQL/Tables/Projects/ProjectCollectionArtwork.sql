CREATE TABLE IF NOT EXISTS public."ProjectCollectionArtwork"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "Active" BOOLEAN NOT NULL DEFAULT FALSE,
    "Width" INT NOT NULL DEFAULT 0,
    "Height" INT NOT NULL DEFAULT 0,
    "ImageModel" VARCHAR(16) NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "Accepted" BOOLEAN NOT NULL DEFAULT FALSE,
    "ResponseId" VARCHAR(64) NOT NULL DEFAULT '',
    "FullSize" BOOLEAN NOT NULL DEFAULT FALSE
);

ALTER TABLE public."ProjectCollectionArtwork" ADD COLUMN IF NOT EXISTS "Accepted" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public."ProjectCollectionArtwork" ADD COLUMN IF NOT EXISTS "ResponseId" VARCHAR(32) NOT NULL DEFAULT '';
ALTER TABLE public."ProjectCollectionArtwork" ADD COLUMN IF NOT EXISTS "FullSize" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public."ProjectCollectionArtwork" ALTER COLUMN "ResponseId" TYPE VARCHAR(64);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProjectCollectionArtwork_CollectionId_ItemId"
    ON public."ProjectCollectionArtwork" ("CollectionId", "ItemId");
