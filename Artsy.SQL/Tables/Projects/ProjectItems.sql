CREATE TABLE IF NOT EXISTS public."ProjectItems"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "Index" INT NOT NULL DEFAULT 0,
    "Title" VARCHAR(64) NULL,
    "SocialMedia" BOOLEAN NOT NULL DEFAULT FALSE
);

ALTER TABLE public."ProjectItems" ADD COLUMN IF NOT EXISTS "SocialMedia" BOOLEAN NOT NULL DEFAULT FALSE;
