CREATE TABLE IF NOT EXISTS public."Projects" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "AppUserId" UUID NOT NULL,
    "Title" VARCHAR(64) NOT NULL,
    "Description" VARCHAR(255) NULL,
    "Key" VARCHAR(16) NOT NULL UNIQUE,
    "Color" VARCHAR(16) NOT NULL,
    "Status" INTEGER NOT NULL DEFAULT 1,
    "PublishToPrintify" BOOLEAN NOT NULL DEFAULT TRUE,
    "PostToInstagram" BOOLEAN NOT NULL DEFAULT TRUE,
    "PrintifyStoreId" INTEGER NULL,
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Ensure column exists for databases created before this field was added
ALTER TABLE public."Projects" ADD COLUMN IF NOT EXISTS "PrintifyStoreId" INTEGER NULL;
ALTER TABLE public."Projects" ADD COLUMN IF NOT EXISTS "InstagramId" UUID NULL;
ALTER TABLE public."Projects" ADD COLUMN IF NOT EXISTS "PostToInstagram" BOOLEAN NOT NULL DEFAULT TRUE;
