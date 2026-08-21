import React from 'react';

/**
 * AspectRatioIcons — SVG icons for each aspect ratio.
 * Each icon is a white 2px outline with rounded corners, showing the shape of the ratio.
 * The icons are drawn within a 24x24 viewBox, centered.
 */

// Helper to compute the rectangle for a given aspect ratio within a 24x24 viewBox
// with 2px padding on all sides
function getRect(ratio) {
  const maxW = 20; // 24 - 2*2 padding
  const maxH = 20;
  let w, h;
  if (ratio >= 1) {
    w = maxW;
    h = Math.round(maxW / ratio);
  } else {
    h = maxH;
    w = Math.round(maxH * ratio);
  }
  const x = (24 - w) / 2;
  const y = (24 - h) / 2;
  return { x, y, w, h };
}

const stroke = '#ffffff';
const strokeWidth = 2;
const rx = 2;

export const Ratio9x21 = () => {
  const r = getRect(9 / 21);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio9x16 = () => {
  const r = getRect(9 / 16);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio2x3 = () => {
  const r = getRect(2 / 3);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio3x4 = () => {
  const r = getRect(3 / 4);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio4x5 = () => {
  const r = getRect(4 / 5);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio1x1 = () => {
  const r = getRect(1);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio5x4 = () => {
  const r = getRect(5 / 4);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio4x3 = () => {
  const r = getRect(4 / 3);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio3x2 = () => {
  const r = getRect(3 / 2);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio16x9 = () => {
  const r = getRect(16 / 9);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const Ratio21x9 = () => {
  const r = getRect(21 / 9);
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x={r.x} y={r.y} width={r.w} height={r.h} rx={rx} stroke={stroke} strokeWidth={strokeWidth} fill="none" />
    </svg>
  );
};

export const aspectRatioOptions = [
  { value: '9:21', label: '9:21', icon: <Ratio9x21 /> },
  { value: '9:16', label: '9:16', icon: <Ratio9x16 /> },
  { value: '2:3', label: '2:3', icon: <Ratio2x3 /> },
  { value: '3:4', label: '3:4', icon: <Ratio3x4 /> },
  { value: '4:5', label: '4:5', icon: <Ratio4x5 /> },
  { value: '1:1', label: '1:1', icon: <Ratio1x1 /> },
  { value: '5:4', label: '5:4', icon: <Ratio5x4 /> },
  { value: '4:3', label: '4:3', icon: <Ratio4x3 /> },
  { value: '3:2', label: '3:2', icon: <Ratio3x2 /> },
  { value: '16:9', label: '16:9', icon: <Ratio16x9 /> },
  { value: '21:9', label: '21:9', icon: <Ratio21x9 /> },
];
