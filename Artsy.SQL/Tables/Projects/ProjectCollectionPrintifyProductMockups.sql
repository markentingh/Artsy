CREATE TABLE IF NOT EXISTS public."ProjectCollectionPrintifyProductMockups"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "PrintifyProductId" UUID NOT NULL REFERENCES public."ProjectCollectionPrintifyProducts"("Id"),
    "VariantIds" TEXT NOT NULL DEFAULT '',
    "Position" VARCHAR(32) NOT NULL DEFAULT '',
    "ImageUrl" TEXT NOT NULL DEFAULT '',
    "IsDefault" BOOLEAN NOT NULL DEFAULT FALSE,
    "Status" INT NOT NULL DEFAULT 1,
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
