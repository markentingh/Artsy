import React, { useCallback } from 'react';
import { useCollection } from '@/context/collection';
import ButtonOutline from '@/components/ui/button-outline';

export default function NextStep() {
  const {
    project, blueprints,
    handleSaveDraft, setMessage,
  } = useCollection();

  const handlePublishProducts = useCallback(() => {
    setMessage({ type: 'info', text: 'Publishing will be implemented at a later time.' });
  }, [setMessage]);

  const platforms = [];
  if (project?.publishToPrintify) platforms.push('Printify');

  return (
    <div>
      <p className="text-center text-lg mb-4">
        The following products will be published via {platforms.join(', ')}.
      </p>
      <div className="space-y-2 mb-6 max-h-[30vh] overflow-y-auto">
        {blueprints.map((bp) => (
          <div key={bp.id} className="bg-gray-100 dark:bg-gray-700 rounded px-4 py-2">
            {bp.name}
          </div>
        ))}
      </div>
      <div className="buttons flex justify-end gap-2">
        <ButtonOutline className="cancel" onClick={handleSaveDraft}>Save Draft</ButtonOutline>
        <ButtonOutline onClick={handlePublishProducts}>Publish Products</ButtonOutline>
      </div>
    </div>
  );
}
