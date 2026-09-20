using Godot;

namespace CombatSolver;

internal readonly record struct SolverActionBarState(bool Collapsed, bool Searching, bool ShowAdopt, bool ShowFreeze, bool AdviceOnly = false);

// Owns layout only. The overlay retains command bindings and capability checks.
internal sealed partial class SolverActionBar : VBoxContainer
{
    private readonly HFlowContainer _actions;
    private readonly Control _autoStart;
    private readonly HBoxContainer _memoryRow;
    private readonly Button _execute;
    private readonly Button _recalculate;
    private readonly Button _stop;
    private readonly Button _adopt;
    private readonly Button _freeze;
    private readonly Button _fullAuto;
    private readonly Control _memory;

    public SolverActionBar(Button execute, Button recalculate, Button stop, Button adopt, Button freeze,
        Button fullAuto, Control autoStart, Control memory, Button releaseMemory)
    {
        Name = "Footer";
        MouseFilter = MouseFilterEnum.Pass;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Sm);
        _execute = execute;
        _recalculate = recalculate;
        _stop = stop;
        _adopt = adopt;
        _freeze = freeze;
        _fullAuto = fullAuto;
        _memory = memory;
        _actions = CreateFlow("CombatActions");
        _autoStart = autoStart;
        HBoxContainer actionRow = new() { Name = "ActionRow", MouseFilter = MouseFilterEnum.Pass };
        actionRow.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Md);
        _actions.AddChild(fullAuto);
        _actions.AddChild(adopt);
        _actions.AddChild(freeze);
        _actions.AddChild(execute);
        _actions.AddChild(recalculate);
        _actions.AddChild(stop);
        actionRow.AddChild(_actions);
        actionRow.AddChild(autoStart);
        AddChild(actionRow);
        _memoryRow = new HBoxContainer { Name = "MemoryRow", MouseFilter = MouseFilterEnum.Pass };
        _memoryRow.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Sm);
        _memoryRow.AddChild(memory);
        _memoryRow.AddChild(releaseMemory);
        AddChild(_memoryRow);
    }

    public void Refresh(SolverActionBarState state)
    {
        _recalculate.Visible = !state.Searching || state.AdviceOnly;
        _stop.Visible = state.Searching;
        _adopt.Visible = state.ShowAdopt && !state.AdviceOnly;
        _freeze.Visible = !state.Searching && state.ShowFreeze && !state.AdviceOnly;
        _execute.Visible = !state.Searching && !state.AdviceOnly;
        _fullAuto.Visible = !state.AdviceOnly;
        _autoStart.Visible = !state.Collapsed && !state.AdviceOnly;
        _memoryRow.Visible = !state.Collapsed;
        _memory.Visible = !state.Collapsed;
    }

    internal void AssertLayoutForTesting()
    {
        bool originalAdoptDisabled = _adopt.Disabled;
        bool originalExecuteDisabled = _execute.Disabled;
        try
        {
            foreach (bool collapsed in new[] { false, true })
            foreach (bool searching in new[] { false, true })
            foreach (bool adopt in new[] { false, true })
            foreach (bool freeze in new[] { false, true })
            foreach (bool adoptDisabled in new[] { false, true })
            foreach (bool executeDisabled in new[] { false, true })
            foreach (bool adviceOnly in new[] { false, true })
            {
                _adopt.Disabled = adoptDisabled;
                _execute.Disabled = executeDisabled;
                Refresh(new SolverActionBarState(collapsed, searching, adopt, freeze, adviceOnly));
                if (_stop.Visible != searching || _recalculate.Visible != (!searching || adviceOnly)
                    || _adopt.Visible != (adopt && !adviceOnly)
                    || _adopt.Disabled != adoptDisabled
                    || _execute.Disabled != executeDisabled
                    || _freeze.Visible != (!searching && freeze && !adviceOnly)
                    || _execute.Visible != (!searching && !adviceOnly)
                    || _fullAuto.Visible == adviceOnly
                    || _memory.Visible == collapsed || _autoStart.Visible != (!collapsed && !adviceOnly)
                    || _memoryRow.Visible == collapsed
                    || _fullAuto.GetParent() != _actions || _fullAuto.GetIndex() != 0
                    || _adopt.GetIndex() != 1 || _freeze.GetIndex() != 2)
                    throw new InvalidOperationException("Action bar layout state did not match its display snapshot.");
            }
        }
        finally
        {
            _adopt.Disabled = originalAdoptDisabled;
            _execute.Disabled = originalExecuteDisabled;
        }
    }

    private static HFlowContainer CreateFlow(string name)
    {
        HFlowContainer flow = new()
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        flow.AddThemeConstantOverride("h_separation", SolverUiTokens.Spacing.Md);
        flow.AddThemeConstantOverride("v_separation", SolverUiTokens.Spacing.Xs);
        return flow;
    }
}
