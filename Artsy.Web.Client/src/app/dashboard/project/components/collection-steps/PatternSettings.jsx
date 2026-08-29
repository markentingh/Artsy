import React from 'react';

export default function PatternSettings({ patternSettings, setPatternSettings }) {
  const { spacingX, spacingY, angle, offset, scale } = patternSettings;

  const handleChange = (key, value) => {
    const num = parseFloat(value);
    if (isNaN(num)) return;
    setPatternSettings(prev => ({ ...prev, [key]: num }));
  };

  return (
    <div className="mb-4 border border-gray-300 dark:border-gray-600 rounded-lg p-4">
      <h4 className="text-sm font-medium text-gray-600 dark:text-gray-300 mb-3">Pattern Settings</h4>
      <div className="flex gap-3">
        <div>
          <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">Spacing X</label>
          <input
            type="number"
            step="0.1"
            min="0.1"
            value={spacingX}
            onChange={(e) => handleChange('spacingX', e.target.value)}
            className="px-2 py-1 text-sm rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            style={{ width: '5em' }}
            placeholder="1"
          />
        </div>
        <div>
          <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">Spacing Y</label>
          <input
            type="number"
            step="0.1"
            min="0.1"
            value={spacingY}
            onChange={(e) => handleChange('spacingY', e.target.value)}
            className="px-2 py-1 text-sm rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            style={{ width: '5em' }}
            placeholder="1"
          />
        </div>
        <div>
          <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">Angle</label>
          <input
            type="number"
            step="1"
            min="-45"
            max="45"
            value={angle}
            onChange={(e) => handleChange('angle', e.target.value)}
            className="px-2 py-1 text-sm rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            style={{ width: '5em' }}
            placeholder="0"
          />
        </div>
        <div>
          <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">Offset</label>
          <input
            type="number"
            step="0.1"
            min="-1"
            max="1"
            value={offset}
            onChange={(e) => handleChange('offset', e.target.value)}
            className="px-2 py-1 text-sm rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            style={{ width: '5em' }}
            placeholder="0"
          />
        </div>
        <div>
          <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">Scale</label>
          <input
            type="number"
            step="0.01"
            min="0.001"
            max="1"
            value={scale}
            onChange={(e) => handleChange('scale', e.target.value)}
            className="px-2 py-1 text-sm rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            style={{ width: '5em' }}
            placeholder="0.5"
          />
        </div>
      </div>
    </div>
  );
}
