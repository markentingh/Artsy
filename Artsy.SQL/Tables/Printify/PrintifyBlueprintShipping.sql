CREATE TABLE IF NOT EXISTS public."PrintifyBlueprintShipping"
(
    "BlueprintId" INT NOT NULL,
    "PrintProviderId" INT NOT NULL,
    "HandlingTimeValue" INT NOT NULL DEFAULT 0,
    "HandlingTimeUnit" VARCHAR(16) NOT NULL DEFAULT 'day',
    "Profiles" TEXT NOT NULL DEFAULT '[]',
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY ("BlueprintId", "PrintProviderId")
);
