using UnityEngine;
using FreeFly.Core;

namespace FreeFly;

internal sealed class TeleportMenuView
{
    private readonly TeleportDestinationService _destinations;
    private readonly TeleportExecutor _executor;
    private GameObject? _inputBlockerObject;
    private FreeFlyMenuWindow? _inputBlockerWindow;
    private GUIStyle? _boxStyle;
    private GUIStyle? _labelStyle;
    private GUIStyle? _buttonStyle;
    private GUISkin? _styleSkin;
    private Vector2 _scrollPosition;
    private int _selectedTarget;

    public TeleportMenuView(TeleportDestinationService destinations, TeleportExecutor executor)
    {
        _destinations = destinations;
        _executor = executor;
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

        string? selectedId = _selectedTarget >= 0 && _selectedTarget < _destinations.Options.Count
            ? _destinations.Options[_selectedTarget].Id : null;
        _destinations.RefreshIfNeeded();
        bool selectionLost = selectedId != null;
        if (selectedId != null)
        {
            for (int i = 0; i < _destinations.Options.Count; i++)
            {
                if (_destinations.Options[i].Id == selectedId)
                {
                    _selectedTarget = i;
                    selectionLost = false;
                    break;
                }
            }
        }
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
        // Do not turn a stale player selection into a different action during a refresh.
        if (selectionLost)
            return;
        if (input.Secondary)
            Execute(_selectedTarget, TeleportAction.Secondary);
        else if (input.Confirm)
            Execute(_selectedTarget, TeleportAction.Primary);
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
        GUILayout.Label("Select a destination. Bringing players skips dead teammates; teleporting to them uses their corpse position.", _labelStyle);
        GUILayout.Space(12f);

        if (_destinations.Options.Count == 0)
        {
            GUILayout.Label("No teleport destination is available.", _labelStyle);
        }
        else
        {
            Rect viewport = GUILayoutUtility.GetRect(0f, 10000f, 0f, 10000f, GUILayout.ExpandHeight(true));
            bool pointerInViewport = viewport.Contains(Event.current.mousePosition);
            Rect content = new(0f, 0f, Mathf.Max(0f, viewport.width - 20f), _destinations.Options.Count * 58f);
            _scrollPosition = GUI.BeginScrollView(viewport, _scrollPosition, content);
            for (int i = 0; i < _destinations.Options.Count; i++)
            {
                TeleportMenuEntry option = _destinations.Options[i];
                string status = !option.Enabled ? " [Generating...]"
                    : option.Kind == TeleportEntryKind.BringAll && !_executor.CanMoveOtherPlayers ? " [Host only]"
                    : !TeleportExecutor.IsSupported ? " [Unavailable]" : string.Empty;
                string label = $"{(i == _selectedTarget ? "> " : "  ")}{option.Label}{status}";
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && _executor.IsActionAvailable(option, TeleportAction.Primary) && IsOpen;
                Rect row = new(0f, i * 58f, content.width, 52f);
                bool secondary = GUI.enabled && _executor.IsActionAvailable(option, TeleportAction.Secondary) &&
                                 pointerInViewport &&
                                 Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                                 row.Contains(Event.current.mousePosition);
                // IMGUI buttons may accept non-left mouse buttons. Consume both edges
                // before drawing so right-click can never also perform the primary action.
                bool nonPrimaryMouse = (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) &&
                                       Event.current.button != 0 && pointerInViewport && row.Contains(Event.current.mousePosition);
                if (nonPrimaryMouse)
                    Event.current.Use();
                bool primary = GUI.Button(row, label, _buttonStyle);
                if (secondary || primary)
                {
                    Execute(i, secondary ? TeleportAction.Secondary : TeleportAction.Primary);
                }
                GUI.enabled = wasEnabled;
            }
            GUI.EndScrollView();
        }

        string controls = "Up/Down or D-pad: select   Left-click/Enter/A: confirm   Escape/B: cancel\n" +
            (_executor.CanMoveOtherPlayers
                ? "Right-click/X: bring player here / send everyone to location"
                : "Bringing players and group teleports: host only");
        GUILayout.Label(controls, _labelStyle);
        GUILayout.EndArea();
    }

    private void Execute(int index, TeleportAction action)
    {
        _selectedTarget = index;
        if (index >= 0 && index < _destinations.Options.Count && _executor.TryExecute(_destinations.Options[index], action))
            Close();
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
