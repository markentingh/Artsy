CREATE TABLE IF NOT EXISTS public."ProjectItemReferences"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "FileName" VARCHAR(255) NOT NULL DEFAULT '',
    "Extension" VARCHAR(10) NOT NULL DEFAULT '.jpg',
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
