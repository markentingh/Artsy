CREATE TABLE IF NOT EXISTS public."OrderItemArtworkPlacements"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderItemArtworkId" UUID NOT NULL REFERENCES public."OrderItemArtworks"("Id") ON DELETE CASCADE,
    "Width" INT NOT NULL DEFAULT 0,
    "Height" INT NOT NULL DEFAULT 0,
    "Index" INT NOT NULL DEFAULT 0,
    "ResponseId" VARCHAR(64) NOT NULL DEFAULT '',
    "GroupId" UUID,
    "Position" VARCHAR(64) NOT NULL DEFAULT ''
);

-- Drop old unique index that conflicts with multi-group placements
DROP INDEX IF EXISTS "UX_OrderItemArtworkPlacements_ArtworkId_Index";

-- Add GroupId and Position columns for seamless placement group support
ALTER TABLE public."OrderItemArtworkPlacements" ADD COLUMN IF NOT EXISTS "GroupId" UUID;
ALTER TABLE public."OrderItemArtworkPlacements" ADD COLUMN IF NOT EXISTS "Position" VARCHAR(64) NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS "IX_OrderItemArtworkPlacements_ArtworkId"
    ON public."OrderItemArtworkPlacements" ("OrderItemArtworkId");

CREATE INDEX IF NOT EXISTS "IX_OrderItemArtworkPlacements_GroupId"
    ON public."OrderItemArtworkPlacements" ("GroupId");
