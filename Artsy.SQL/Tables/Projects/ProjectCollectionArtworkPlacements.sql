CREATE TABLE IF NOT EXISTS public."ProjectCollectionArtworkPlacements"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CollectionArtworkId" UUID NOT NULL REFERENCES public."ProjectCollectionArtwork"("Id") ON DELETE CASCADE,
    "GroupId" UUID,
    "Position" VARCHAR(64) NOT NULL DEFAULT '',
    "Width" INT NOT NULL DEFAULT 0,
    "Height" INT NOT NULL DEFAULT 0,
    "Index" INT NOT NULL DEFAULT 0,
    "FullSize" BOOLEAN NOT NULL DEFAULT FALSE,
    "PrintifyImageId" VARCHAR(32) NOT NULL DEFAULT '',
    "ResponseId" VARCHAR(64) NOT NULL DEFAULT '',
    "OptionalPrompt" TEXT NOT NULL DEFAULT ''
);

-- Drop old unique index that conflicts with multi-group placements
DROP INDEX IF EXISTS "UX_ProjectCollectionArtworkPlacements_ArtworkId_Index";

-- Add GroupId and Position columns for seamless placement group support
ALTER TABLE public."ProjectCollectionArtworkPlacements" ADD COLUMN IF NOT EXISTS "GroupId" UUID;
ALTER TABLE public."ProjectCollectionArtworkPlacements" ADD COLUMN IF NOT EXISTS "Position" VARCHAR(64) NOT NULL DEFAULT '';

-- Add OptionalPrompt column for per-placement regeneration prompts
ALTER TABLE public."ProjectCollectionArtworkPlacements" ADD COLUMN IF NOT EXISTS "OptionalPrompt" TEXT NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS "IX_ProjectCollectionArtworkPlacements_ArtworkId"
    ON public."ProjectCollectionArtworkPlacements" ("CollectionArtworkId");

CREATE INDEX IF NOT EXISTS "IX_ProjectCollectionArtworkPlacements_GroupId"
    ON public."ProjectCollectionArtworkPlacements" ("GroupId");
