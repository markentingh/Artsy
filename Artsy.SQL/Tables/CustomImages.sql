CREATE TABLE IF NOT EXISTS public."CustomImages"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "AppUserId" UUID NOT NULL REFERENCES public."AppUsers"("Id"),
    "FileName" VARCHAR(255) NOT NULL DEFAULT '',
    "Extension" VARCHAR(10) NOT NULL DEFAULT '.jpg',
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_CustomImages_AppUserId_Created" ON public."CustomImages" ("AppUserId", "Created" DESC);
