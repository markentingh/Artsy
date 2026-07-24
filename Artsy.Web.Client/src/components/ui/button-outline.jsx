import React from 'react';
import { Link } from 'react-router-dom';

export default function ButtonOutline({ to, children, onClick, disabled, color, className = '' }) {
  const colorClasses = color === 'green'
    ? 'border-green-600 text-green-600 dark:text-green-500 hover:bg-green-600 hover:text-white dark:hover:bg-green-700 dark:hover:text-white'
    : color === 'red'
    ? 'border-red-600 text-red-600 dark:text-red-500 hover:bg-red-600 hover:text-white dark:hover:bg-red-700 dark:hover:text-white'
    : 'border-primary-600 text-primary-600 dark:text-[#75a0ff] hover:bg-primary-600 hover:text-white dark:hover:bg-primary-700 dark:hover:text-white';
  const classes = `inline-flex items-center justify-center py-2 px-4 text-center border rounded transition ${colorClasses} ${className}`;

  if (to) {
    return (
      <Link to={to} className={classes}>
        {children}
      </Link>
    );
  }

  return (
    <button type="button" onClick={onClick} disabled={disabled} className={classes}>
      {children}
    </button>
  );
}
