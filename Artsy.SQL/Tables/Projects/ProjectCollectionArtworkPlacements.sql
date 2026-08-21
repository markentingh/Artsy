CREATE TABLE IF NOT EXISTS public."ProjectCollectionArtworkPlacements"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CollectionArtworkId" UUID NOT NULL REFERENCES public."ProjectCollectionArtwork"("Id") ON DELETE CASCADE,
    "Width" INT NOT NULL DEFAULT 0,
    "Height" INT NOT NULL DEFAULT 0,
    "Index" INT NOT NULL DEFAULT 0,
    "FullSize" BOOLEAN NOT NULL DEFAULT FALSE,
    "PrintifyImageId" VARCHAR(32) NOT NULL DEFAULT '',
    "ResponseId" VARCHAR(64) NOT NULL DEFAULT ''
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProjectCollectionArtworkPlacements_ArtworkId_Index"
    ON public."ProjectCollectionArtworkPlacements" ("CollectionArtworkId", "Index");

CREATE INDEX IF NOT EXISTS "IX_ProjectCollectionArtworkPlacements_ArtworkId"
    ON public."ProjectCollectionArtworkPlacements" ("CollectionArtworkId");
