import React from 'react';
import { Link } from 'react-router-dom';

export default function ButtonOutline({ to, children, onClick, disabled, color, size, type = 'button', className = '' }) {
  const colorClasses = color === 'green'
    ? 'border-green-600 text-green-600 dark:text-green-500 hover:bg-green-600 hover:text-white dark:hover:bg-green-700 dark:hover:text-white'
    : color === 'red'
    ? 'border-red-600 text-red-600 dark:text-red-500 hover:bg-red-600 hover:text-white dark:hover:bg-red-700 dark:hover:text-white'
    : color === 'blue'
    ? 'border-blue-600 text-blue-600 dark:text-blue-500 hover:bg-blue-600 hover:text-white dark:hover:bg-blue-700 dark:hover:text-white'
    : color === 'gray'
    ? 'border-gray-500 text-gray-500 dark:text-gray-400 hover:bg-gray-500 hover:text-white dark:hover:bg-gray-600 dark:hover:text-white'
    : 'border-primary-600 text-primary-600 dark:text-[#75a0ff] hover:bg-primary-600 hover:text-white dark:hover:bg-primary-700 dark:hover:text-white';
  const sizeClasses = size === 'small'
    ? 'py-1 px-2 text-xs gap-1'
    : 'py-2 px-4';
  const classes = `inline-flex items-center justify-center text-center border rounded transition ${colorClasses} ${sizeClasses} ${className}`;

  if (to) {
    return (
      <Link to={to} className={classes}>
        {children}
      </Link>
    );
  }

  return (
    <button type={type} onClick={onClick} disabled={disabled} className={classes}>
      {children}
    </button>
  );
}
