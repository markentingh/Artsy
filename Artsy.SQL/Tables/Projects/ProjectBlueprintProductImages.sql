CREATE TABLE IF NOT EXISTS public."ProjectBlueprintProductImages"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "ProjectBlueprintId" UUID NOT NULL REFERENCES public."ProjectBlueprints"("Id"),
    "Title" VARCHAR(32) NOT NULL DEFAULT '',
    "VariantColor" VARCHAR(32) NOT NULL DEFAULT '',
    "Status" INT NOT NULL DEFAULT 1,
    "Prompt" TEXT NOT NULL DEFAULT '',
    "ImageId" UUID NULL,
    "DateCreated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE public."ProjectBlueprintProductImages" ADD COLUMN IF NOT EXISTS "ImageId" UUID NULL;

CREATE INDEX IF NOT EXISTS "IX_ProjectBlueprintProductImages_ProjectBlueprintId"
    ON public."ProjectBlueprintProductImages" ("ProjectBlueprintId");

CREATE INDEX IF NOT EXISTS "IX_ProjectBlueprintProductImages_ProjectId_Status" ON public."ProjectBlueprintProductImages" ("ProjectId", "Status");
