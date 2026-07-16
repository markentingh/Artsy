CREATE TABLE IF NOT EXISTS public."ProjectCollectionItems"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id")
);
