CREATE TABLE IF NOT EXISTS public."ProjectQuestions"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "Index" INT NOT NULL DEFAULT 0,
    "Question" VARCHAR(255) NOT NULL DEFAULT '',
    "Status" INT NOT NULL DEFAULT 1
);

ALTER TABLE public."ProjectQuestions" ADD COLUMN IF NOT EXISTS "Status" INT NOT NULL DEFAULT 1;
