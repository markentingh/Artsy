CREATE TABLE IF NOT EXISTS public."ProjectCollections"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Title" VARCHAR(64) NOT NULL,
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
