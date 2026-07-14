CREATE TABLE IF NOT EXISTS public."AppUsers" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "FullName" VARCHAR(255) NOT NULL,
    "Email" VARCHAR(255) NOT NULL UNIQUE,
    "EmailConfirmed" BOOLEAN NOT NULL DEFAULT FALSE,
    "PasswordHash" VARCHAR(255) NOT NULL,
    "LockoutEndDate" TIMESTAMP NULL,
    "LockoutEnabled" BOOLEAN NOT NULL DEFAULT FALSE,
    "AccessFailedCount" INTEGER NOT NULL DEFAULT 0,
    "AccessFailedTime" TIMESTAMP NULL,
    "PasswordResetHash" VARCHAR(255) NULL,
    "PasswordResetTime" TIMESTAMP NULL,
    "NewEmail" VARCHAR(255) NULL,
    "Status" INTEGER NOT NULL DEFAULT 1,
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Ensure connection columns exist for databases created before these fields were added
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "PrintifyAccessToken" TEXT NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "PrintifyRefreshToken" TEXT NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "PrintifyTokensExpireAtUtc" TIMESTAMP NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "PrintifyShopId" VARCHAR(255) NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "MetaAccessToken" TEXT NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "MetaTokenExpiresAtUtc" TIMESTAMP NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "MetaUserId" VARCHAR(255) NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "InstagramBusinessAccountId" VARCHAR(255) NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "TelegramUserId" VARCHAR(255) NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "TelegramChatId" VARCHAR(255) NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "TelegramConnectionToken" VARCHAR(255) NULL;
ALTER TABLE public."AppUsers" ADD COLUMN IF NOT EXISTS "OAuthState" VARCHAR(255) NULL;
