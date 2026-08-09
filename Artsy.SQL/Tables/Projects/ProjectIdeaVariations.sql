CREATE TABLE IF NOT EXISTS public."ProjectIdeaVariations"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectIdeaId" UUID NOT NULL REFERENCES public."ProjectIdeas"("Id") ON DELETE CASCADE,
    "Title" VARCHAR(64) NOT NULL DEFAULT '',
    "Description" TEXT NOT NULL DEFAULT '',
    "IdeaJson" TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS "IX_ProjectIdeaVariations_ProjectIdeaId" ON public."ProjectIdeaVariations" ("ProjectIdeaId");
