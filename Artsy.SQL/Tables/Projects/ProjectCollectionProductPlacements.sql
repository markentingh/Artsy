CREATE TABLE IF NOT EXISTS public."ProjectCollectionProductPlacements"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductId" UUID NOT NULL REFERENCES public."ProjectCollectionProducts"("Id") ON DELETE CASCADE,
    "ArtworkId" UUID NOT NULL REFERENCES public."ProjectCollectionArtwork"("Id"),
    "Position" VARCHAR(32) NOT NULL DEFAULT '',
    "VariantIds" TEXT NOT NULL DEFAULT '[]'
);
