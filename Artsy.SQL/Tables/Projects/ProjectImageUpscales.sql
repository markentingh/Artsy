CREATE TABLE IF NOT EXISTS public."ProjectImageUpscales"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "ArtworkId" UUID NOT NULL REFERENCES public."ProjectCollectionArtwork"("Id"),
    "Width" INT NOT NULL DEFAULT 0,
    "Height" INT NOT NULL DEFAULT 0,
    "Scale" INT NOT NULL DEFAULT 2,
    "Created" TIMESTAMP NOT NULL DEFAULT NOW()
);
