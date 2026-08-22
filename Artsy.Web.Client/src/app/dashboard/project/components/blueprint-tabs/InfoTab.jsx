import React from 'react';
import Input from '@/components/forms/input';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import Spinner from '@/components/ui/spinner';
import { useProductBlueprint } from '@/context/productBlueprint';

export default function InfoTab() {
  const { productName, setProductName, productDescription, setProductDescription, safetyInfo, setSafetyInfo, handleGenerateInfo, generatingInfo } = useProductBlueprint();

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Product Name & Description</span>
        <ButtonOutline onClick={handleGenerateInfo} disabled={generatingInfo} className="!py-1 !px-3 !text-sm">
          {generatingInfo ? <Spinner className="text-base" /> : 'Generate Info'}
        </ButtonOutline>
      </div>
      <Input
        name="productName"
        label="Name"
        value={productName}
        onChange={(e) => setProductName(e.target.value)}
        placeholder="Enter product name"
      />
      <TextArea
        name="productDescription"
        label="Description"
        value={productDescription}
        onChange={(e) => setProductDescription(e.target.value)}
        placeholder="Enter product description"
        rows={5}
      />
      <TextArea
        name="safetyInfo"
        label="Safety Information"
        value={safetyInfo}
        onChange={(e) => setSafetyInfo(e.target.value)}
        placeholder="Enter safety information"
        rows={5}
      />
    </div>
  );
}
