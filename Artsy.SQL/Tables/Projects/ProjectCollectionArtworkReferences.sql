CREATE TABLE IF NOT EXISTS public."ProjectCollectionArtworkReferences"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CollectionId" UUID NOT NULL REFERENCES public."ProjectCollections"("Id"),
    "ProjectId" UUID NOT NULL REFERENCES public."Projects"("Id"),
    "ItemId" UUID NOT NULL REFERENCES public."ProjectItems"("Id"),
    "CustomImageId" UUID NOT NULL REFERENCES public."CustomImages"("Id"),
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_ProjectCollectionArtworkReferences_CollectionId_ItemId" ON public."ProjectCollectionArtworkReferences" ("CollectionId", "ItemId");
CREATE INDEX IF NOT EXISTS "IX_ProjectCollectionArtworkReferences_ProjectId" ON public."ProjectCollectionArtworkReferences" ("ProjectId");
