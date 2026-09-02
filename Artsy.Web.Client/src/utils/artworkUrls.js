/**
 * Builds artwork image URLs from data, without requiring the api object.
 * Base pattern: /api/projects/collection/{collectionId}/item/{itemId}/artwork/{artworkId}
 */

const BASE = '/api/projects';

function buildUrl(collectionId, itemId, artworkId, params) {
  const path = `${BASE}/collection/${collectionId}/item/${itemId}/artwork/${artworkId}`;
  const valid = (params || []).filter(Boolean);
  return valid.length > 0 ? `${path}?${valid.join('&')}` : path;
}

export function artworkImageUrl(collectionId, itemId, artworkId, { thumb = false, fullSize = false, jpgWithBg = false, placementIndex = null, cacheBust = null } = {}) {
  const params = [];
  if (thumb) params.push('thumb=true');
  if (fullSize) params.push('fullSize=true');
  if (jpgWithBg) params.push('jpgWithBg=true');
  if (placementIndex != null) params.push(`placementIndex=${placementIndex}`);
  if (cacheBust != null) params.push(`r=${cacheBust}`);
  return buildUrl(collectionId, itemId, artworkId, params);
}

export function artworkGroupImageUrl(collectionId, itemId, artworkId, groupId, position, { thumb = false, fullSize = false, png = false, cacheBust = null } = {}) {
  const path = `${BASE}/collection/${collectionId}/item/${itemId}/artwork/${artworkId}/group/${groupId}/${position}`;
  const params = [];
  if (thumb) params.push('thumb=true');
  if (fullSize) params.push('fullSize=true');
  if (png) params.push('png=true');
  if (cacheBust != null) params.push(`r=${cacheBust}`);
  return params.length > 0 ? `${path}?${params.join('&')}` : path;
}

export function artworkThumbUrl(collectionId, itemId, artworkId, { placementIndex = null, cacheBust = null } = {}) {
  return artworkImageUrl(collectionId, itemId, artworkId, { thumb: true, placementIndex, cacheBust });
}

export function artworkJpgWithBgUrl(collectionId, itemId, artworkId, { thumb = false, fullSize = false, placementIndex = null, cacheBust = null } = {}) {
  return artworkImageUrl(collectionId, itemId, artworkId, { jpgWithBg: true, thumb, fullSize, placementIndex, cacheBust });
}

export function artworkJpgWithBgThumbUrl(collectionId, itemId, artworkId, { placementIndex = null, cacheBust = null } = {}) {
  return artworkJpgWithBgUrl(collectionId, itemId, artworkId, { thumb: true, placementIndex, cacheBust });
}
