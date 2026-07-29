import React from 'react';
import Input from '@/components/forms/input';
import TextArea from '@/components/forms/textarea';
import { useProductBlueprint } from '@/context/productBlueprint';

export default function InfoTab() {
  const { productName, setProductName, productDescription, setProductDescription, safetyInfo, setSafetyInfo } = useProductBlueprint();

  return (
    <div>
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
