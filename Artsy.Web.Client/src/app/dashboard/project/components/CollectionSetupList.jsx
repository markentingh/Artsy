import React from 'react';
import { useCollection } from '@/context/collection';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import List, { Item } from '@/components/ui/list';
import Accordion from '@/components/ui/accordion';

export default function CollectionSetupList() {
  const {
    aiItems, collectionArtwork, step, STEPS,
    allProductImages, selectedProductCombos, productBlueprintImages, project,
    collectionId, api, setStep, setCollectionArtwork,
    setAllProductImages, setSelectedProductCombos,
    currentProductComboIndex, setCurrentProductComboIndex,
    loadItemData, currentItemIndex,
    projectQuestions, setProductImagePrompt,
  } = useCollection();

  const aiArtworks = collectionArtwork.filter(a => a.imageModel !== 'custom');
  const customItemIds = new Set(collectionArtwork.filter(a => a.imageModel === 'custom').map(a => String(a.itemId)));
  const aiBlueprintItems = aiItems.filter(item => !customItemIds.has(String(item.id)));
  const totalArtwork = aiBlueprintItems.length;
  const acceptedArtwork = aiArtworks.filter(a => a.accepted).length;

  const allProductCombos = productBlueprintImages.map(pbi => ({
    productImageId: pbi.id,
    projectBlueprintId: pbi.projectBlueprintId,
    blueprintName: pbi.blueprintName,
    title: pbi.title,
    variantColor: pbi.variantColor,
  }));
  const totalProductImages = allProductCombos.length;
  const acceptedProductImages = allProductImages.filter(img => img.accepted).length;

  const platforms = [];
  if (project?.publishToPrintify) platforms.push('Printify');

  const hasPQ = projectQuestions.length > 0;
  const checkIdx = {
    [STEPS.PROJECT_QUESTIONS]: 0,
    [STEPS.ARTWORK_QUESTIONS]: hasPQ ? 1 : 0,
    [STEPS.ARTWORK_PREVIEW]: hasPQ ? 1 : 0,
    [STEPS.READY_TO_GENERATE]: hasPQ ? 2 : 1,
    [STEPS.PRODUCT_IMAGE_PROMPT]: hasPQ ? 3 : 2,
    [STEPS.PRODUCT_IMAGE_PREVIEW]: hasPQ ? 3 : 2,
    [STEPS.CREATE_PRODUCTS]: hasPQ ? 4 : 3,
    [STEPS.PUBLISH_PRODUCTS]: hasPQ ? 5 : 4,
    [STEPS.SOCIAL_MEDIA]: hasPQ ? 6 : 5,
  };

  const isComplete = (itemStep) => {
    const currentIdx = checkIdx[step] ?? 0;
    return currentIdx > checkIdx[itemStep];
  };

  const isCurrent = (itemStep) => {
    const currentIdx = checkIdx[step] ?? -1;
    return currentIdx === checkIdx[itemStep];
  };

  const renderTitle = (label, complete, count, current = false) => (
    <>
      <Icon
        name={complete ? 'check_circle' : 'radio_button_unchecked'}
        className={complete
          ? 'text-green-500'
          : current
          ? 'text-blue-500'
          : 'text-gray-400 dark:text-gray-500'}
      />
      <span className={complete
        ? 'text-gray-500 dark:text-gray-400'
        : current
        ? 'text-blue-600 dark:text-blue-400'
        : 'text-gray-700 dark:text-gray-300'}>
        {label}
      </span>
      {count && (
        <span className="text-gray-500 dark:text-gray-400 font-medium ml-auto">
          {count}
        </span>
      )}
    </>
  );

  const handleSkipArtwork = async (itemId) => {
    if (!collectionId) return;
    try {
      await api.deleteCollectionArtwork({ collectionId, itemId });
      setCollectionArtwork(prev => prev.filter(a => String(a.itemId) !== String(itemId)));
    } catch (e) {
      console.error('deleteCollectionArtwork error:', e?.response?.data || e);
    }
  };

  const handleReviewArtwork = (itemId) => {
    const itemIndex = aiItems.findIndex(a => String(a.id) === String(itemId));
    if (itemIndex === -1) return;
    setStep(STEPS.ARTWORK_QUESTIONS);
    loadItemData(itemIndex);
  };

  const handleSkipProductImage = async (combo) => {
    if (!collectionId) return;
    try {
      await api.deleteProductImage({
        collectionId,
        projectBlueprintId: combo.projectBlueprintId,
        productImageId: combo.productImageId,
      });
      setAllProductImages(prev => prev.filter(img =>
        !(img.projectBlueprintId === combo.projectBlueprintId &&
          img.productImageId === combo.productImageId)
      ));
    } catch (e) {
      console.error('deleteProductImage error:', e?.response?.data || e);
    }
  };

  const handleReviewProductImage = (combo) => {
    const existingImg = allProductImages.find(img =>
      img.projectBlueprintId === combo.projectBlueprintId &&
      img.productImageId === combo.productImageId
    );
    if (existingImg?.prompt) {
      setProductImagePrompt(existingImg.prompt);
    }
    const comboIndex = selectedProductCombos.findIndex(c =>
      c.projectBlueprintId === combo.projectBlueprintId &&
      c.productImageId === combo.productImageId
    );
    if (comboIndex !== -1) {
      setCurrentProductComboIndex(comboIndex);
      setStep(STEPS.PRODUCT_IMAGE_PROMPT);
    } else {
      setSelectedProductCombos(prev => {
        const next = [...prev, combo];
        setCurrentProductComboIndex(next.length - 1);
        setStep(STEPS.PRODUCT_IMAGE_PROMPT);
        return next;
      });
    }
  };

  const artworkContent = (
    <List inModal>
      {aiBlueprintItems.map((item, idx) => {
        const artwork = collectionArtwork.find(a => String(a.itemId) === String(item.id));
        const isAccepted = artwork && artwork.accepted;
        const isCurrentItem = (step === STEPS.ARTWORK_QUESTIONS || step === STEPS.ARTWORK_PREVIEW) && idx === currentItemIndex;
        return (
          <Item key={item.id} className="justify-between text-sm">
            <div className="flex items-center gap-2">
              <Icon
                name={isAccepted ? 'check_circle' : 'radio_button_unchecked'}
                className={isAccepted
                  ? 'text-green-500'
                  : isCurrentItem
                  ? 'text-blue-500'
                  : 'text-gray-400 dark:text-gray-500'}
              />
              <span className={isAccepted
                ? 'text-gray-500 dark:text-gray-400'
                : isCurrentItem
                ? 'text-blue-600 dark:text-blue-400'
                : 'text-gray-700 dark:text-gray-300'}>
                {item.title || 'Untitled'}
              </span>
            </div>
            {isAccepted && (
              <ButtonOutline size="small" color="blue" onClick={() => handleReviewArtwork(item.id)}>
                Review
              </ButtonOutline>
            )}
          </Item>
        );
      })}
    </List>
  );

  const productImageContent = allProductCombos.length > 0 ? (
    <List inModal>
      {allProductCombos.map((combo, i) => {
        const isAccepted = allProductImages.some(img =>
          img.accepted &&
          img.projectBlueprintId === combo.projectBlueprintId &&
          img.productImageId === combo.productImageId
        );
        const currentCombo = selectedProductCombos[currentProductComboIndex];
        const isCurrentItem = (step === STEPS.PRODUCT_IMAGE_PROMPT || step === STEPS.PRODUCT_IMAGE_PREVIEW) &&
          currentCombo &&
          currentCombo.projectBlueprintId === combo.projectBlueprintId &&
          currentCombo.productImageId === combo.productImageId;
        return (
          <Item key={i} className="justify-between text-sm">
            <div className="flex items-center gap-2">
              <Icon
                name={isAccepted ? 'check_circle' : 'radio_button_unchecked'}
                className={isAccepted
                  ? 'text-green-500'
                  : isCurrentItem
                  ? 'text-blue-500'
                  : 'text-gray-400 dark:text-gray-500'}
              />
              <span className={isAccepted
                ? 'text-gray-500 dark:text-gray-400'
                : isCurrentItem
                ? 'text-blue-600 dark:text-blue-400'
                : 'text-gray-700 dark:text-gray-300'}>
                {combo.blueprintName} - {combo.title} ({combo.variantColor})
              </span>
            </div>
            <div className="flex items-center gap-1 ml-auto">
              {isAccepted && (
                <ButtonOutline size="small" color="gray" onClick={() => handleSkipProductImage(combo)}>
                  Skip
                </ButtonOutline>
              )}
              <ButtonOutline size="small" color="blue" onClick={() => handleReviewProductImage(combo)}>
                Review
              </ButtonOutline>
            </div>
          </Item>
        );
      })}
    </List>
  ) : null;

  const productImageComplete = acceptedProductImages > 0 && (totalProductImages === 0 ? isComplete(STEPS.PRODUCT_IMAGE_PREVIEW) : acceptedProductImages === totalProductImages);

  const accordionItems = [
    ...(projectQuestions.length > 0 ? [{
      title: renderTitle('Answer Project Questions', isComplete(STEPS.PROJECT_QUESTIONS), null, isCurrent(STEPS.PROJECT_QUESTIONS)),
      content: null,
      action: isComplete(STEPS.PROJECT_QUESTIONS) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.PROJECT_QUESTIONS)}>
          Review
        </ButtonOutline>
      ) : null,
    }] : []),
    {
      title: renderTitle('Generate Artworks using Questions & Answers', acceptedArtwork > 0 && acceptedArtwork === totalArtwork, totalArtwork > 0 ? `${acceptedArtwork}/${totalArtwork}` : null, isCurrent(STEPS.ARTWORK_QUESTIONS)),
      content: artworkContent,
    },
    {
      title: renderTitle('Upscale Artworks to 4K', aiArtworks.length > 0 && aiArtworks.filter(a => a.accepted).every(a => a.fullSize), null, isCurrent(STEPS.READY_TO_GENERATE)),
      content: null,
      action: aiArtworks.filter(a => a.accepted).some(a => !a.fullSize) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.READY_TO_GENERATE)}>
          Review
        </ButtonOutline>
      ) : null,
    },
    {
      title: renderTitle('Generate Product Images', productImageComplete, totalProductImages > 0 ? `${acceptedProductImages}/${totalProductImages}` : (acceptedProductImages > 0 ? `${acceptedProductImages}/${acceptedProductImages}` : null), isCurrent(STEPS.PRODUCT_IMAGE_PROMPT)),
      content: productImageContent,
    },
    {
      title: renderTitle(`Create Products on ${platforms.join(', ') || 'Platform'}`, false, null, isCurrent(STEPS.CREATE_PRODUCTS)),
      content: null,
      action: totalProductImages > 0 && acceptedProductImages === totalProductImages && !isCurrent(STEPS.CREATE_PRODUCTS) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.CREATE_PRODUCTS)}>
          Review
        </ButtonOutline>
      ) : null,
    },
    {
      title: renderTitle(`Publish Products on ${platforms.join(', ') || 'Platform'}`, false, null, isCurrent(STEPS.PUBLISH_PRODUCTS)),
      content: null,
      action: isComplete(STEPS.CREATE_PRODUCTS) && !isCurrent(STEPS.PUBLISH_PRODUCTS) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.PUBLISH_PRODUCTS)}>
          Review
        </ButtonOutline>
      ) : null,
    },
    {
      title: renderTitle('Post to Social Media', false, null),
      content: null,
    },
  ];

  return (
    <Accordion inModal items={accordionItems} className="mb-2" />
  );
}
