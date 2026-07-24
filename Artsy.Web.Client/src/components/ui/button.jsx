import React from 'react';

export default function Button({ children, onClick, type = 'button', disabled, color, className = '' }) {
  const colorClasses = color === 'red'
    ? 'bg-red-600 text-white hover:bg-red-700 dark:bg-red-600 dark:hover:bg-red-700'
    : color === 'green'
    ? 'bg-green-600 text-white hover:bg-green-700 dark:bg-green-600 dark:hover:bg-green-700'
    : color === 'gray'
    ? 'bg-gray-500 text-white hover:bg-gray-600 dark:bg-gray-600 dark:hover:bg-gray-500'
    : 'bg-primary-600 text-white hover:bg-primary-700 dark:bg-primary-600 dark:hover:bg-primary-700';

  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      className={`inline-flex items-center justify-center py-2 px-4 text-center rounded transition disabled:opacity-50 disabled:cursor-not-allowed ${colorClasses} ${className}`}
    >
      {children}
    </button>
  );
}
