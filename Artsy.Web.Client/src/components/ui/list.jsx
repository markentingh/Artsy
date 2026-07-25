import React from 'react';

export function List({ children, className = '' }) {
  return (
    <div className={`space-y-2 ${className}`}>
      {children}
    </div>
  );
}

export function Item({ children, hover = true, className = '' }) {
  const hoverClass = hover
    ? 'hover:bg-gray-100 dark:hover:bg-gray-700'
    : '';
  return (
    <div
      className={`flex items-center rounded-lg bg-gray-50 dark:bg-gray-800 p-3 transition ${hoverClass} ${className}`}
    >
      {children}
    </div>
  );
}

export default List;
