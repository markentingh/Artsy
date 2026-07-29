CREATE TABLE IF NOT EXISTS public."PrintifyBlueprintVariants"
(
    "VariantId" INT PRIMARY KEY,
    "BlueprintId" INT NOT NULL,
    "PrintProviderId" INT NOT NULL,
    "Color" VARCHAR(256) NOT NULL DEFAULT '',
    "Options" TEXT NOT NULL DEFAULT '{}',
    "Size" VARCHAR(64) NOT NULL DEFAULT '',
    "DecorationMethods" TEXT NOT NULL DEFAULT '[]',
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_printify_variants_blueprint_provider
    ON public."PrintifyBlueprintVariants" ("BlueprintId", "PrintProviderId");

ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Size" VARCHAR(64) NOT NULL DEFAULT '';
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Color" VARCHAR(256) NOT NULL DEFAULT '';

