CREATE TABLE IF NOT EXISTS public."ProjectCollectionAnswers"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ItemId" UUID NULL REFERENCES public."ProjectItems"("Id"),
    "Answer" VARCHAR(64) NOT NULL DEFAULT ''
);
