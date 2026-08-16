CREATE TABLE IF NOT EXISTS public."Orders"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "AppUserId" UUID NOT NULL,
    "PrintifyShopId" INT NOT NULL,
    "OrderId" VARCHAR(32) NOT NULL,
    "AppOrderId" VARCHAR(64) NOT NULL DEFAULT '',
    "AddressTo" TEXT NOT NULL DEFAULT '',
    "Metadata" TEXT NOT NULL DEFAULT '',
    "TotalPrice" INT NOT NULL DEFAULT 0,
    "TotalShipping" INT NOT NULL DEFAULT 0,
    "TotalTax" INT NOT NULL DEFAULT 0,
    "Status" VARCHAR(16) NOT NULL DEFAULT '',
    "ShippingMethod" INT NOT NULL DEFAULT 0,
    "IsExpress" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsEconomyShipping" BOOLEAN NOT NULL DEFAULT FALSE,
    "DateCreated" TIMESTAMP NULL,
    "DateSentToProduction" TIMESTAMP NULL,
    "DateFulfilled" TIMESTAMP NULL,
    "PrintifyConnect" TEXT NOT NULL DEFAULT '',
    "DataHash" TEXT NOT NULL DEFAULT '',
    "ResponseJson" TEXT NOT NULL DEFAULT '',
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "Updated" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "UQ_Orders_OrderId" UNIQUE ("OrderId")
);

CREATE INDEX IF NOT EXISTS "IX_Orders_AppUserId" ON public."Orders" ("AppUserId");
CREATE INDEX IF NOT EXISTS "IX_Orders_PrintifyShopId" ON public."Orders" ("PrintifyShopId");
CREATE INDEX IF NOT EXISTS "IX_Orders_Status" ON public."Orders" ("Status");
CREATE INDEX IF NOT EXISTS "IX_Orders_DateCreated" ON public."Orders" ("DateCreated");
