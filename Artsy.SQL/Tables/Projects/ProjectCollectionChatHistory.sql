CREATE TABLE IF NOT EXISTS public."ProjectCollectionChatHistory"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "IsUser" BOOLEAN NOT NULL DEFAULT FALSE,
    "ItemId" UUID NULL REFERENCES public."ProjectItems"("Id"),
    "QuestionId" UUID NULL,
    "Text" VARCHAR(64) NOT NULL DEFAULT ''
);
