import React, { useState, useEffect } from 'react';
import Modal from '@/components/ui/modal';
import Input from '@/components/forms/input';
import Checkbox from '@/components/forms/checkbox';
import ButtonOutline from '@/components/ui/button-outline';
import Message from '@/components/ui/message';

export default function ImageGenerationModal({ show, model, onClose, onSave }) {
    const [form, setForm] = useState({
        modelKey: '',
        name: '',
        model: '',
        cpmitTokens: '',
        cpmiiTokens: '',
        cpmoTokens: '',
        active: true,
        tokenConversion: '10'
    });
    const [error, setError] = useState(null);
    const [message, setMessage] = useState(null);

    useEffect(() => {
        if (model) {
            setForm({
                modelKey: model.modelKey || '',
                name: model.name || '',
                model: model.model || '',
                cpmitTokens: model.cpmitTokens?.toString() || '0',
                cpmiiTokens: model.cpmiiTokens?.toString() || '0',
                cpmoTokens: model.cpmoTokens?.toString() || '0',
                active: model.active !== false,
                tokenConversion: model.tokenConversion?.toString() || '10'
            });
        } else {
            setForm({
                modelKey: '',
                name: '',
                model: '',
                cpmitTokens: '0',
                cpmiiTokens: '0',
                cpmoTokens: '0',
                active: true,
                tokenConversion: '10'
            });
        }
        setError(null);
        setMessage(null);
    }, [model, show]);

    if (!show) return null;

    const handleChange = (field, value) => {
        setForm(prev => ({ ...prev, [field]: value }));
    };

    const handleSave = () => {
        if (!form.modelKey) {
            setError('Model Key is required');
            return;
        }
        if (!form.name || !form.model) {
            setError('Name and Model are required');
            return;
        }

        setError(null);
        const payload = {
            id: model?.id || 0,
            modelKey: form.modelKey,
            name: form.name,
            model: form.model,
            cpmitTokens: parseFloat(form.cpmitTokens) || 0,
            cpmiiTokens: parseFloat(form.cpmiiTokens) || 0,
            cpmoTokens: parseFloat(form.cpmoTokens) || 0,
            active: form.active,
            tokenConversion: parseFloat(form.tokenConversion) || 10
        };

        if (onSave) {
            onSave(payload);
        }
        if (onClose) {
            onClose();
        }
    };

    return (
        <Modal title={model ? 'Edit Image Generation Model' : 'Add Image Generation Model'} onClose={onClose}>
            {error && (
                <div className="mb-4 p-3 rounded bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200">{error}</div>
            )}
            {message && (
                <Message type={message.type} onClose={() => setMessage(null)}>
                    {message.text}
                </Message>
            )}
            <Input
                label="Model Key"
                name="modelKey"
                value={form.modelKey}
                onInput={(e) => handleChange('modelKey', e.target.value)}
            />
            <Input
                label="Name"
                name="name"
                value={form.name}
                onInput={(e) => handleChange('name', e.target.value)}
            />
            <Input
                label="Model"
                name="model"
                value={form.model}
                onInput={(e) => handleChange('model', e.target.value)}
            />
            <Input
                label="Cost Per Million Text Input Tokens"
                name="cpmitTokens"
                type="number"
                value={form.cpmitTokens}
                onInput={(e) => handleChange('cpmitTokens', e.target.value)}
            />
            <Input
                label="Cost Per Million Image Input Tokens"
                name="cpmiiTokens"
                type="number"
                value={form.cpmiiTokens}
                onInput={(e) => handleChange('cpmiiTokens', e.target.value)}
            />
            <Input
                label="Cost Per Million Output Tokens"
                name="cpmoTokens"
                type="number"
                value={form.cpmoTokens}
                onInput={(e) => handleChange('cpmoTokens', e.target.value)}
            />
            <Input
                label={`Convert To Platform Tokens: 1000 * ${form.tokenConversion || 0} = ${(1000 * (parseFloat(form.tokenConversion) || 0)).toLocaleString()}`}
                name="tokenConversion"
                type="number"
                value={form.tokenConversion}
                onInput={(e) => handleChange('tokenConversion', e.target.value)}
            />
            <Checkbox
                name="active"
                label="Active"
                checked={form.active}
                onChange={(e) => handleChange('active', e.target.checked)}
            />
            <div className="buttons flex justify-end gap-2">
                <ButtonOutline onClick={onClose} className="cancel">
                    Cancel
                </ButtonOutline>
                <ButtonOutline onClick={handleSave}>
                    Save Changes
                </ButtonOutline>
            </div>
        </Modal>
    );
}
