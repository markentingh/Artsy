CREATE TABLE IF NOT EXISTS public."HangfireOrders"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "DateChecked" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "NewOrders" INT NOT NULL DEFAULT 0,
    "UpdatedOrders" INT NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS "IX_HangfireOrders_DateChecked" ON public."HangfireOrders" ("DateChecked" DESC);
