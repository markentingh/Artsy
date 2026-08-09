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
    "Prompt" TEXT NOT NULL DEFAULT '',
    "Filename" VARCHAR(64) NOT NULL DEFAULT '',
    "Resolution" VARCHAR(16) NOT NULL DEFAULT '',
    "InputImages" INT NOT NULL DEFAULT 0,
    "InputImageJson" TEXT NOT NULL DEFAULT '[]',
    "Type" INT NOT NULL DEFAULT 0,
    "Cost" INT NOT NULL DEFAULT 0,
    "DateYear" INT NOT NULL DEFAULT EXTRACT(YEAR FROM NOW())::int,
    "DateMonth" INT NOT NULL DEFAULT EXTRACT(MONTH FROM NOW())::int,
    "DateDay" INT NOT NULL DEFAULT EXTRACT(DAY FROM NOW())::int,
    "DateCreated" TIMESTAMP NOT NULL DEFAULT NOW()
);

ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "AppUserId" UUID NULL REFERENCES public."AppUsers"("Id");
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "ImageGenerationId" INT NULL REFERENCES public."ImageGeneration"("Id");
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "Tokens" INT NOT NULL DEFAULT 0;
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "Resolution" VARCHAR(16) NOT NULL DEFAULT '';
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "InputImages" INT NOT NULL DEFAULT 0;
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "InputImageJson" TEXT NOT NULL DEFAULT '[]';
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "Type" INT NOT NULL DEFAULT 0;
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "Cost" INT NOT NULL DEFAULT 0;
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "DateYear" INT NOT NULL DEFAULT EXTRACT(YEAR FROM NOW())::int;
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "DateMonth" INT NOT NULL DEFAULT EXTRACT(MONTH FROM NOW())::int;
ALTER TABLE public."ProjectImageGenerations" ADD COLUMN IF NOT EXISTS "DateDay" INT NOT NULL DEFAULT EXTRACT(DAY FROM NOW())::int;

-- Populate DateYear, DateMonth, DateDay from DateCreated for existing records
UPDATE public."ProjectImageGenerations"
SET "DateYear" = EXTRACT(YEAR FROM "DateCreated")::int,
    "DateMonth" = EXTRACT(MONTH FROM "DateCreated")::int,
    "DateDay" = EXTRACT(DAY FROM "DateCreated")::int
WHERE "DateYear" != EXTRACT(YEAR FROM "DateCreated")::int
   OR "DateMonth" != EXTRACT(MONTH FROM "DateCreated")::int
   OR "DateDay" != EXTRACT(DAY FROM "DateCreated")::int;

ALTER TABLE public."ProjectImageGenerations" DROP COLUMN IF EXISTS "HasThumbnail";
ALTER TABLE public."ProjectImageGenerations" DROP COLUMN IF EXISTS "IsFullSize";
ALTER TABLE public."ProjectImageGenerations" DROP COLUMN IF EXISTS "ImageModel";

CREATE INDEX IF NOT EXISTS "IX_ProjectImageGenerations_ProjectId"
    ON public."ProjectImageGenerations" ("ProjectId");

CREATE INDEX IF NOT EXISTS "IX_ProjectImageGenerations_CollectionId"
    ON public."ProjectImageGenerations" ("CollectionId");

CREATE INDEX IF NOT EXISTS "IX_ProjectImageGenerations_ItemId"
    ON public."ProjectImageGenerations" ("ItemId");

CREATE INDEX IF NOT EXISTS "IX_ProjectImageGenerations_DateYear_DateMonth_DateDay"
    ON public."ProjectImageGenerations" ("DateYear", "DateMonth", "DateDay");
