CREATE TABLE IF NOT EXISTS public."ProjectCollectionInstagramPostImages"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "InstagramPostId" UUID NOT NULL REFERENCES public."ProjectCollectionInstagramPosts"("Id"),
    "ProductImageId" UUID NULL REFERENCES public."ProjectCollectionProductImages"("Id"),
    "ArtworkId" UUID NULL REFERENCES public."ProjectCollectionArtwork"("Id"),
    "SortOrder" INT NOT NULL DEFAULT 0,
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
