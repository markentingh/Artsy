import React from 'react';
import Icon from '@/components/ui/icon';

export default function ButtonIcon({ name, onClick, title, color, className = '' }) {
  const colorClasses = color === 'red'
    ? 'text-red-500 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20'
    : color === 'green'
    ? 'text-green-600 dark:text-green-500 hover:bg-green-50 dark:hover:bg-green-900/20'
    : color === 'gray'
    ? 'text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700'
    : 'text-primary-600 dark:text-primary-400 hover:bg-gray-100 dark:hover:bg-gray-700';

  return (
    <button
      onClick={onClick}
      title={title}
      className={`icon w-8 h-8 flex items-center justify-center ${colorClasses} rounded transition ${className}`}
    >
      <Icon name={name} />
    </button>
  );
}
