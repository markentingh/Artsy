CREATE TABLE IF NOT EXISTS public."ProjectCollectionInstagramPosts"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "InstagramAccountId" UUID NOT NULL REFERENCES public."AppUserInstagramAccounts"("Id"),
    "Description" TEXT NOT NULL DEFAULT '',
    "ContainerId" VARCHAR(64) NOT NULL DEFAULT '',
    "Permalink" VARCHAR(512) NULL,
    "Status" INT NOT NULL DEFAULT 1,
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_ProjectCollectionInstagramPosts_CollectionId_Status" ON public."ProjectCollectionInstagramPosts" ("CollectionId", "Status");
