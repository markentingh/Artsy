CREATE TABLE IF NOT EXISTS public."PrintifyBlueprintVariants"
(
    "VariantId" INT PRIMARY KEY,
    "BlueprintId" INT NOT NULL,
    "PrintProviderId" INT NOT NULL,
    "Color" VARCHAR(256) NOT NULL DEFAULT '',
    "HexColor" VARCHAR(32) NOT NULL DEFAULT '',
    "Options" TEXT NOT NULL DEFAULT '{}',
    "Size" VARCHAR(64) NOT NULL DEFAULT '',
    "DecorationMethods" TEXT NOT NULL DEFAULT '[]',
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "Depth" VARCHAR(32),
    "Design" VARCHAR(32),
    "Finish" VARCHAR(32),
    "Flavor" VARCHAR(32),
    "Hands" VARCHAR(32),
    "Length" VARCHAR(32),
    "Material" VARCHAR(32),
    "Paper" VARCHAR(32),
    "Quantity" VARCHAR(32),
    "Scent" VARCHAR(32),
    "Shape" VARCHAR(32),
    "Surface" VARCHAR(32),
    "Type" VARCHAR(32),
    "Voltage" VARCHAR(32),
    "Weight" VARCHAR(32)
);

CREATE INDEX IF NOT EXISTS idx_printify_variants_blueprint_provider
    ON public."PrintifyBlueprintVariants" ("BlueprintId", "PrintProviderId");

ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Size" VARCHAR(64) NOT NULL DEFAULT '';
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Color" VARCHAR(256) NOT NULL DEFAULT '';
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "HexColor" VARCHAR(32) NOT NULL DEFAULT '';
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Depth" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Design" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Finish" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Flavor" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Hands" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Length" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Material" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Paper" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Quantity" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Scent" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Shape" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Surface" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Type" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Voltage" VARCHAR(32);
ALTER TABLE public."PrintifyBlueprintVariants" ADD COLUMN IF NOT EXISTS "Weight" VARCHAR(32);

