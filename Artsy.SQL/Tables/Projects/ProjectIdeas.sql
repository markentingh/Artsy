CREATE TABLE IF NOT EXISTS public."ProjectIdeas"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "Title" VARCHAR(64) NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "MetadataJson" TEXT NOT NULL DEFAULT '',
    "Created" TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE public."ProjectIdeas" ADD COLUMN IF NOT EXISTS "MetadataJson" TEXT NOT NULL DEFAULT '';