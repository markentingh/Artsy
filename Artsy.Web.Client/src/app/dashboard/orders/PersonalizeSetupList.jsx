import React from 'react';
import { usePersonalizeOrderItem } from '@/context/personalizeOrderItem';
import Icon from '@/components/ui/icon';
import ButtonOutline from '@/components/ui/button-outline';
import List, { Item } from '@/components/ui/list';
import Accordion from '@/components/ui/accordion';

export default function PersonalizeSetupList() {
  const {
    STEPS,
    step,
    artworks,
    usedArtworks,
    currentArtworkIndex,
    setStep,
    setCurrentArtworkIndex,
  } = usePersonalizeOrderItem();

  const totalArtworks = usedArtworks.length || 1;
  const generateComplete = artworks.length >= totalArtworks;
  const downloadComplete = artworks.length > 0 && step === STEPS.DOWNLOAD;

  const isCurrent = (targetStep) => step === targetStep;

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
      <span className={`whitespace-nowrap ${complete && current
        ? 'text-blue-600 dark:text-blue-400'
        : complete
        ? 'text-gray-500 dark:text-gray-400'
        : current
        ? 'text-blue-600 dark:text-blue-400'
        : 'text-gray-700 dark:text-gray-300'}`}>
        {label}
      </span>
      {count && (
        <span className="text-gray-500 dark:text-gray-400 font-medium ml-auto">
          {count}
        </span>
      )}
    </>
  );

  const artworkContent = (
    <List inModal>
      {usedArtworks.map((artwork, idx) => {
        const generated = idx < artworks.length;
        const current = step === STEPS.GENERATE && currentArtworkIndex === idx;
        const title = artwork.artworkItemTitle || artwork.artworkPrompt || artwork.artworkImageModel || `Artwork ${idx + 1}`;
        return (
          <Item key={artwork.id || idx} className="justify-between text-sm" onClick={() => { setStep(STEPS.GENERATE); setCurrentArtworkIndex(idx); }}>
            <div className="flex items-center gap-2">
              <Icon
                name={generated ? 'check_circle' : 'radio_button_unchecked'}
                className={generated && current
                  ? 'text-blue-500'
                  : generated
                  ? 'text-green-500'
                  : current
                  ? 'text-blue-500'
                  : 'text-gray-400 dark:text-gray-500'}
              />
              <span className={`whitespace-nowrap ${generated && current
                ? 'text-blue-600 dark:text-blue-400'
                : generated
                ? 'text-gray-500 dark:text-gray-400'
                : current
                ? 'text-blue-600 dark:text-blue-400'
                : 'text-gray-700 dark:text-gray-300'}`}>
                {title}
              </span>
            </div>
            {((generated && !current) || (idx === artworks.length && !current)) && (
              <ButtonOutline size="small" color="blue" onClick={() => { setStep(STEPS.GENERATE); setCurrentArtworkIndex(idx); }}>
                Review
              </ButtonOutline>
            )}
          </Item>
        );
      })}
    </List>
  );

  const accordionItems = [
    {
      title: renderTitle('Generate Personalized Artworks', generateComplete, `${artworks.length}/${totalArtworks}`, isCurrent(STEPS.GENERATE)),
      content: artworkContent,
    },
    {
      title: renderTitle('Download Artworks', downloadComplete, null, isCurrent(STEPS.DOWNLOAD)),
      content: null,
      action: generateComplete && !isCurrent(STEPS.DOWNLOAD) ? (
        <ButtonOutline size="small" color="blue" onClick={() => setStep(STEPS.DOWNLOAD)}>
          Review
        </ButtonOutline>
      ) : null,
    },
  ];

  return (
    <Accordion inModal items={accordionItems} className="mb-2" />
  );
}
