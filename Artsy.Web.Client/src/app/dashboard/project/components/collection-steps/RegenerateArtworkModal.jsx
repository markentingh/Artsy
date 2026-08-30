import React, { useState, useEffect, useRef, useCallback } from 'react';
import Modal from '@/components/ui/modal';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import Icon from '@/components/ui/icon';

export default function RegenerateArtworkModal({
  show,
  imageUrl,
  placementLabel,
  existingOptionalPrompt = '',
  isGenerating,
  isApplyingEdits,
  onGenerate,
  onApplyEdits,
  onClose,
}) {
  const [optionalPrompt, setOptionalPrompt] = useState('');
  const [rotate180, setRotate180] = useState(false);
  const [flipH, setFlipH] = useState(false);
  const [flipV, setFlipV] = useState(false);
  const [hasEdits, setHasEdits] = useState(false);
  const textareaRef = useRef(null);

  const autoResizeTextarea = useCallback((el) => {
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  }, []);

  // Reset state when modal opens
  useEffect(() => {
    if (show) {
      setOptionalPrompt(existingOptionalPrompt || '');
      setRotate180(false);
      setFlipH(false);
      setFlipV(false);
      setHasEdits(false);
    }
  }, [show, existingOptionalPrompt]);

  // Auto-resize on value change
  useEffect(() => {
    if (textareaRef.current) {
      autoResizeTextarea(textareaRef.current);
    }
  }, [optionalPrompt, autoResizeTextarea]);

  if (!show) return null;

  const handleGenerate = () => {
    onGenerate(optionalPrompt);
  };

  const handleApplyEdits = () => {
    onApplyEdits({ rotate180, flipHorizontal: flipH, flipVertical: flipV });
  };

  // Build CSS transform from edit state
  const imgStyle = {
    maxWidth: '350px',
    width: '100%',
    transform: `${rotate180 ? 'rotate(180deg)' : ''} ${flipH ? 'scaleX(-1)' : ''} ${flipV ? 'scaleY(-1)' : ''}`.trim(),
  };

  const editButtonClass = 'flex items-center justify-center w-10 h-10 rounded border transition';
  const editButtonActive = 'border-primary-500 bg-primary-500/10 text-primary-600 dark:text-primary-400';
  const editButtonInactive = 'border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-400 hover:border-gray-400 dark:hover:border-gray-500';

  return (
    <Modal title="Regenerate Artwork" onClose={onClose}>
      <div className="flex flex-col items-center gap-4">
        {imageUrl && (
          <img
            src={imageUrl}
            alt={placementLabel || 'Placement'}
            className="object-contain rounded border border-gray-300 dark:border-gray-600"
            style={imgStyle}
          />
        )}
        <div className="flex gap-3">
          <button
            type="button"
            title="Rotate 180 degrees"
            className={`${editButtonClass} ${rotate180 ? editButtonActive : editButtonInactive}`}
            onClick={() => { setRotate180(v => !v); setHasEdits(true); }}
          >
            <Icon name="rotate_90_degrees_cw" />
          </button>
          <button
            type="button"
            title="Flip horizontally"
            className={`${editButtonClass} ${flipH ? editButtonActive : editButtonInactive}`}
            onClick={() => { setFlipH(v => !v); setHasEdits(true); }}
          >
            <Icon name="swap_horiz" />
          </button>
          <button
            type="button"
            title="Flip vertically"
            className={`${editButtonClass} ${flipV ? editButtonActive : editButtonInactive}`}
            onClick={() => { setFlipV(v => !v); setHasEdits(true); }}
          >
            <Icon name="swap_vert" />
          </button>
        </div>
        <div className="w-full">
          <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Prompt (optional)
          </label>
          <textarea
            ref={textareaRef}
            name="placementOptionalPrompt"
            value={optionalPrompt}
            onChange={(e) => {
              setOptionalPrompt(e.target.value);
              autoResizeTextarea(e.target);
            }}
            onInput={(e) => autoResizeTextarea(e.target)}
            placeholder="Additional prompt instructions appended to the generated prompt..."
            rows={1}
            className="w-full px-3 py-2 text-sm rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 resize-none overflow-hidden"
            style={{ minHeight: '2.25em' }}
          />
        </div>
      </div>
      <div className="buttons flex justify-end gap-2 mt-6">
        <ButtonOutline color="gray" className="cancel" onClick={onClose}>
          Cancel
        </ButtonOutline>
        {hasEdits && (
          <ButtonOutline onClick={handleApplyEdits} disabled={isApplyingEdits}>
            {isApplyingEdits ? <Spinner className="text-base" /> : 'Apply Edits'}
          </ButtonOutline>
        )}
        <ButtonOutline color="green" onClick={handleGenerate} disabled={isGenerating || isApplyingEdits}>
          {isGenerating ? <Spinner className="text-base" /> : 'Generate Artwork'}
        </ButtonOutline>
      </div>
    </Modal>
  );
}
