CREATE TABLE IF NOT EXISTS public."ProjectCollectionProductPlacements"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductId" UUID NOT NULL REFERENCES public."ProjectCollectionProducts"("Id") ON DELETE CASCADE,
    "ArtworkId" UUID NOT NULL REFERENCES public."ProjectCollectionArtwork"("Id"),
    "ArtworkPlacementId" UUID REFERENCES public."ProjectCollectionArtworkPlacements"("Id") ON DELETE SET NULL,
    "Position" VARCHAR(32) NOT NULL DEFAULT '',
    "VariantIds" TEXT NOT NULL DEFAULT '[]',
    "PlacementIndex" INT NOT NULL DEFAULT 0
);

ALTER TABLE public."ProjectCollectionProductPlacements"
    ADD COLUMN IF NOT EXISTS "PlacementIndex" INT NOT NULL DEFAULT 0;
ALTER TABLE public."ProjectCollectionProductPlacements"
    ADD COLUMN IF NOT EXISTS "ArtworkPlacementId" UUID REFERENCES public."ProjectCollectionArtworkPlacements"("Id") ON DELETE SET NULL;
