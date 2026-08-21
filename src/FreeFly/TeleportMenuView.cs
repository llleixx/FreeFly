using UnityEngine;

namespace FreeFly;

internal sealed class TeleportMenuView
{
    private readonly TeleportDestinationService _destinations;
    private GameObject? _inputBlockerObject;
    private FreeFlyMenuWindow? _inputBlockerWindow;
    private GUIStyle? _boxStyle;
    private GUIStyle? _labelStyle;
    private GUIStyle? _buttonStyle;
    private GUISkin? _styleSkin;
    private Vector2 _scrollPosition;
    private int _selectedTarget;

    public TeleportMenuView(TeleportDestinationService destinations)
    {
        _destinations = destinations;
    }

    public bool IsOpen { get; private set; }

    public void Open()
    {
        _destinations.RefreshIfNeeded(force: true);
        _selectedTarget = FreeFly.Core.FreeFlyInputRules.ClampSelection(
            _selectedTarget,
            _destinations.Options.Count);
        _scrollPosition = Vector2.zero;
        IsOpen = true;
        CreateInputBlocker();
    }

    public void Tick(FreeFlyInputSnapshot input)
    {
        if (!IsOpen)
            return;

        _destinations.RefreshIfNeeded();
        _selectedTarget = FreeFly.Core.FreeFlyInputRules.ClampSelection(
            _selectedTarget,
            _destinations.Options.Count);

        if (input.Cancel)
        {
            Close();
            return;
        }

        if (input.Up)
            _selectedTarget = Mathf.Max(0, _selectedTarget - 1);
        if (input.Down)
            _selectedTarget = Mathf.Min(Mathf.Max(0, _destinations.Options.Count - 1), _selectedTarget + 1);
        if (input.Confirm && _destinations.TryTeleport(_selectedTarget))
            Close();
    }

    public void Draw()
    {
        if (!IsOpen)
            return;

        EnsureStyles();
        float width = Mathf.Min(720f, Screen.width - 40f);
        float height = Mathf.Min(620f, Screen.height - 40f);
        Rect area = new((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(area, "FreeFly - Teleport", _boxStyle);
        GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 64f, area.width - 48f, area.height - 88f));
        GUILayout.Label("Select a destination. Dead teammates use their corpse position.", _labelStyle);
        GUILayout.Space(12f);

        if (_destinations.Options.Count == 0)
        {
            GUILayout.Label("No teleport destination is available.", _labelStyle);
        }
        else
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < _destinations.Options.Count; i++)
            {
                TeleportOption option = _destinations.Options[i];
                string status = option.Enabled ? string.Empty : " [Generating...]";
                string label = $"{(i == _selectedTarget ? "> " : "  ")}{option.Label}{status}";
                bool wasEnabled = GUI.enabled;
                GUI.enabled = option.Enabled;
                if (GUILayout.Button(label, _buttonStyle, GUILayout.Height(52f)))
                {
                    _selectedTarget = i;
                    if (_destinations.TryTeleport(_selectedTarget))
                        Close();
                }
                GUI.enabled = wasEnabled;
            }
            GUILayout.EndScrollView();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Up/Down or D-pad: select    Enter/A: teleport    Escape/B: cancel", _labelStyle);
        GUILayout.EndArea();
    }

    public void Close()
    {
        IsOpen = false;
        DestroyInputBlocker();
    }

    public void Shutdown() => Close();

    private void EnsureStyles()
    {
        if (_styleSkin == GUI.skin && _boxStyle != null && _labelStyle != null && _buttonStyle != null)
            return;

        _styleSkin = GUI.skin;
        _boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 28 };
        _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
        _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
    }

    private void CreateInputBlocker()
    {
        if (_inputBlockerObject != null)
            return;

        _inputBlockerObject = new GameObject("FreeFly Teleport Menu Input Blocker");
        _inputBlockerWindow = _inputBlockerObject.AddComponent<FreeFlyMenuWindow>();
        if (!MenuWindow.AllActiveWindows.Contains(_inputBlockerWindow))
            MenuWindow.AllActiveWindows.Add(_inputBlockerWindow);
    }

    private void DestroyInputBlocker()
    {
        if (_inputBlockerWindow != null)
            MenuWindow.AllActiveWindows.Remove(_inputBlockerWindow);

        if (_inputBlockerObject != null)
            UnityEngine.Object.Destroy(_inputBlockerObject);

        _inputBlockerWindow = null;
        _inputBlockerObject = null;
    }
}
