using System.Collections;
using TMPro;
using UnityEngine;

namespace FreeFly;

internal sealed class FreeFlyNotification : MonoBehaviour
{
    private TextMeshProUGUI? _label;
    private Material? _labelMaterial;
    private CanvasGroup? _group;
    private Coroutine? _routine;

    public void Show(string message)
    {
        if (!EnsureLabel() || _label == null || _group == null)
            return;

        _label.text = message;
        _label.transform.SetAsLastSibling();
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    private bool EnsureLabel()
    {
        if (_label != null && _group != null)
            return true;
        if (GUIManager.instance == null || GUIManager.instance.hudCanvas == null)
            return false;

        GameObject labelObject = new(
            "FreeFly Notification",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(GUIManager.instance.hudCanvas.transform, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -96f);
        rect.sizeDelta = new Vector2(520f, 52f);

        _group = labelObject.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _label = labelObject.GetComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontSize = 30f;
        TextMeshProUGUI? fontSource = GUIManager.instance.interactNameText ??
                                          GUIManager.instance.itemPromptMain;
        if (fontSource != null && fontSource.font != null)
        {
            _label.font = fontSource.font;
            _label.fontStyle = fontSource.fontStyle;
            if (fontSource.fontSharedMaterial != null)
            {
                _labelMaterial = new Material(fontSource.fontSharedMaterial);
                _label.fontSharedMaterial = _labelMaterial;
            }
        }

        _label.faceColor = Color.white;
        _label.outlineColor = Color.black;
        _label.outlineWidth = 0.08f;
        _label.raycastTarget = false;
        return true;
    }

    private IEnumerator ShowRoutine()
    {
        if (_group == null)
            yield break;

        _group.alpha = 1f;
        yield return new WaitForSecondsRealtime(1.25f);

        const float fadeDuration = 0.25f;
        float elapsed = 0f;
        while (elapsed < fadeDuration && _group != null)
        {
            elapsed += Time.unscaledDeltaTime;
            _group.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        if (_group != null)
            _group.alpha = 0f;
        _routine = null;
    }

    private void OnDestroy()
    {
        if (_label != null)
            Destroy(_label.gameObject);
        if (_labelMaterial != null)
            Destroy(_labelMaterial);
    }
}
