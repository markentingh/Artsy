CREATE TABLE IF NOT EXISTS public."ProjectItemPreviews"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "ImageModel" VARCHAR(16) NOT NULL DEFAULT '',
    "ImageModelJson" TEXT NOT NULL DEFAULT ''
);
