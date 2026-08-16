CREATE TABLE IF NOT EXISTS public."OrderItemArtworks"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderId" UUID NOT NULL REFERENCES public."Orders"("Id") ON DELETE CASCADE,
    "OrderItemId" UUID NOT NULL REFERENCES public."OrderItems"("Id") ON DELETE CASCADE,
    "ProjectId" UUID NOT NULL,
    "CollectionId" UUID NOT NULL,
    "ItemId" UUID NOT NULL,
    "Active" BOOLEAN NOT NULL DEFAULT TRUE,
    "Width" INT NOT NULL DEFAULT 0,
    "Height" INT NOT NULL DEFAULT 0,
    "ImageModel" VARCHAR(64) NOT NULL DEFAULT '',
    "Prompt" TEXT NOT NULL DEFAULT '',
    "Accepted" BOOLEAN NOT NULL DEFAULT FALSE,
    "ResponseId" VARCHAR(128) NOT NULL DEFAULT '',
    "FullSize" BOOLEAN NOT NULL DEFAULT FALSE,
    "Index" INT NOT NULL DEFAULT 0,
    "PrintifyImageId" VARCHAR(64) NOT NULL DEFAULT '',
    "Opacity" BOOLEAN NOT NULL DEFAULT FALSE,
    "RequestText" TEXT NOT NULL DEFAULT '',
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "Updated" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_OrderItemArtworks_OrderId" ON public."OrderItemArtworks" ("OrderId");
CREATE INDEX IF NOT EXISTS "IX_OrderItemArtworks_OrderItemId" ON public."OrderItemArtworks" ("OrderItemId");
CREATE INDEX IF NOT EXISTS "IX_OrderItemArtworks_CollectionId" ON public."OrderItemArtworks" ("CollectionId");
