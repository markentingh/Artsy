import React from 'react';
import { useCollection } from '@/context/collection';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import List, { Item } from '@/components/ui/list';
import Accordion from '@/components/ui/accordion';

export default function CollectionSetupList() {
  const {
    aiItems, collectionArtwork, step, STEPS, maxStepIndex,
    allProductImages, selectedProductCombos, productBlueprintImages, project,
    collectionId, api, setStep, setCollectionArtwork,
    setAllProductImages,
    currentProductComboIndex,
    currentItemIndex,
    projectQuestions,
    printifyProducts, mockups,
    collectionProducts,
    reviewStep,
    instagramPosted,
  } = useCollection();

  const aiArtworks = collectionArtwork.filter(a => a.imageModel !== 'custom');
  const customItemIds = new Set(collectionArtwork.filter(a => a.imageModel === 'custom').map(a => String(a.itemId)));
  const aiBlueprintItems = aiItems.filter(item => !customItemIds.has(String(item.id)));
  const totalArtwork = aiBlueprintItems.length;
  const acceptedArtwork = aiArtworks.filter(a => a.accepted).length;
  const allArtworksUpscaled = aiBlueprintItems.length > 0 && aiBlueprintItems.every(item => {
    const art = collectionArtwork.find(a => String(a.itemId) === String(item.id));
    return art && art.accepted && art.fullSize;
  });

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
    [STEPS.CREATE_PRODUCTS]: hasPQ ? 3 : 2,
    [STEPS.PRODUCT_IMAGE_PROMPT]: hasPQ ? 4 : 3,
    [STEPS.PRODUCT_IMAGE_PREVIEW]: hasPQ ? 4 : 3,
    [STEPS.PUBLISH_PRODUCTS]: hasPQ ? 5 : 4,
    [STEPS.SOCIAL_MEDIA]: hasPQ ? 6 : 5,
    [STEPS.SUMMARY]: hasPQ ? 7 : 6,
  };

  const currentStepIdx = checkIdx[step] ?? 0;
  const maxStepIdx = Math.max(maxStepIndex, currentStepIdx);

  const isCurrent = (itemStep) => {
    const currentIdx = checkIdx[step] ?? -1;
    return currentIdx === checkIdx[itemStep];
  };

  const isComplete = (itemStep) => {
    if (maxStepIdx <= checkIdx[itemStep]) return false;
    if (itemStep === STEPS.CREATE_PRODUCTS) {
      return mockups.length > 0 && printifyProducts.some(pp => pp.mockupsDownloaded);
    }
    return true;
  };

  const renderTitle = (label, complete, count, current = false) => (
    <>
      <Icon
        name={complete ? 'check_circle' : 'radio_button_unchecked'}
        className={complete && current
          ? 'text-blue-500'
          : complete
          ? 'text-green-500'
          : current
          ? 'text-blue-500'
          : 'text-gray-400 dark:text-gray-500'}
      />
      <span className={complete && current
        ? 'text-blue-600 dark:text-blue-400'
        : complete
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

  const handleReviewArtwork = (itemId) => {
    reviewStep(STEPS.ARTWORK_QUESTIONS, itemId);
  };

  const handleReviewProductImage = (combo) => {
    reviewStep(STEPS.PRODUCT_IMAGE_PROMPT, combo);
  };

  const firstUnacceptedIdx = aiBlueprintItems.findIndex((item) => {
    const art = collectionArtwork.find(a => String(a.itemId) === String(item.id));
    return !(art && art.accepted);
  });

  const artworkContent = (
    <List inModal>
      {aiBlueprintItems.map((item, idx) => {
        const artwork = collectionArtwork.find(a => String(a.itemId) === String(item.id));
        const isAccepted = artwork && artwork.accepted;
        const currentItemId = aiItems[currentItemIndex]?.id;
        const isCurrentItem = (step === STEPS.ARTWORK_QUESTIONS || step === STEPS.ARTWORK_PREVIEW) && String(item.id) === String(currentItemId);
        const isOnArtworkStep = step === STEPS.ARTWORK_QUESTIONS || step === STEPS.ARTWORK_PREVIEW;
        const isNextUnaccepted = !isOnArtworkStep && idx === firstUnacceptedIdx;
        const isHighlighted = isCurrentItem || isNextUnaccepted;
        return (
          <Item key={item.id} className="justify-between text-sm">
            <div className="flex items-center gap-2">
              <Icon
                name={isAccepted ? 'check_circle' : 'radio_button_unchecked'}
                className={isAccepted && isHighlighted
                  ? 'text-blue-500'
                  : isAccepted
                  ? 'text-green-500'
                  : isHighlighted
                  ? 'text-blue-500'
                  : 'text-gray-400 dark:text-gray-500'}
              />
              <span className={isAccepted && isHighlighted
                ? 'text-blue-600 dark:text-blue-400'
                : isAccepted
                ? 'text-gray-500 dark:text-gray-400'
                : isHighlighted
                ? 'text-blue-600 dark:text-blue-400'
                : 'text-gray-700 dark:text-gray-300'}>
                {item.title || 'Untitled'}
              </span>
            </div>
            {((isAccepted && !isCurrentItem) || (idx === firstUnacceptedIdx && !isCurrentItem)) && (
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
        const cp = collectionProducts.find(p => p.projectBlueprintId === combo.projectBlueprintId);
        const displayName = cp?.name || combo.blueprintName;
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
                className={isAccepted && isCurrentItem
                  ? 'text-blue-500'
                  : isAccepted
                  ? 'text-green-500'
                  : isCurrentItem
                  ? 'text-blue-500'
                  : 'text-gray-400 dark:text-gray-500'}
              />
              <span className={isAccepted && isCurrentItem
                ? 'text-blue-600 dark:text-blue-400'
                : isAccepted
                ? 'text-gray-500 dark:text-gray-400'
                : isCurrentItem
                ? 'text-blue-600 dark:text-blue-400'
                : 'text-gray-700 dark:text-gray-300'}>
                {displayName}
              </span>
            </div>
            <div className="flex items-center gap-1 ml-auto">
              {!isCurrentItem && (
                <ButtonOutline size="small" color="blue" onClick={() => handleReviewProductImage(combo)}>
                  Review
                </ButtonOutline>
              )}
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
      action: isComplete(STEPS.PROJECT_QUESTIONS) && !isCurrent(STEPS.PROJECT_QUESTIONS) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.PROJECT_QUESTIONS)}>
          Review
        </ButtonOutline>
      ) : null,
    }] : []),
    {
      title: renderTitle('Generate Artworks', acceptedArtwork > 0 && acceptedArtwork === totalArtwork, totalArtwork > 0 ? `${acceptedArtwork}/${totalArtwork}` : null, isCurrent(STEPS.ARTWORK_QUESTIONS)),
      content: artworkContent,
    },
    {
      title: renderTitle('Upscale Artworks to 4K', allArtworksUpscaled, null, isCurrent(STEPS.READY_TO_GENERATE)),
      content: null,
      action: !allArtworksUpscaled && totalArtwork > 0 && acceptedArtwork === totalArtwork && aiArtworks.filter(a => a.accepted).some(a => !a.fullSize) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.READY_TO_GENERATE)}>
          Review
        </ButtonOutline>
      ) : null,
    },
    {
      title: renderTitle(`Create Products on ${platforms.join(', ') || 'Platform'}`, isComplete(STEPS.CREATE_PRODUCTS), null, isCurrent(STEPS.CREATE_PRODUCTS)),
      content: null,
      action: isComplete(STEPS.READY_TO_GENERATE) && !isCurrent(STEPS.CREATE_PRODUCTS) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.CREATE_PRODUCTS)}>
          Review
        </ButtonOutline>
      ) : null,
    },
    {
      title: renderTitle('Generate Product Images', productImageComplete, totalProductImages > 0 ? `${acceptedProductImages}/${totalProductImages}` : (acceptedProductImages > 0 ? `${acceptedProductImages}/${acceptedProductImages}` : null), isCurrent(STEPS.PRODUCT_IMAGE_PROMPT)),
      content: productImageContent,
      action: null,
    },
    {
      title: renderTitle(`Publish Products on ${platforms.join(', ') || 'Platform'}`, isComplete(STEPS.PUBLISH_PRODUCTS), null, isCurrent(STEPS.PUBLISH_PRODUCTS)),
      content: null,
      action: isComplete(STEPS.CREATE_PRODUCTS) && !isCurrent(STEPS.PUBLISH_PRODUCTS) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.PUBLISH_PRODUCTS)}>
          Review
        </ButtonOutline>
      ) : null,
    },
    {
      title: renderTitle('Post to Social Media', instagramPosted, null, isCurrent(STEPS.SOCIAL_MEDIA)),
      content: null,
      action: printifyProducts.length > 0 && printifyProducts.every(pp => pp.published) && !isCurrent(STEPS.SOCIAL_MEDIA) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.SOCIAL_MEDIA)}>
          Review
        </ButtonOutline>
      ) : null,
    },
    {
      title: renderTitle('Summary', instagramPosted, null, isCurrent(STEPS.SUMMARY)),
      content: null,
      action: instagramPosted && !isCurrent(STEPS.SUMMARY) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.SUMMARY)}>
          Review
        </ButtonOutline>
      ) : null,
    },
  ];

  return (
    <Accordion inModal items={accordionItems} className="mb-2" />
  );
}
