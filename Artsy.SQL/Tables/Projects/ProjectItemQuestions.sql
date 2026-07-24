CREATE TABLE IF NOT EXISTS public."ProjectItemQuestions"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "Index" INT NOT NULL DEFAULT 0,
    "Question" VARCHAR(255) NOT NULL DEFAULT '',
    "Status" INT NOT NULL DEFAULT 1
);

ALTER TABLE public."ProjectItemQuestions" ADD COLUMN IF NOT EXISTS "Status" INT NOT NULL DEFAULT 1;
