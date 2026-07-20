CREATE TABLE IF NOT EXISTS public."Trends"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Keyword" VARCHAR(256) NOT NULL,
    "Sector" VARCHAR(128) NOT NULL,
    "EtsyListingCount" INT NOT NULL DEFAULT 0,
    "Data" TEXT NULL,
    "DateCreated" TIMESTAMP NOT NULL DEFAULT NOW()
);
