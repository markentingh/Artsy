CREATE TABLE IF NOT EXISTS public."OrderShipments"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderId" UUID NOT NULL REFERENCES public."Orders"("Id") ON DELETE CASCADE,
    "Carrier" VARCHAR(16) NOT NULL DEFAULT '',
    "Number" VARCHAR(32) NOT NULL DEFAULT '',
    "Url" VARCHAR(128) NOT NULL DEFAULT '',
    "DeliveredAt" TIMESTAMP NULL
);

CREATE INDEX IF NOT EXISTS "IX_OrderShipments_OrderId" ON public."OrderShipments" ("OrderId");
