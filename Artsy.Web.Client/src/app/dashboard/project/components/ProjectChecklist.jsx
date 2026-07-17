import React from 'react';
import Checked from '@/components/ui/checked';

export default function ProjectChecklist({ checklist }) {
  const items = [
    {
      label: 'Set up Artwork for Image Generation',
      key: 'imageGenerationSetup',
      completed: checklist?.imageGenerationSetupCompleted ?? 0,
      total: checklist?.imageGenerationSetupTotal ?? 0,
    },
    {
      label: 'Add one or more Questions for your Artwork',
      key: 'itemQuestionsAdded',
      completed: checklist?.itemQuestionsAddedCompleted ?? 0,
      total: checklist?.itemQuestionsAddedTotal ?? 0,
    },
    {
      label: 'Add one or more Product Blueprints to your Artworks',
      key: 'productBlueprintsAdded',
      completed: checklist?.productBlueprintsAddedCompleted ?? 0,
      total: checklist?.productBlueprintsAddedTotal ?? 0,
    },
    {
      label: 'Add one or more Questions',
      key: 'questionsAdded',
      completed: checklist?.questionsAddedCompleted ?? 0,
      total: checklist?.questionsAddedTotal ?? 0,
    },
  ];

  const allChecked = checklist && items.every((item) => checklist?.[item.key]);
  if (allChecked) return null;

  return (
    <div className="mb-8">
      <h2 className="text-xl font-semibold mb-4">Project Setup Checklist</h2>
      <div className="space-y-3">
        {items.map((item) => (
          <div
            key={item.key}
            className="flex items-center justify-between gap-4 rounded-lg bg-white dark:bg-gray-800 p-4 shadow"
          >
            <div className="flex items-center gap-4">
              <Checked checked={checklist?.[item.key]} />
              <span className="text-gray-700 dark:text-gray-200">{item.label}</span>
            </div>
            <span className="bg-gray-50 dark:bg-gray-900 text-gray-600 dark:text-gray-300 text-sm font-medium rounded px-2 py-1">
              {item.completed}/{item.total}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
