import React, { useEffect, useState, useMemo } from 'react';
import { useSession } from '@/context/session';
import { Projects } from '@/api/user/projects';
import Modal from '@/components/ui/modal';
import TextArea from '@/components/forms/textarea';
import ButtonOutline from '@/components/ui/button-outline';
import ButtonIcon from '@/components/ui/button-icon';
import Icon from '@/components/ui/icon';
import Message from '@/components/ui/message';
import ProgressBar from '@/components/ui/progress-bar';
import Carousel from '@/components/ui/carousel';
import { List, Item } from '@/components/ui/list';

export default function IdeaModal({ projectId, ideaId, onClose, onIdeaCreated, onCollectionCreated }) {
  const session = useSession();
  const {
    getIdea, createIdea, createIdeaVariation, deleteIdea, makeIdeaCollection,
    getQuestions, getItems, getItemQuestions,
  } = Projects(session);

  const [step, setStep] = useState(ideaId ? 'loading' : 'input');
  const [prompt, setPrompt] = useState('');
  const [idea, setIdea] = useState(null);
  const [generating, setGenerating] = useState(false);
  const [message, setMessage] = useState(null);
  const [projectQuestions, setProjectQuestions] = useState([]);
  const [items, setItems] = useState([]);
  const [itemQuestions, setItemQuestions] = useState([]);
  const [openVariationIds, setOpenVariationIds] = useState(new Set());
  const [progress, setProgress] = useState(0);
  const [progressMessage, setProgressMessage] = useState('');
  const [currentVariation, setCurrentVariation] = useState(0);

  useEffect(() => {
    const fetchBase = async () => {
      try {
        const [qRes, itemsRes] = await Promise.all([
          getQuestions(projectId),
          getItems(projectId),
        ]);
        if (qRes.data.success) setProjectQuestions(qRes.data.data || []);
        if (itemsRes.data.success) {
          const loadedItems = itemsRes.data.data || [];
          setItems(loadedItems);
          const iqRes = await Promise.all(loadedItems.map((i) => getItemQuestions(i.id)));
          const all = [];
          iqRes.forEach((res, idx) => {
            if (res.data.success) {
              all.push(...(res.data.data || []).map((q) => ({ ...q, itemId: loadedItems[idx].id, itemTitle: loadedItems[idx].title })));
            }
          });
          setItemQuestions(all);
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load project data' });
      }
    };
    fetchBase();
  }, [projectId]);

  useEffect(() => {
    if (!ideaId) return;
    const fetchIdea = async () => {
      try {
        const res = await getIdea(projectId, ideaId);
        if (res.data.success) {
          const loadedIdea = res.data.data;
          setIdea(loadedIdea);
          const variations = loadedIdea?.variations || [];
          setStep(variations.length < 5 ? 'continue' : 'results');
        } else {
          setMessage({ type: 'error', text: res.data.message || 'Failed to load idea' });
          setStep('input');
        }
      } catch (error) {
        setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to load idea' });
        setStep('input');
      }
    };
    fetchIdea();
  }, [projectId, ideaId]);

  const projectQuestionById = useMemo(() => {
    const map = {};
    projectQuestions.forEach((q) => { map[q.id] = q.question; });
    return map;
  }, [projectQuestions]);

  const itemQuestionById = useMemo(() => {
    const map = {};
    itemQuestions.forEach((q) => { map[q.id] = q; });
    return map;
  }, [itemQuestions]);

  const itemsById = useMemo(() => {
    const map = {};
    items.forEach((i) => { map[i.id] = i; });
    return map;
  }, [items]);



  const parsedVariations = useMemo(() => {
    if (!idea?.variations) return [];
    return idea.variations.map((v) => {
      let parsed = {};
      try { parsed = JSON.parse(v.ideaJson || '{}'); } catch { }
      return { ...v, parsed };
    });
  }, [idea]);

  const handleExpandIdea = async () => {
    const trimmed = prompt.trim();
    if (!trimmed) {
      setMessage({ type: 'error', text: 'Please enter an idea first.' });
      return;
    }
    setStep('generating');
    setMessage(null);
    setProgress(0);
    setCurrentVariation(0);
    setProgressMessage('Generating idea details...');

    let ideaData;
    try {
      const totalSteps = 1 + 5;
      const ideaRes = await createIdea(projectId, { prompt: trimmed });
      if (!ideaRes.data.success) {
        setMessage({ type: 'error', text: ideaRes.data.message || 'Failed to expand idea' });
        setStep('input');
        return;
      }
      ideaData = ideaRes.data.data;
      setIdea(ideaData);
      setProgress((1 / totalSteps) * 100);

      for (let i = 0; i < 5; i++) {
        setCurrentVariation(i + 1);
        setProgressMessage(`Generating idea variation #${i + 1}`);
        const variationRes = await createIdeaVariation(projectId, ideaData.id);
        if (!variationRes.data.success) {
          setMessage({ type: 'error', text: variationRes.data.message || `Failed to generate variation #${i + 1}` });
          setStep('input');
          return;
        }
        ideaData = variationRes.data.data;
        setIdea(ideaData);
        setProgress(((i + 2) / totalSteps) * 100);
      }

      setProgress(100);
      setStep('results');
      if (onIdeaCreated) onIdeaCreated(ideaData);
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to expand idea' });
      setStep('input');
    }
  };

  const handleContinue = async () => {
    if (!idea) return;
    setStep('generating');
    setMessage(null);

    const existing = (idea.variations || []).length;
    const totalSteps = 5;
    setCurrentVariation(existing + 1);
    setProgress((existing / totalSteps) * 100);

    let ideaData = idea;
    try {
      for (let i = 0; i < 5 - existing; i++) {
        const variationNumber = existing + i + 1;
        setCurrentVariation(variationNumber);
        setProgressMessage(`Generating idea variation #${variationNumber}`);
        const variationRes = await createIdeaVariation(projectId, ideaData.id);
        if (!variationRes.data.success) {
          setMessage({ type: 'error', text: variationRes.data.message || `Failed to generate variation #${variationNumber}` });
          setStep('continue');
          return;
        }
        ideaData = variationRes.data.data;
        setIdea(ideaData);
        setProgress(((existing + i + 1) / totalSteps) * 100);
      }

      setProgress(100);
      setStep('results');
      if (onIdeaCreated) onIdeaCreated(ideaData);
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to continue idea' });
      setStep('continue');
    }
  };

  const handleDeleteIdea = async () => {
    if (!idea) return;
    setMessage(null);
    try {
      const res = await deleteIdea(projectId, idea.id);
      if (res.data.success) {
        onClose();
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to delete idea' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to delete idea' });
    }
  };

  const handleMakeCollection = async (variationId) => {
    setMessage(null);
    try {
      const res = await makeIdeaCollection(projectId, idea.id, { variationId });
      if (res.data.success) {
        if (onCollectionCreated) onCollectionCreated(res.data.data);
      } else {
        setMessage({ type: 'error', text: res.data.message || 'Failed to create collection' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: error?.response?.data?.message || 'Failed to create collection' });
    }
  };

  const toggleVariation = (id) => {
    setOpenVariationIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const renderContinue = () => (
    <div className="py-8 text-center">
      {message && (
        <div className="mb-4">
          <Message type={message.type} onClose={() => setMessage(null)}>
            {message.text}
          </Message>
        </div>
      )}
      <p className="text-lg text-gray-700 dark:text-gray-300 mb-6">The idea has not yet been fully realized. Continue?</p>
      <div className="flex justify-center gap-4">
        <ButtonOutline onClick={handleContinue}>Continue</ButtonOutline>
        <ButtonOutline onClick={handleDeleteIdea} color="red">Delete Idea</ButtonOutline>
      </div>
    </div>
  );

  const renderInput = () => (
    <div>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <TextArea
        label="Your Idea"
        name="ideaPrompt"
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
        rows={6}
        placeholder="Describe your idea..."
      />
      <div className="flex justify-end">
        <ButtonOutline onClick={handleExpandIdea}>Expand Idea</ButtonOutline>
      </div>
    </div>
  );

  const renderGenerating = () => (
    <div className="py-12 text-center">
      {message && (
        <div className="mb-4">
          <Message type={message.type} onClose={() => setMessage(null)}>
            {message.text}
          </Message>
        </div>
      )}
      <div className="max-w-xl mx-auto">
        <ProgressBar progress={progress} message={progressMessage} />
        <div className="mt-4 text-left">
          <List inModal={true}>
            <Item>
              <div className="flex items-center gap-2 min-w-0">
                {idea?.title ? (
                  <Icon name="check" className="w-4 h-4 text-green-600 dark:text-green-400 shrink-0" />
                ) : (
                  <Icon name="progress_activity" spin className="w-4 h-4 text-primary-600 dark:text-primary-400 shrink-0" />
                )}
                <span className={(idea?.title ? 'truncate' : 'truncate opacity-50') + ' pl-2'}>
                  {idea?.title ? `${idea.title} created` : 'Thinking about your idea...'}
                </span>
              </div>
            </Item>
            {[1, 2, 3, 4, 5].map((n) => {
              const completed = (idea?.variations || []).length >= n;
              const variation = idea?.variations?.[n - 1];
              const isPending = currentVariation === n;
              return (
                <Item key={n}>
                  <div className="flex items-center gap-2 min-w-0">
                    {completed ? (
                      <Icon name="check" className="w-4 h-4 text-green-600 dark:text-green-400 shrink-0" />
                    ) : (
                      <Icon name="progress_activity" spin className="w-4 h-4 text-primary-600 dark:text-primary-400 shrink-0" />
                    )}
                    <span className={(completed ? 'truncate' : 'truncate opacity-50') + ' pl-2'}>
                      {completed
                        ? `Idea Variation "${variation?.title || ''}" has been created`
                        : isPending
                        ? `Thinking about idea variation #${n}`
                        : `Waiting for idea variation #${n}`}
                    </span>
                  </div>
                </Item>
              );
            })}
          </List>
        </div>
      </div>
    </div>
  );

  const renderVariationContent = (v) => {
    const projectAnswers = v.parsed?.project?.answers || [];
    const allAnswers = v.parsed?.artworks?.answers || [];
    const answersByItem = {};
    allAnswers.forEach((a) => {
      const q = itemQuestionById[a.id];
      if (!q) return;
      const list = answersByItem[q.itemId] || [];
      list.push(a);
      answersByItem[q.itemId] = list;
    });

    const artworkItems = items.filter((i) => answersByItem[i.id]?.length > 0);

    return (
      <div className="p-3 mt-2">
        {projectAnswers.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">No project answers.</p>
        ) : (
          <div className="mb-4">
            <h4 className="font-medium mb-2">Project Questions</h4>
            <List inModal={true}>
              {projectAnswers.map((a) => {
                const q = projectQuestionById[a.id];
                return (
                  <Item key={a.id}>
                    <div>
                      <p className="text-xs text-gray-500 dark:text-gray-400">Question: {q || 'Unknown'}</p>
                      <p className="text-sm font-medium">Answer: {a.answer}</p>
                    </div>
                  </Item>
                );
              })}
            </List>
          </div>
        )}
        {artworkItems.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No artwork answers.</p>
        ) : (artworkItems.map((item) => {
              const itemAnswers = answersByItem[item.id];
              return (
                <div key={item.id} className="mb-4">
                  <h4 className="font-medium mb-2">{item.title}</h4>
                  <div className="w-full flex flex-col md:flex-row gap-4">
                    <div className="w-full md:w-60 shrink-0">
                      <Carousel
                        images={item.thumbnails || []}
                        alt={item.title}
                        singleImage
                        infiniteScroll
                        placeholder="No Previews"
                        imageClassName="!max-w-[240px] !max-h-[240px] object-contain"
                      />
                    </div>
                    <div className="flex-1 min-w-0">
                      <List inModal={true}>
                        {itemAnswers.map((a) => {
                          const q = itemQuestionById[a.id];
                          return (
                            <Item key={a.id}>
                              <div>
                                <p className="text-xs text-gray-500 dark:text-gray-400">Question: {q?.question || 'Unknown'}</p>
                                <p className="text-sm font-medium">Answer: {a.answer}</p>
                              </div>
                            </Item>
                          );
                        })}
                      </List>
                    </div>
                  </div>
                </div>
              );
            }))}
        <div className="flex justify-end mt-4">
          <ButtonOutline onClick={() => handleMakeCollection(v.id)}>Make Collection</ButtonOutline>
        </div>
      </div>
    );
  };

  const renderResults = () => (
    <div>
      {message && (
        <Message type={message.type} onClose={() => setMessage(null)}>
          {message.text}
        </Message>
      )}
      <div>
        <h3 className="text-lg font-medium mb-2">Variations</h3>
        <div className="space-y-2">
          {parsedVariations.map((v) => {
            const isOpen = openVariationIds.has(v.id);
            return (
              <div key={v.id} className="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
                <button
                  type="button"
                  onClick={() => toggleVariation(v.id)}
                  className="w-full flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700/50 hover:bg-gray-100 dark:hover:bg-gray-700 transition text-left"
                >
                  <span className="font-medium">{v.title}</span>
                  <Icon name={isOpen ? 'expand_less' : 'expand_more'} />
                </button>
                {isOpen && renderVariationContent(v)}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );

  const getTitle = () => {
    if (step === 'input') return 'New Idea';
    if (step === 'generating') return 'Expanding Idea';
    if (step === 'continue') return 'Continue Idea';
    return idea?.title || 'Idea';
  };

  const getContent = () => {
    if (step === 'loading') return (
      <div className="py-8 text-center">
        <Icon name="progress_activity" spin className="w-8 h-8 mx-auto" />
      </div>
    );
    if (step === 'input') return renderInput();
    if (step === 'generating') return renderGenerating();
    if (step === 'continue') return renderContinue();
    return renderResults();
  };

  return (
    <Modal title={getTitle()} onClose={onClose} className="max-w-3xl w-full">
      {getContent()}
    </Modal>
  );
}
