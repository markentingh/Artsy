CREATE TABLE IF NOT EXISTS public."PrintifyBlueprintImageVariants"
(
    "ImageId" UUID NOT NULL,
    "VariantId" INT NOT NULL,
    PRIMARY KEY ("ImageId", "VariantId")
);

CREATE INDEX IF NOT EXISTS idx_printify_image_variants_variant_id
    ON public."PrintifyBlueprintImageVariants" ("VariantId");
