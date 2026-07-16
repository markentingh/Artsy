CREATE TABLE IF NOT EXISTS public."ProjectItems"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "Index" INT NOT NULL DEFAULT 0
);
