CREATE TABLE IF NOT EXISTS public."PrintifyBlueprintVariantPlaceholders"
(
    "VariantId" INT NOT NULL,
    "Position" VARCHAR(64) NOT NULL DEFAULT '',
    "DecorationMethod" VARCHAR(64) NOT NULL DEFAULT '',
    "Height" INT NOT NULL DEFAULT 0,
    "Width" INT NOT NULL DEFAULT 0,
    PRIMARY KEY ("VariantId", "Position")
);

CREATE INDEX IF NOT EXISTS idx_printify_placeholders_variant
    ON public."PrintifyBlueprintVariantPlaceholders" ("VariantId");
