import React, { useEffect, useState } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Modal from '@/components/ui/modal';
import Input from '@/components/forms/input';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Message from '@/components/ui/message';

export default function EditArtworkModal({ show, item, onClose, onBlueprintsChanged }) {
  const session = useSession();
  const {
    getItemBlueprints, createItemBlueprint, deleteItemBlueprint
  } = Projects(session);

  const [blueprints, setBlueprints] = useState([]);
  const [newBlueprintName, setNewBlueprintName] = useState('');
  const [newBlueprintId, setNewBlueprintId] = useState('');
  const [message, setMessage] = useState(null);

  useEffect(() => {
    if (!show || !item) return;
    setNewBlueprintName('');
    setNewBlueprintId('');
    setMessage(null);
    const fetchBlueprints = async () => {
      try {
        const response = await getItemBlueprints(item.id);
        if (response.data.success) {
          setBlueprints(response.data.data || []);
        } else {
          setMessage({ type: 'error', text: response.data.message || 'Failed to load blueprints' });
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load blueprints' });
      }
    };
    fetchBlueprints();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [show, item]);

  const handleAddBlueprint = async () => {
    if (!item) return;
    const name = newBlueprintName.trim();
    const blueprintId = parseInt(newBlueprintId, 10);
    if (!name || Number.isNaN(blueprintId)) {
      setMessage({ type: 'error', text: 'Blueprint name and ID are required.' });
      return;
    }
    try {
      const response = await createItemBlueprint({ itemId: item.id, blueprintId, name });
      if (response.data.success) {
        setBlueprints((prev) => [...prev, response.data.data]);
        setNewBlueprintName('');
        setNewBlueprintId('');
        setMessage(null);
        if (onBlueprintsChanged) onBlueprintsChanged(item.id);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to add blueprint' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to add blueprint' });
    }
  };

  const handleDeleteBlueprint = async (blueprintId) => {
    if (!item) return;
    try {
      const response = await deleteItemBlueprint({ id: blueprintId });
      if (response.data.success) {
        setBlueprints((prev) => prev.filter((b) => b.id !== blueprintId));
        if (onBlueprintsChanged) onBlueprintsChanged(item.id);
      } else {
        setMessage({ type: 'error', text: response.data.message || 'Failed to delete blueprint' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete blueprint' });
    }
  };

  if (!show || !item) return null;

  return (
    <Modal
      title={`Edit Artwork #${String(item.index).padStart(2, '0')}`}
      onClose={onClose}
    >
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <h3 className="text-sm font-medium text-gray-600 dark:text-gray-300 mb-2">Product Blueprints</h3>
      {blueprints.length === 0 ? (
        <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">No product blueprints assigned.</p>
      ) : (
        <ul className="mb-4 space-y-1">
          {blueprints.map((blueprint) => (
            <li
              key={blueprint.id}
              className="flex items-center justify-between rounded bg-gray-100 dark:bg-gray-700 px-3 py-2"
            >
              <span>{blueprint.name}</span>
              <ButtonIcon name="delete" onClick={() => handleDeleteBlueprint(blueprint.id)} title="Remove blueprint" />
            </li>
          ))}
        </ul>
      )}
      <div className="flex gap-2 items-end">
        <Input
          name="blueprintId"
          label="Blueprint ID"
          type="number"
          value={newBlueprintId}
          onChange={(e) => setNewBlueprintId(e.target.value)}
          className="w-24"
        />
        <Input
          name="blueprintName"
          label="Name"
          value={newBlueprintName}
          onChange={(e) => setNewBlueprintName(e.target.value)}
        />
        <ButtonOutline onClick={handleAddBlueprint}>
          Add
        </ButtonOutline>
      </div>
      <div className="buttons mt-6 flex justify-end gap-2">
        <ButtonOutline onClick={onClose} className="cancel">
          Close
        </ButtonOutline>
      </div>
    </Modal>
  );
}
