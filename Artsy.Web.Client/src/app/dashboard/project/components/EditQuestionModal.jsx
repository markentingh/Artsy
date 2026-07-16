import React from 'react';
import Modal from '@/components/ui/modal';
import Input from '@/components/forms/input';
import ButtonOutline from '@/components/ui/button-outline';

export default function EditQuestionModal({
  show,
  editingQuestionId,
  value,
  onClose,
  onChange,
  onSave,
}) {
  if (!show) return null;

  return (
    <Modal
      title={editingQuestionId ? 'Edit Question' : 'New Question'}
      onClose={onClose}
    >
      <Input
        name="question"
        label="Question"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        autoFocus
      />
      <div className="buttons mt-6 flex justify-end gap-2">
        <ButtonOutline onClick={onClose} className="cancel">
          Cancel
        </ButtonOutline>
        <ButtonOutline onClick={onSave}>
          Save
        </ButtonOutline>
      </div>
    </Modal>
  );
}
