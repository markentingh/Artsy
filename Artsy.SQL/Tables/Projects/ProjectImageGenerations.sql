CREATE TABLE IF NOT EXISTS public."ProjectImageGenerations"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "ItemId" UUID NULL REFERENCES public."ProjectItems"("Id"),
    "CollectionId" UUID NULL REFERENCES public."ProjectCollections"("Id"),
    "BlueprintId" UUID NULL,
    "AppUserId" UUID NULL REFERENCES public."AppUsers"("Id"),
    "ImageGenerationId" INT NULL REFERENCES public."ImageGeneration"("Id"),
    "InputTextTokens" INT NOT NULL DEFAULT 0,
    "InputImageTokens" INT NOT NULL DEFAULT 0,
    "OutputTokens" INT NOT NULL DEFAULT 0,
    "Tokens" INT NOT NULL DEFAULT 0,
    "ImageModel" VARCHAR(32) NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "Filename" VARCHAR(64) NOT NULL DEFAULT '',
    "IsFullSize" BOOLEAN NOT NULL DEFAULT FALSE,
    "DateCreated" TIMESTAMP NOT NULL DEFAULT NOW()
);

ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "AppUserId" UUID NULL REFERENCES public."AppUsers"("Id");
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "ImageGenerationId" INT NULL REFERENCES public."ImageGeneration"("Id");
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "Tokens" INT NOT NULL DEFAULT 0;
ALTER TABLE public."ProjectImageGenerations" DROP COLUMN IF EXISTS "HasThumbnail";

CREATE INDEX IF NOT EXISTS "IX_ProjectImageGenerations_ProjectId"
    ON public."ProjectImageGenerations" ("ProjectId");

CREATE INDEX IF NOT EXISTS "IX_ProjectImageGenerations_CollectionId"
    ON public."ProjectImageGenerations" ("CollectionId");

CREATE INDEX IF NOT EXISTS "IX_ProjectImageGenerations_ItemId"
    ON public."ProjectImageGenerations" ("ItemId");
