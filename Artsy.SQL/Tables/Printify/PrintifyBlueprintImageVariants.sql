CREATE TABLE IF NOT EXISTS public."PrintifyBlueprintImageVariants"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "BlueprintImageId" UUID NOT NULL REFERENCES public."PrintifyBlueprintImages"("Id") ON DELETE CASCADE,
    "VariantColor" VARCHAR(32) NOT NULL DEFAULT '',
    "DateCreated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_PrintifyBlueprintImageVariants_ImageId_Color"
    ON public."PrintifyBlueprintImageVariants" ("BlueprintImageId", "VariantColor");
