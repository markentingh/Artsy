import React from 'react';

export default function ProgressBar({ progress, message, className = '' }) {
  const clamped = Math.min(100, Math.max(0, progress));

  return (
    <div className={`w-full ${className}`}>
      <div
        className="w-full h-2.5 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden"
        role="progressbar"
        aria-valuenow={clamped}
        aria-valuemin={0}
        aria-valuemax={100}
      >
        <div
          className="h-full bg-primary-600 dark:bg-primary-500 transition-all duration-300 ease-in-out"
          style={{ width: `${clamped}%` }}
        />
      </div>
      {message && (
        <p className="text-center text-xs text-gray-600 dark:text-gray-400 mt-2">{message}</p>
      )}
    </div>
  );
}
