#nullable enable

using System;
using System.Collections.Generic;
using Game.Config.Cards;
using Game.Domain.Cards;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class DrawCardPresentationPresenter : MonoBehaviour, IDrawCardPresentation
    {
        [Header("Feel")]
        [SerializeField] private MMF_Player? drawFeedbacks;
        [SerializeField] private MMF_Player? revealFeedbacks;
        [SerializeField] private MMF_Player? failedDrawFeedbacks;

        [Header("Card Visual")]
        [SerializeField] private Image? cardImage;
        [SerializeField] private TMP_Text? cardLabel;
        [SerializeField] private CardDeckSO? deckConfig;
        [SerializeField] private Sprite? placeholderSprite;
        [SerializeField] private Sprite? missingCardSprite;

        private List<CardDefinitionSO>? cards;

        private void Awake()
        {
            CacheReferences();
            ResetCardVisual();
        }

        public float BeginDraw()
        {
            ResetCardVisual();
            return Play(drawFeedbacks);
        }

        public float Present(AuthoritativeDrawResult result)
        {
            if (result == null || !result.IsSuccess)
            {
                return Play(failedDrawFeedbacks);
            }

            Sprite? sprite = ResolveSprite(result.DrawnCardId);
            SetCardSprite(sprite != null ? sprite : missingCardSprite);

            if (cardLabel != null)
            {
                cardLabel.SetText(result.DrawnCardId);
            }

            return Play(revealFeedbacks);
        }

        private void CacheReferences()
        {
            if (drawFeedbacks == null)
            {
                drawFeedbacks = GetComponentInParent<MMF_Player>();
            }

            if (cards == null && deckConfig != null)
            {
                cards = deckConfig.Cards;
            }
        }

        private void ResetCardVisual()
        {
            SetCardSprite(placeholderSprite);

            if (cardLabel != null)
            {
                cardLabel.SetText(string.Empty);
            }
        }

        private void SetCardSprite(Sprite? sprite)
        {
            if (cardImage == null)
            {
                return;
            }

            cardImage.sprite = sprite;
            cardImage.enabled = sprite != null;
        }

        private Sprite? ResolveSprite(string cardId)
        {
            if (cards == null || string.IsNullOrWhiteSpace(cardId))
            {
                return null;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                CardDefinitionSO? definition = cards[i];
                if (definition == null)
                {
                    continue;
                }

                if (string.Equals(definition.CardId, cardId, StringComparison.Ordinal))
                {
                    return definition.CardSprite;
                }
            }

            return null;
        }

        private static float Play(MMF_Player? feedbacks)
        {
            if (feedbacks == null
                || feedbacks.FeedbacksList == null
                || feedbacks.FeedbacksList.Count == 0)
            {
                return 0f;
            }

            feedbacks.PlayFeedbacks();
            return Mathf.Max(0f, feedbacks.TotalDuration);
        }
    }
}
