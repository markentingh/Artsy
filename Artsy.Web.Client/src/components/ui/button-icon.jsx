import React from 'react';
import Icon from '@/components/ui/icon';

export default function ButtonIcon({ name, onClick, title, className = '' }) {
  return (
    <button
      onClick={onClick}
      title={title}
      className={`icon w-8 h-8 flex items-center justify-center text-primary-600 dark:text-primary-400 hover:bg-gray-100 dark:hover:bg-gray-700 rounded transition ${className}`}
    >
      <Icon name={name} />
    </button>
  );
}
