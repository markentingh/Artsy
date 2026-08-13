CREATE TABLE IF NOT EXISTS public."OrderItems"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderId" UUID NOT NULL REFERENCES public."Orders"("Id") ON DELETE CASCADE,
    "ProductId" VARCHAR(32) NOT NULL DEFAULT '',
    "Quantity" INT NOT NULL DEFAULT 0,
    "VariantId" INT NOT NULL DEFAULT 0,
    "PrintProviderId" INT NOT NULL DEFAULT 0,
    "Cost" INT NOT NULL DEFAULT 0,
    "ShippingCost" INT NOT NULL DEFAULT 0,
    "Status" VARCHAR(16) NOT NULL DEFAULT '',
    "Metadata" TEXT NOT NULL DEFAULT '',
    "DateSentToProduction" TIMESTAMP NULL,
    "DateFulfilled" TIMESTAMP NULL
);

CREATE INDEX IF NOT EXISTS "IX_OrderItems_OrderId" ON public."OrderItems" ("OrderId");
