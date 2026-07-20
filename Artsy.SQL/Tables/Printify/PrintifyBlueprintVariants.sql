CREATE TABLE IF NOT EXISTS public."PrintifyBlueprintVariants"
(
    "VariantId" INT PRIMARY KEY,
    "BlueprintId" INT NOT NULL,
    "PrintProviderId" INT NOT NULL,
    "Title" VARCHAR(256) NOT NULL DEFAULT '',
    "Options" TEXT NOT NULL DEFAULT '{}',
    "DecorationMethods" TEXT NOT NULL DEFAULT '[]',
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_printify_variants_blueprint_provider
    ON public."PrintifyBlueprintVariants" ("BlueprintId", "PrintProviderId");
