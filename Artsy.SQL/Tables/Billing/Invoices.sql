CREATE TABLE IF NOT EXISTS public."Invoices"
(
    "Id" SERIAL PRIMARY KEY,
    "AppUserId" UUID NOT NULL REFERENCES public."AppUsers"("Id"),
    "SubscriptionId" INT NOT NULL REFERENCES public."Subscriptions"("Id"),
    "ProductId" INT NOT NULL REFERENCES public."Products"("Id"),
    "Price" INT NOT NULL DEFAULT 0,
    "DateCreated" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_Invoices_AppUserId" ON public."Invoices" ("AppUserId");
