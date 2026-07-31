CREATE TABLE IF NOT EXISTS public."AppUserInstagramAccounts"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "AppUserId" UUID NOT NULL REFERENCES public."AppUsers"("Id") ON DELETE CASCADE,
    "InstagramBusinessAccountId" VARCHAR(255) NOT NULL,
    "MetaUserId" VARCHAR(255) NOT NULL DEFAULT '',
    "MetaAccessToken" TEXT NOT NULL DEFAULT '',
    "MetaTokenExpiresAtUtc" TIMESTAMP NULL,
    "Username" VARCHAR(255) NULL,
    "ProfilePictureUrl" TEXT NULL,
    "DateCreated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "DateUpdated" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_AppUserInstagramAccounts_UserId_IgId"
    ON public."AppUserInstagramAccounts" ("AppUserId", "InstagramBusinessAccountId");
