CREATE TABLE IF NOT EXISTS public."PrintifyBlueprintPrintProviders"
(
    "BlueprintId" INT NOT NULL,
    "PrintProviderId" INT NOT NULL,
    "Title" VARCHAR(256) NOT NULL DEFAULT '',
    "DecorationMethods" TEXT NOT NULL DEFAULT '[]',
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY ("BlueprintId", "PrintProviderId")
);
