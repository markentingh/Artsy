import React from 'react';

export function List({ children, className = '', hover = true, inModal = false }) {
  return (
    <div className={`space-y-2 ${className}`}>
      {React.Children.map(children, (child) => {
        if (React.isValidElement(child)) {
          return React.cloneElement(child, { hover, inModal });
        }
        return child;
      })}
    </div>
  );
}

export function Item({ children, hover = true, inModal = false, className = '' }) {
  const hoverClass = hover
    ? inModal
      ? 'hover:bg-gray-200 dark:hover:bg-gray-700/70'
      : 'hover:bg-gray-100 dark:hover:bg-gray-700'
    : '';
  const bgClass = inModal
    ? 'bg-gray-100 dark:bg-gray-700/50'
    : 'bg-gray-50 dark:bg-gray-800';
  return (
    <div
      className={`flex items-center rounded-lg ${bgClass} p-3 transition ${hoverClass} ${className}`}
    >
      {children}
    </div>
  );
}

export default List;
