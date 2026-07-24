CREATE TABLE IF NOT EXISTS public."ProjectCollectionAnswers"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "QuestionId" UUID NULL,
    "ItemId" UUID NULL REFERENCES public."ProjectItems"("Id"),
    "Answer" VARCHAR(255) NOT NULL DEFAULT ''
);

ALTER TABLE public."ProjectCollectionAnswers" ADD COLUMN IF NOT EXISTS "QuestionId" UUID NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProjectCollectionAnswers_CollectionId_QuestionId_ItemId"
    ON public."ProjectCollectionAnswers" ("CollectionId", "QuestionId", "ItemId")
    NULLS NOT DISTINCT;
