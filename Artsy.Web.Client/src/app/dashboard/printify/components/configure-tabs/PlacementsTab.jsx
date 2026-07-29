import React, { useMemo } from 'react';
import { usePrintifyBlueprint } from '@/context/printifyBlueprint';

const decorationMethodKeys = [
  'dtg', 'dtf', 'embroidery', 'sublimation',
  'digital_printing', 'digital printing',
  'engraving', 'fiber_laser', 'fiber laser', 'co2_laser', 'co2 laser',
];

const decorationMethodLabels = {
  'dtg': 'Direct to Garment',
  'dtf': 'Direct to Film',
  'embroidery': 'Embroidery',
  'sublimation': 'Sublimation',
  'digital_printing': 'Digital Printing',
  'digital printing': 'Digital Printing',
  'engraving': 'Engraving',
  'fiber_laser': 'Fiber Laser',
  'fiber laser': 'Fiber Laser',
  'co2_laser': 'CO2 Laser',
  'co2 laser': 'CO2 Laser',
};

function formatDecorationMethod(method) {
  if (!method) return '—';
  const key = method.toLowerCase();
  return decorationMethodLabels[key] || method.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
}

function formatPosition(position) {
  let result = position;
  for (const key of decorationMethodKeys) {
    const escaped = key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    result = result.replace(new RegExp(escaped, 'gi'), '');
  }
  return result
    .replace(/_/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

export default function PlacementsTab() {
  const { variants } = usePrintifyBlueprint();

  const allPlaceholders = useMemo(() => {
    const groups = new Map();
    for (const variant of variants) {
      const phs = variant.placeholders || [];
      for (const ph of phs) {
        const cleanPosition = formatPosition(ph.position);
        const groupKey = `${cleanPosition}|${ph.width}|${ph.height}`;
        if (!groups.has(groupKey)) {
          groups.set(groupKey, {
            key: groupKey,
            label: cleanPosition,
            position: ph.position,
            decorationMethods: new Set(),
            height: ph.height,
            width: ph.width,
          });
        }
        if (ph.decoration_method) {
          groups.get(groupKey).decorationMethods.add(ph.decoration_method);
        }
      }
    }
    return Array.from(groups.values()).map((g) => ({
      ...g,
      decorationMethods: Array.from(g.decorationMethods),
    })).sort((a, b) => a.label.localeCompare(b.label, undefined, { numeric: true, sensitivity: 'base' }));
  }, [variants]);

  if (allPlaceholders.length === 0) return null;

  return (
    <div>
      <label className="block text-sm font-medium mb-2">Placements</label>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-gray-200 dark:border-gray-700">
              <th className="text-left py-2 px-3">Position</th>
              <th className="text-left py-2 px-3">Decoration Method</th>
              <th className="text-right py-2 px-3">Width</th>
              <th className="text-right py-2 px-3">Height</th>
            </tr>
          </thead>
          <tbody>
            {allPlaceholders.map((ph) => (
              <tr key={ph.key} className="border-b border-gray-100 dark:border-gray-700">
                <td className="py-2 px-3">{ph.label}</td>
                <td className="py-2 px-3 text-gray-500 dark:text-gray-400">
                  {ph.decorationMethods.length > 0
                    ? ph.decorationMethods.map(formatDecorationMethod).join(', ')
                    : '—'}
                </td>
                <td className="py-2 px-3 text-right text-gray-500 dark:text-gray-400">{ph.width}px</td>
                <td className="py-2 px-3 text-right text-gray-500 dark:text-gray-400">{ph.height}px</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
