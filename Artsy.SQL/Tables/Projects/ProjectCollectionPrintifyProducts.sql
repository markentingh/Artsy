CREATE TABLE IF NOT EXISTS public."ProjectCollectionPrintifyProducts"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ProductId" UUID NOT NULL REFERENCES public."ProjectCollectionProducts"("Id"),
    "PrintifyProductId" VARCHAR(32) NOT NULL DEFAULT '',
    "PrintifyShopId" INT NOT NULL DEFAULT 0,
    "PrintifyUserId" INT NOT NULL DEFAULT 0,
    "ProviderId" INT NOT NULL DEFAULT 0,
    "Published" BOOLEAN NOT NULL DEFAULT FALSE,
    "Status" INT NOT NULL DEFAULT 1,
    "RequestJson" TEXT NOT NULL DEFAULT '',
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
ALTER TABLE public."ProjectCollectionPrintifyProducts" ADD COLUMN IF NOT EXISTS "RequestJson" TEXT NOT NULL DEFAULT '';