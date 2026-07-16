import React, { useState, useEffect } from 'react';
import Modal from '@/components/ui/modal';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import Input from '@/components/forms/input';
import Select from '@/components/forms/select';
import TextArea from '@/components/forms/textarea';
import Checkbox from '@/components/forms/checkbox';
import Message from '@/components/ui/message';
import { useSession } from '@/context/session';
import { OpenAI } from '@/api/admin/openai';

const LLM_TYPES = [
    { value: 0, label: 'Local' },
    { value: 1, label: 'Cloud' }
];

export default function AdminOpenAI() {
    const session = useSession();
    const { getAll, add, update, setEnabled, setPreferred, delete: deleteModel } = OpenAI(session);

    const getEmptyForm = () => ({
        modelId: 0,
        name: '',
        model: '',
        endpoint: '',
        privateKey: '',
        type: 1,
        enabled: false,
        preferred: false,
        extraBody: ''
    });

    const [models, setModels] = useState([]);
    const [showModal, setShowModal] = useState(false);
    const [editingModel, setEditingModel] = useState(null);
    const [form, setForm] = useState(getEmptyForm);
    const [error, setError] = useState(null);
    const [message, setMessage] = useState(null);

    useEffect(() => {
        fetchModels();
    }, []);

    const fetchModels = () => {
        getAll().then(response => {
            if (response.data.success) {
                setModels(response.data.data || []);
            }
        }).catch(error => {
            console.error('Error fetching LLM models:', error);
            setMessage({ type: 'error', text: 'Failed to fetch LLM models' });
        });
    };

    const handleAdd = () => {
        setEditingModel(null);
        setForm(getEmptyForm());
        setError(null);
        setShowModal(true);
    };

    const handleEdit = (model) => {
        setEditingModel(model);
        setForm({
            modelId: model.modelId,
            name: model.name || '',
            model: model.model || '',
            endpoint: model.endpoint || '',
            privateKey: model.privateKey || '',
            type: model.type ?? 1,
            enabled: !!model.enabled,
            preferred: !!model.preferred,
            extraBody: model.extraBody || ''
        });
        setError(null);
        setShowModal(true);
    };

    const handleSave = () => {
        if (!form.name || !form.model || !form.endpoint) {
            setError('Name, Model, and Endpoint are required');
            return;
        }

        setError(null);
        const payload = { ...form };
        const action = editingModel ? update : add;
        action(payload).then(response => {
            if (response.data.success) {
                fetchModels();
                setShowModal(false);
            } else {
                setError(response.data.message || 'Failed to save model');
            }
        }).catch(error => {
            console.error('Error saving LLM model:', error);
            setError('Failed to save model');
        });
    };

    const handleToggleEnabled = (model) => {
        const newEnabled = !model.enabled;
        setEnabled(model.modelId, newEnabled).then(response => {
            if (response.data.success) {
                fetchModels();
            } else {
                console.error('Failed to update enabled state:', response.data.message);
            }
        }).catch(error => {
            console.error('Error updating enabled state:', error);
        });
    };

    const handleTogglePreferred = (model) => {
        if (model.preferred) return;
        setPreferred(model.modelId).then(response => {
            if (response.data.success) {
                fetchModels();
            } else {
                console.error('Failed to update preferred state:', response.data.message);
            }
        }).catch(error => {
            console.error('Error updating preferred state:', error);
        });
    };

    const handleDelete = (model) => {
        if (!confirm(`Are you sure you want to delete the model "${model.name}"?`)) return;
        deleteModel(model.modelId).then(response => {
            if (response.data.success) {
                fetchModels();
            }
        }).catch(error => {
            console.error('Error deleting model:', error);
        });
    };

    const handleFormChange = (field, value) => {
        setForm(prev => ({ ...prev, [field]: value }));
    };

    return (
        <div className="admin-openai">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-3xl">OpenAI Endpoints</h1>
                <ButtonOutline onClick={handleAdd}>
                    <Icon name="add" />
                    <span className="ml-2">Add Model</span>
                </ButtonOutline>
            </div>

            {message && (
                <Message type={message.type} onClose={() => setMessage(null)}>
                    {message.text}
                </Message>
            )}

            {showModal && (
                <Modal title={editingModel ? 'Edit LLM Model' : 'Add LLM Model'} onClose={() => setShowModal(false)}>
                    {error && <div className="mb-4 p-3 rounded bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200">{error}</div>}
                    <Input
                        label="Name"
                        name="name"
                        value={form.name}
                        onInput={(e) => handleFormChange('name', e.target.value)}
                    />
                    <Input
                        label="Model"
                        name="model"
                        value={form.model}
                        onInput={(e) => handleFormChange('model', e.target.value)}
                    />
                    <Input
                        label="Endpoint"
                        name="endpoint"
                        value={form.endpoint}
                        onInput={(e) => handleFormChange('endpoint', e.target.value)}
                    />
                    <Input
                        label="Private Key"
                        name="privateKey"
                        type="password"
                        value={form.privateKey}
                        onInput={(e) => handleFormChange('privateKey', e.target.value)}
                    />
                    <Select
                        label="Type"
                        name="type"
                        options={LLM_TYPES}
                        value={form.type}
                        onChange={(e) => handleFormChange('type', parseInt(e.target.value))}
                    />
                    <TextArea
                        label="Extra Body (JSON)"
                        name="extraBody"
                        value={form.extraBody}
                        rows={3}
                        onInput={(e) => handleFormChange('extraBody', e.target.value)}
                    />
                    <Checkbox
                        name="enabled"
                        label="Enabled"
                        checked={form.enabled}
                        onChange={(e) => handleFormChange('enabled', e.target.checked)}
                    />
                    <Checkbox
                        name="preferred"
                        label="Preferred"
                        checked={form.preferred}
                        onChange={(e) => handleFormChange('preferred', e.target.checked)}
                    />
                    <div className="buttons">
                        <button type="button" onClick={handleSave}>Save</button>
                        <button type="button" onClick={() => setShowModal(false)} className="cancel">Cancel</button>
                    </div>
                </Modal>
            )}

            <div className="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
                <table className="w-full text-left border-collapse">
                    <thead className="bg-gray-100 dark:bg-gray-700">
                        <tr>
                            <th className="px-4 py-3 w-16 text-center"></th>
                            <th className="px-4 py-3">Name</th>
                            <th className="px-4 py-3">Model</th>
                            <th className="px-4 py-3">Endpoint</th>
                            <th className="px-4 py-3">Type</th>
                            <th className="px-4 py-3 w-40"></th>
                        </tr>
                    </thead>
                    <tbody>
                        {models.map(model => (
                            <tr
                                key={model.modelId}
                                onClick={() => handleEdit(model)}
                                className="border-b border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/50 cursor-pointer"
                            >
                                <td className="px-4 py-3 text-center cursor-default" onClick={(e) => e.stopPropagation()}>
                                    <input
                                        type="checkbox"
                                        checked={model.enabled}
                                        onChange={() => handleToggleEnabled(model)}
                                        title={model.enabled ? 'Disable' : 'Enable'}
                                        className="w-[18px] h-[18px] cursor-pointer"
                                    />
                                </td>
                                <td className="px-4 py-3">{model.name}</td>
                                <td className="px-4 py-3">{model.model}</td>
                                <td className="px-4 py-3 truncate max-w-xs">{model.endpoint}</td>
                                <td className="px-4 py-3">{model.type === 0 ? 'Local' : 'Cloud'}</td>
                                <td className="px-4 py-3 space-x-2" onClick={(e) => e.stopPropagation()}>
                                    <button
                                        type="button"
                                        className={'icon ' + (model.preferred ? 'text-amber-400' : '')}
                                        onClick={() => handleTogglePreferred(model)}
                                        title={model.preferred ? 'Preferred' : 'Set as preferred'}
                                    >
                                        <Icon name={model.preferred ? 'star_shine' : 'star'} />
                                    </button>
                                    <button
                                        type="button"
                                        className="icon"
                                        onClick={() => handleEdit(model)}
                                        title="Edit model"
                                    >
                                        <Icon name="edit" />
                                    </button>
                                    <button
                                        type="button"
                                        className="icon"
                                        onClick={() => handleDelete(model)}
                                        title="Delete model"
                                    >
                                        <Icon name="delete" />
                                    </button>
                                </td>
                            </tr>
                        ))}
                        {models.length === 0 && (
                            <tr>
                                <td colSpan="6" className="text-center py-8 text-gray-600 dark:text-gray-400">
                                    No Open AI endpoints configured. Click "Add Model" to get started.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
