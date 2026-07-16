CREATE TABLE IF NOT EXISTS public."ProjectCollectionArtwork"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "Active" BOOLEAN NOT NULL DEFAULT FALSE,
    "Images" INT NOT NULL DEFAULT 0,
    "ImageModel" VARCHAR(16) NOT NULL DEFAULT '',
    "ImageModelJson" TEXT NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT ''
);
