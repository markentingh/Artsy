import React from 'react';
import Checked from '@/components/ui/checked';
import { List, Item } from '@/components/ui/list';

export default function ProjectChecklist({ checklist, project }) {
  const items = [
    {
      label: 'Set up Artwork Blueprints to be Generated',
      key: 'imageGenerationSetup',
      completed: checklist?.imageGenerationSetupCompleted ?? 0,
      total: checklist?.imageGenerationSetupTotal ?? 0,
    },
    {
      label: 'Configure one or more Product Blueprints',
      key: 'productBlueprintsAdded',
      completed: (checklist?.productBlueprintsAddedCompleted ?? 0) > 0 ? 1 : 0,
      total: 1,
      isChecked: (checklist?.productBlueprintsAddedCompleted ?? 0) > 0,
    },
    {
      label: 'Select Printify Shop for Publishing Products to',
      key: 'printifyShopSelected',
      completed: project?.printifyStoreId ? 1 : 0,
      total: 1,
      isChecked: !!project?.printifyStoreId,
    },
    {
      label: 'Select Social Media Account for Posting to',
      key: 'socialMediaAccountSelected',
      completed: project?.instagramId ? 1 : 0,
      total: 1,
      isChecked: !!project?.instagramId,
    },
    {
      label: 'Configure Social Media Post Description',
      key: 'socialMediaDescriptionConfigured',
      completed: (project?.socialMediaDescription || project?.socialMediaPrompt) ? 1 : 0,
      total: 1,
      isChecked: !!(project?.socialMediaDescription || project?.socialMediaPrompt),
    },
  ];

  const allChecked = checklist && items.every((item) => item.isChecked ?? checklist?.[item.key]);
  if (allChecked) return null;

  return (
    <div className="mb-8">
      <h2 className="text-xl font-semibold mb-4">Project Setup Checklist</h2>
      <List>
        {items.map((item) => (
          <Item key={item.key} className="justify-between gap-4">
            <div className="flex items-center gap-4">
              <Checked checked={item.isChecked ?? checklist?.[item.key]} />
              <span className="text-gray-700 dark:text-gray-200">{item.label}</span>
            </div>
            <span className="bg-gray-100 dark:bg-gray-900 text-gray-600 dark:text-gray-300 text-sm font-medium rounded px-2 py-1">
              {item.completed}/{item.total}
            </span>
          </Item>
        ))}
      </List>
    </div>
  );
}
