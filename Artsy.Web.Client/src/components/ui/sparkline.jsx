import React, { useMemo } from 'react';

export default function Sparkline({ data, width = 120, height = 40, color = '#3B82F6' }) {
  const path = useMemo(() => {
    if (!data || data.length < 2) return '';
    const max = Math.max(...data);
    const min = Math.min(...data);
    const range = max - min || 1;
    const stepX = width / (data.length - 1);
    return data
      .map((value, i) => {
        const x = i * stepX;
        const y = height - ((value - min) / range) * height;
        return `${i === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
      })
      .join(' ');
  }, [data, width, height]);

  const areaPath = useMemo(() => {
    if (!path) return '';
    return `${path} L ${width} ${height} L 0 ${height} Z`;
  }, [path, width, height]);

  if (!data || data.length < 2) {
    return (
      <div
        style={{ width, height }}
        className="flex items-center justify-center text-xs text-gray-400"
      >
        No data
      </div>
    );
  }

  return (
    <svg width={width} height={height} className="overflow-visible">
      <defs>
        <linearGradient id={`sparkline-gradient-${color.replace('#', '')}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.2} />
          <stop offset="100%" stopColor={color} stopOpacity={0} />
        </linearGradient>
      </defs>
      <path d={areaPath} fill={`url(#sparkline-gradient-${color.replace('#', '')})`} />
      <path
        d={path}
        fill="none"
        stroke={color}
        strokeWidth={1.5}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
