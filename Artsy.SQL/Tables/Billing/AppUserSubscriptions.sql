CREATE TABLE IF NOT EXISTS public."AppUserSubscriptions"
(
    "Id" SERIAL PRIMARY KEY,
    "AppUserId" UUID NOT NULL REFERENCES public."AppUsers"("Id"),
    "SubscriptionId" INT NOT NULL REFERENCES public."Subscriptions"("Id"),
    "StartDate" TIMESTAMP NOT NULL DEFAULT NOW(),
    "EndDate" TIMESTAMP NULL,
    "Cancelled" BOOLEAN NOT NULL DEFAULT FALSE,
    "DateCreated" TIMESTAMP NOT NULL DEFAULT NOW()
);

ALTER TABLE public."AppUserSubscriptions" ADD COLUMN IF NOT EXISTS "Cancelled" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE INDEX IF NOT EXISTS "IX_AppUserSubscriptions_AppUserId" ON public."AppUserSubscriptions" ("AppUserId");
CREATE INDEX IF NOT EXISTS "IX_AppUserSubscriptions_AppUserId_Cancelled" ON public."AppUserSubscriptions" ("AppUserId", "Cancelled");
