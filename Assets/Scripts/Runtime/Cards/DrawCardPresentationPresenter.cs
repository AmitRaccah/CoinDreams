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

        public float Present(AuthoritativeDrawResult result, int multiplier)
        {
            if (result == null || !result.IsSuccess)
            {
                return Play(failedDrawFeedbacks);
            }

            Sprite? sprite = ResolveSprite(result.DrawnCardId);
            SetCardSprite(sprite != null ? sprite : missingCardSprite);

            DisplayCardLabel(result.DrawnCardId, multiplier);

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

        // Picks the first reward effect on the card and renders the
        // multiplied amount + a short label (COINS / ENERGY / SHIELDS) so the
        // player sees what they actually receive rather than the card's base
        // value. Falls back to the raw card ID for cards without effects or
        // for the LaunchSteal effect (no numeric amount to show).
        //
        // TMP_Text.SetText(string, float) is zero-allocation — TMP parses the
        // format string and writes the number into its internal char buffer
        // without producing a managed string per call.
        private void DisplayCardLabel(string cardId, int multiplier)
        {
            if (cardLabel == null) return;

            int safeMultiplier = multiplier > 0 ? multiplier : 1;
            CardDefinitionSO? definition = FindDefinition(cardId);
            RewardEffectConfig? effect = ResolvePrimaryEffect(definition);

            if (effect == null)
            {
                cardLabel.SetText(cardId);
                return;
            }

            int multipliedAmount = effect.IntValue * safeMultiplier;

            switch (effect.EffectType)
            {
                case RewardEffectType.AddCoins:
                    cardLabel.SetText("{0:0} COINS", multipliedAmount);
                    break;
                case RewardEffectType.AddEnergy:
                    cardLabel.SetText("{0:0} ENERGY", multipliedAmount);
                    break;
                case RewardEffectType.AddShields:
                    cardLabel.SetText("{0:0} SHIELDS", multipliedAmount);
                    break;
                default:
                    cardLabel.SetText(cardId);
                    break;
            }
        }

        private CardDefinitionSO? FindDefinition(string cardId)
        {
            if (cards == null || string.IsNullOrWhiteSpace(cardId))
            {
                return null;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                CardDefinitionSO? definition = cards[i];
                if (definition == null) continue;
                if (string.Equals(definition.CardId, cardId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }
            return null;
        }

        private static RewardEffectConfig? ResolvePrimaryEffect(CardDefinitionSO? definition)
        {
            if (definition == null) return null;
            List<RewardEffectConfig> configs = definition.EffectConfigs;
            if (configs == null) return null;
            for (int i = 0; i < configs.Count; i++)
            {
                RewardEffectConfig config = configs[i];
                if (config == null) continue;
                return config;
            }
            return null;
        }

        private Sprite? ResolveSprite(string cardId)
        {
            CardDefinitionSO? definition = FindDefinition(cardId);
            return definition != null ? definition.CardSprite : null;
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
