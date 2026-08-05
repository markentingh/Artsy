CREATE TABLE IF NOT EXISTS public."AppUserAITokens"
(
    "Id" SERIAL PRIMARY KEY,
    "AppUserId" UUID NOT NULL REFERENCES public."AppUsers"("Id"),
    "InvoiceId" INT NULL REFERENCES public."Invoices"("Id"),
    "BillingMonth" DATE NOT NULL,
    "Tokens" INT NOT NULL DEFAULT 0,
    "TokensUsed" INT NOT NULL DEFAULT 0,
    "DateCreated" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_AppUserAITokens_AppUserId"
    ON public."AppUserAITokens" ("AppUserId");

CREATE INDEX IF NOT EXISTS "IX_AppUserAITokens_BillingMonth"
    ON public."AppUserAITokens" ("BillingMonth");
