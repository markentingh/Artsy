import React, { useState } from 'react';
import Icon from '@/components/ui/icon';

export function Accordion({ items, inModal = false, className = '' }) {
  const bgClass = inModal
    ? 'bg-gray-100 dark:bg-gray-700/50'
    : 'bg-gray-50 dark:bg-gray-800';
  const hoverClass = inModal
    ? 'hover:bg-gray-200 dark:hover:bg-gray-700/70'
    : 'hover:bg-gray-100 dark:hover:bg-gray-700';

  return (
    <div className={className}>
      <div>
        <div className="mt-3 space-y-2">
          {items.map((item, i) => (
            <AccordionItem
              key={i}
              title={item.title}
              content={item.content}
              action={item.action}
              bgClass={bgClass}
              hoverClass={hoverClass}
            />
          ))}
        </div>
      </div>
    </div>
  );
}

function AccordionItem({ title, content, action, bgClass, hoverClass }) {
  const [expanded, setExpanded] = useState(false);
  const hasContent = content && (Array.isArray(content) ? content.length > 0 : true);

  return (
    <div>
      <div className={`rounded-lg ${bgClass} transition ${hoverClass}`}>
        <div
          className="flex items-center justify-between p-3 text-sm cursor-pointer"
          onClick={() => hasContent && setExpanded(!expanded)}
        >
          <div className="flex items-center gap-2 flex-1">
            {title}
          </div>
          <div className="flex items-center gap-2 ml-2">
            {action && (
              <div onClick={(e) => e.stopPropagation()}>
                {action}
              </div>
            )}
            {hasContent && (
              <div className="rounded-full p-1">
                <Icon
                  name="expand_more"
                  className={`text-lg leading-none text-gray-500 dark:text-gray-400 transition-transform duration-200 ${expanded ? 'rotate-180' : ''}`}
                  style={{ display: 'block', transform: expanded ? 'rotate(180deg) translateY(2px)' : 'translateY(-1px)' }}
                />
              </div>
            )}
          </div>
        </div>
      </div>
      {hasContent && (
        <div
          className="overflow-hidden transition-all duration-300 ease-in-out"
          style={{ maxHeight: expanded ? 'none' : '0px', opacity: expanded ? 1 : 0 }}
        >
          <div className="mt-2 ml-[2em]">
            {content}
          </div>
        </div>
      )}
    </div>
  );
}

export default Accordion;
