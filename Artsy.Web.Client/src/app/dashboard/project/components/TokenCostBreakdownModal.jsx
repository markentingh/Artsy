import React, { useMemo } from 'react';
import Modal from '@/components/ui/modal';
import Accordion from '@/components/ui/accordion';
import { List, Item } from '@/components/ui/list';

export default function TokenCostBreakdownModal({ show, onClose, generations }) {
  const accordionItems = useMemo(() => {
    if (!generations || generations.length === 0) return [];

    return generations.map((gen, i) => {
      // Group placements by blueprintId
      const byBlueprint = new Map();
      for (const p of (gen.placements || [])) {
        if (!byBlueprint.has(p.blueprintId)) {
          byBlueprint.set(p.blueprintId, { blueprintId: p.blueprintId, blueprintName: p.blueprintName, placements: [] });
        }
        byBlueprint.get(p.blueprintId).placements.push(p);
      }

      // Check if this is a seamless group (multiple placements from same blueprint with different aspect ratios)
      const isSeamlessGroup = (() => {
        for (const [, group] of byBlueprint) {
          if (group.placements.length > 1) {
            const ratios = group.placements.map(p => p.width / p.height);
            const allSame = ratios.every(r => Math.abs(r - ratios[0]) < 0.001);
            if (!allSame) return true;
          }
        }
        return false;
      })();

      const content = (
        <div className="space-y-4">
          {/* First row: dimensions, tokens, optional seamless tag */}
          <div className="flex items-center justify-between text-sm">
            <div className="flex items-center gap-4">
              <span className="text-gray-500 dark:text-gray-400">
                Dimensions: <span className="font-medium text-gray-700 dark:text-gray-200">{gen.width} x {gen.height}</span>
              </span>
              <span className="text-gray-500 dark:text-gray-400">
                Tokens: <span className="font-medium text-gray-700 dark:text-gray-200">{gen.tokens}</span>
              </span>
            </div>
            {isSeamlessGroup && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300">
                Seamless Placements Group
              </span>
            )}
          </div>

          {/* Per-blueprint sections */}
          {Array.from(byBlueprint.values()).map((bp) => (
            <div key={bp.blueprintId}>
              <h5 className="text-sm font-medium text-gray-600 dark:text-gray-300 mb-2">
                {bp.blueprintName || `Blueprint ${bp.blueprintId}`}
              </h5>
              <List inModal>
                {bp.placements.map((p, idx) => (
                  <Item key={idx} className="justify-between">
                    <span className="text-sm text-gray-600 dark:text-gray-300 capitalize">
                      {p.position}
                    </span>
                    <span className="text-sm text-gray-500 dark:text-gray-400">
                      {p.width} x {p.height}
                    </span>
                  </Item>
                ))}
              </List>
            </div>
          ))}
        </div>
      );

      return {
        title: <span className="font-medium">Generation #{i + 1}</span>,
        content,
      };
    });
  }, [generations]);

  return (
    <Modal
      title="Token Cost Breakdown"
      onClose={onClose}
      top
      className="min-w-[36em] max-w-full"
    >
      {generations && generations.length > 0 ? (
        <Accordion items={accordionItems} inModal defaultExpandedIndex={0} />
      ) : (
        <p className="text-sm text-gray-500 dark:text-gray-400 py-4">No generation data available.</p>
      )}
    </Modal>
  );
}
