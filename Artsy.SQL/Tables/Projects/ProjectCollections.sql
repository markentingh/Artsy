CREATE TABLE IF NOT EXISTS public."ProjectCollections"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "Title" VARCHAR(64) NOT NULL,
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "Status" INT NOT NULL DEFAULT 1
);

ALTER TABLE public."ProjectCollections" ADD COLUMN IF NOT EXISTS "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id");
ALTER TABLE public."ProjectCollections" ADD COLUMN IF NOT EXISTS "Status" INT NOT NULL DEFAULT 1;
