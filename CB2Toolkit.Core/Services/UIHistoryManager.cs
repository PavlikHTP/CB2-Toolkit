using System.Collections.ObjectModel;
using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Services;

public class UIServiceManager
{
    private readonly ObservableCollection<UIElementModel> _elements;
    private readonly Stack<List<UIElementModel>> _undoStack = new();
    private readonly Stack<List<UIElementModel>> _redoStack = new();
    private const int MaxSteps = 999;
    private bool _isOperating;
    public bool HasUnsavedChanges { get; private set; }

    public UIServiceManager(ObservableCollection<UIElementModel> elements)
    {
        _elements = elements;
    }

    public void MarkSaved() => HasUnsavedChanges = false;

    public void SaveState()
    {
        if (_isOperating) return;

        HasUnsavedChanges = true;
        var snapshot = _elements.Select(CloneElement).ToList();
        _undoStack.Push(snapshot);
        _redoStack.Clear();
        
        if (_undoStack.Count > MaxSteps)
        {
            var temp = _undoStack.ToList();
            
            temp.RemoveAt(temp.Count - 1); 
            
            _undoStack.Clear();
            
            for (int i = temp.Count - 1; i >= 0; i--)
            {
                _undoStack.Push(temp[i]);
            }
        }
    }

    public void Undo(Action<UIElementModel> selectElementCallback)
    {
        if (_undoStack.Count == 0) return;

        _isOperating = true;
        try
        {
            var currentSnapshot = _elements.Select(CloneElement).ToList();
            _redoStack.Push(currentSnapshot);

            var previousState = _undoStack.Pop();
            RestoreState(previousState, selectElementCallback);
        }
        finally
        {
            _isOperating = false;
        }
    }

    public void Redo(Action<UIElementModel> selectElementCallback)
    {
        if (_redoStack.Count == 0) return;

        _isOperating = true;
        try
        {
            var currentSnapshot = _elements.Select(CloneElement).ToList();
            _undoStack.Push(currentSnapshot);

            var nextState = _redoStack.Pop();
            RestoreState(nextState, selectElementCallback);
        }
        finally
        {
            _isOperating = false;
        }
    }

    private void RestoreState(List<UIElementModel> state, Action<UIElementModel> selectElementCallback)
    {
        _elements.Clear();
        foreach (var el in state)
        {
            _elements.Add(el);
        }

        if (_elements.Count > 0)
        {
            selectElementCallback?.Invoke(_elements[0]);
        }
    }

    public UIElementModel CloneElement(UIElementModel source)
    {
        return new UIElementModel
        {
            Name = source.Name,
            Type = source.Type,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
            R = source.R,
            G = source.G,
            B = source.B,
            Opacity = source.Opacity,
            Text = source.Text,
            MiscValue = source.MiscValue,
            GroupId = source.GroupId,
            Font = source.Font
        };
    }
}