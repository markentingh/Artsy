CREATE TABLE IF NOT EXISTS public."PrintifyBlueprints"
(
    "BlueprintId" INT PRIMARY KEY,
    "Title" VARCHAR(256) NOT NULL DEFAULT '',
    "Description" TEXT NOT NULL DEFAULT '',
    "Brand" VARCHAR(128) NOT NULL DEFAULT '',
    "Model" VARCHAR(128) NOT NULL DEFAULT '',
    "ImageCount" INT NOT NULL DEFAULT 0,
    "DateCreated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_printify_blueprints_brand ON public."PrintifyBlueprints" ("Brand");
CREATE INDEX IF NOT EXISTS idx_printify_blueprints_title ON public."PrintifyBlueprints" ("Title");
