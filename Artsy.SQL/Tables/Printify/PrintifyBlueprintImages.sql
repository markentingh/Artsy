CREATE TABLE IF NOT EXISTS public."PrintifyBlueprintImages"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "BlueprintId" INT NOT NULL,
    "ImageIndex" INT NOT NULL DEFAULT 0,
    "Variants" TEXT NOT NULL DEFAULT '[]',
    "Type" INT NOT NULL DEFAULT 0,
    "Position" INT NOT NULL DEFAULT 1,
    "DateCreated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "FK_PrintifyBlueprintImages_PrintifyBlueprints" FOREIGN KEY ("BlueprintId")
        REFERENCES public."PrintifyBlueprints" ("BlueprintId") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_printify_blueprint_images_bp_idx ON public."PrintifyBlueprintImages" ("BlueprintId", "ImageIndex");

ALTER TABLE public."PrintifyBlueprintImages" ADD COLUMN IF NOT EXISTS "Variants" TEXT NOT NULL DEFAULT '[]';
ALTER TABLE public."PrintifyBlueprintImages" ADD COLUMN IF NOT EXISTS "Position" INT NOT NULL DEFAULT 1;
ALTER TABLE public."PrintifyBlueprintImages" DROP COLUMN IF EXISTS "VariantId";