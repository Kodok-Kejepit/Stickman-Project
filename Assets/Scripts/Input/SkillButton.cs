using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stickman.Input
{
    /// <summary>Instant-trigger multitouch skill button with dirty-checked cooldown overlay.</summary>
    public sealed class SkillButton : MonoBehaviour, IPointerDownHandler
    {
        Image _cooldownFill;
        Text _cooldownText;
        int _lastDisplaySec = -1;

        public event Action OnPressed;

        public void Initialize(Image cooldownFill, Text cooldownText)
        {
            _cooldownFill = cooldownFill;
            _cooldownText = cooldownText;
            if (_cooldownFill != null) _cooldownFill.fillAmount = 0f;
            if (_cooldownText != null) _cooldownText.text = string.Empty;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnPressed?.Invoke();
        }

        public void UpdateCooldown(float ratio, float remainingSeconds)
        {
            if (_cooldownFill != null)
                _cooldownFill.fillAmount = Mathf.Clamp01(ratio);

            if (_cooldownText != null)
            {
                int sec = remainingSeconds > 0.05f ? Mathf.CeilToInt(remainingSeconds) : 0;
                if (sec != _lastDisplaySec)
                {
                    _lastDisplaySec = sec;
                    _cooldownText.text = sec > 0 ? sec.ToString() : string.Empty;
                }
            }
        }
    }
}
