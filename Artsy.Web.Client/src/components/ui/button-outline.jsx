import React from 'react';
import { Link } from 'react-router-dom';

export default function ButtonOutline({ to, children, onClick, className = '' }) {
  const classes = `inline-block py-2 px-4 text-center border border-primary-600 text-primary-600 dark:text-[#75a0ff] rounded hover:bg-primary-600 hover:text-white dark:hover:bg-primary-700 dark:hover:text-white transition ${className}`;

  if (to) {
    return (
      <Link to={to} className={classes}>
        {children}
      </Link>
    );
  }

  return (
    <button type="button" onClick={onClick} className={classes}>
      {children}
    </button>
  );
}
