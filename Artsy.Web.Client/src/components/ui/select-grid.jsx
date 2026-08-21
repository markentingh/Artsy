import React, { useState, useRef, useEffect, useCallback } from 'react';
import { createPortal } from 'react-dom';
import Icon from '@/components/ui/icon';

/**
 * SelectGrid — a dropdown that renders options as a grid of icon cells.
 * Based on the select-checklist pattern (portal-based dropdown, fixed positioning).
 *
 * Props:
 *   name        — field name
 *   label       — optional label above the dropdown
 *   options     — array of { value, label, icon } where icon is a React node (e.g. <svg>)
 *   value       — currently selected value (single-select)
 *   onChange     — callback(selectedValue)
 *   columns     — number of columns in the dropdown grid (default 6)
 *   placeholder — text when nothing selected
 *   className   — extra classes on the wrapper
 *   disabled    — disable the dropdown
 */
export default function SelectGrid({
  name,
  label,
  options = [],
  value,
  onChange,
  columns = 6,
  placeholder = 'Select...',
  className = '',
  disabled = false,
  buttonWidth = null,
  dropdownWidth = null,
}) {
  const [open, setOpen] = useState(false);
  const [dropdownStyle, setDropdownStyle] = useState({});
  const buttonRef = useRef(null);
  const dropdownRef = useRef(null);

  const reposition = useCallback(() => {
    if (!buttonRef.current) return;
    const rect = buttonRef.current.getBoundingClientRect();
    const maxH = window.innerHeight - rect.bottom - 8;
    // If dropdownWidth is explicitly set, use it; otherwise use the button width
    // but allow the dropdown to be wider than a narrow button
    const w = dropdownWidth || rect.width;
    setDropdownStyle({
      position: 'fixed',
      top: `${rect.bottom + 4}px`,
      left: `${rect.left}px`,
      width: `${w}px`,
      maxHeight: `${Math.max(80, maxH)}px`,
      zIndex: '9999',
    });
  }, [dropdownWidth]);

  useEffect(() => {
    if (!open) return;

    reposition();

    const handler = (e) => {
      if (dropdownRef.current && dropdownRef.current.contains(e.target)) return;
      if (buttonRef.current && buttonRef.current.contains(e.target)) return;
      setOpen(false);
    };

    const onScroll = () => reposition();

    document.addEventListener('click', handler);
    window.addEventListener('resize', onScroll);
    window.addEventListener('scroll', onScroll, true);

    return () => {
      document.removeEventListener('click', handler);
      window.removeEventListener('resize', onScroll);
      window.removeEventListener('scroll', onScroll, true);
    };
  }, [open, reposition]);

  const handleCellClick = (val) => {
    if (onChange) onChange(val);
    setOpen(false);
  };

  const selectedOption = options.find((o) => o.value === value);

  return (
    <div className={`relative ${className}`}>
      {label && (
        <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          {label}
        </label>
      )}
      <button
        ref={buttonRef}
        type="button"
        name={name}
        disabled={disabled}
        onClick={() => setOpen((prev) => !prev)}
        style={buttonWidth ? { width: `${buttonWidth}px` } : undefined}
        className="px-3 py-2 border rounded bg-white dark:bg-gray-700 text-left text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 border-gray-300 dark:border-gray-600 disabled:opacity-50 flex items-center justify-between"
      >
        <span className={`flex-1 min-w-0 truncate whitespace-nowrap ${!selectedOption ? 'text-gray-400' : ''}`}>
          {selectedOption ? (
            <span className="inline-flex items-center gap-2">
              {selectedOption.icon && (
                <span className="flex items-center justify-center w-6 h-6 flex-shrink-0">
                  {selectedOption.icon}
                </span>
              )}
              <span className="text-sm whitespace-nowrap">{selectedOption.label}</span>
            </span>
          ) : placeholder}
        </span>
        <Icon name="expand_more" className="text-gray-400 text-sm" />
      </button>

      {open && createPortal(
        <div
          ref={dropdownRef}
          style={dropdownStyle}
          className="overflow-y-auto rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 shadow-lg p-2"
        >
          <div style={{ display: 'grid', gridTemplateColumns: `repeat(${columns}, 1fr)`, gap: '4px' }}>
            {options.map((option) => {
              const isSelected = option.value === value;
              return (
                <div
                  key={option.value}
                  className={`flex flex-col items-center justify-center gap-1 p-2 rounded cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 ${isSelected ? 'bg-primary-50 dark:bg-primary-900/30' : ''}`}
                  onClick={() => handleCellClick(option.value)}
                >
                  <div className="flex items-center justify-center w-8 h-8">
                    {option.icon}
                  </div>
                  <span className={`text-xs ${isSelected ? 'text-primary-700 dark:text-primary-300 font-medium' : 'text-gray-600 dark:text-gray-300'}`}>
                    {option.label}
                  </span>
                </div>
              );
            })}
          </div>
        </div>,
        document.body
      )}
    </div>
  );
}
