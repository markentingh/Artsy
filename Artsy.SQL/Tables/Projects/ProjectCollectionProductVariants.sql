CREATE TABLE IF NOT EXISTS public."ProjectCollectionProductVariants"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductId" UUID NOT NULL REFERENCES public."ProjectCollectionProducts"("Id") ON DELETE CASCADE,
    "VariantId" INT NOT NULL,
    "Price" DECIMAL(10,2) NOT NULL DEFAULT 0,
    "Enabled" BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProjectCollectionProductVariants_ProductId_VariantId"
    ON public."ProjectCollectionProductVariants" ("ProductId", "VariantId");
