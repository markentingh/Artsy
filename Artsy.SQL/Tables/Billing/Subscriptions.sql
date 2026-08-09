CREATE TABLE IF NOT EXISTS public."Subscriptions"
(
    "Id" SERIAL PRIMARY KEY,
    "Title" VARCHAR(64) NOT NULL,
    "MonthlyProductId" INT NULL REFERENCES public."Products"("Id"),
    "YearlyProductId" INT NULL REFERENCES public."Products"("Id"),
    "SortIndex" INT NOT NULL DEFAULT 0,
    "Featured" BOOLEAN NOT NULL DEFAULT FALSE,
    "Archived" BOOLEAN NOT NULL DEFAULT FALSE,
    "Status" INT NOT NULL DEFAULT 1,
    "DateCreated" TIMESTAMP NOT NULL DEFAULT NOW()
);

ALTER TABLE public."Subscriptions" ADD COLUMN IF NOT EXISTS "Archived" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public."Subscriptions" ADD COLUMN IF NOT EXISTS "FeaturesJson" TEXT NULL;
ALTER TABLE public."Subscriptions" ADD COLUMN IF NOT EXISTS "MonthlyProductId" INT NULL REFERENCES public."Products"("Id");
ALTER TABLE public."Subscriptions" ADD COLUMN IF NOT EXISTS "YearlyProductId" INT NULL REFERENCES public."Products"("Id");
ALTER TABLE public."Subscriptions" ADD COLUMN IF NOT EXISTS "SortIndex" INT NOT NULL DEFAULT 0;
ALTER TABLE public."Subscriptions" ADD COLUMN IF NOT EXISTS "Featured" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public."Subscriptions" ADD COLUMN IF NOT EXISTS "Status" INT NOT NULL DEFAULT 1;

CREATE INDEX IF NOT EXISTS "IX_Subscriptions_Archived_Status" ON public."Subscriptions" ("Archived", "Status");
