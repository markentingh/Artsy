import React from 'react';
import { usePrintifyBlueprint } from '@/context/printifyBlueprint';
import TextArea from '@/components/forms/textarea';

export default function ImagePromptTab() {
  const { imagePrompt, setImagePrompt } = usePrintifyBlueprint();

  return (
    <div>
      <label className="block text-sm font-medium mb-2">Image Prompt</label>
      <TextArea
        name="imagePrompt"
        value={imagePrompt}
        onChange={(e) => setImagePrompt(e.target.value)}
        placeholder="Enter a prompt used for generating artwork images..."
        rows={4}
      />
    </div>
  );
}
