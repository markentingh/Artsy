import React, { useMemo } from 'react';

export default function PatternPreview({ patternSettings, previewImage }) {
  const { spacingX, spacingY, angle, offset, scale } = patternSettings;

  const tiles = useMemo(() => {
    if (!previewImage) return [];

    const baseWidth = 500;
    const imgW = baseWidth * (scale || 0.5);
    const stepX = imgW * (spacingX || 1);
    const stepY = imgW * (spacingY || 1);

    const containerSize = 5000;
    const rows = Math.ceil(containerSize / stepY) + 2;

    // Calculate max offset shift to add extra columns on both sides
    const maxOffsetShift = Math.abs(offset || 0) * stepX * rows;
    const extraCols = Math.ceil(maxOffsetShift / stepX) + 2;
    const cols = Math.ceil(containerSize / stepX) + 2 + extraCols * 2;

    // Center the grid in the 5000px container, accounting for extra columns
    const startX = (containerSize - cols * stepX) / 2;
    const startY = (containerSize - rows * stepY) / 2;

    const result = [];
    for (let row = 0; row < rows; row++) {
      const rowOffset = offset !== 0 ? (offset * stepX * row) : 0;
      for (let col = 0; col < cols; col++) {
        const x = startX + col * stepX + rowOffset;
        const y = startY + row * stepY;
        result.push({ x, y, key: `${row}-${col}` });
      }
    }
    return { result, imgW };
  }, [previewImage, spacingX, spacingY, offset, scale]);

  if (!previewImage) return null;

  return (
    <div className="w-full max-w-[500px] mx-auto rounded-lg overflow-hidden border border-gray-300 dark:border-gray-600 mb-4">
      <div className="w-full h-[400px] overflow-hidden bg-gray-100 dark:bg-gray-700 relative">
        <div
          style={{
            width: '5000px',
            height: '5000px',
            position: 'absolute',
            top: '50%',
            left: '50%',
            marginLeft: '-2500px',
            marginTop: '-2500px',
            transform: `rotate(${angle || 0}deg)`,
            transformOrigin: 'center center',
          }}
        >
          {tiles.result && tiles.result.map((tile) => (
            <img
              key={tile.key}
              src={previewImage}
              alt=""
              style={{
                position: 'absolute',
                left: `${tile.x}px`,
                top: `${tile.y}px`,
                width: `${tiles.imgW}px`,
                height: 'auto',
                pointerEvents: 'none',
                userSelect: 'none',
              }}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
