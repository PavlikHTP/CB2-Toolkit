using CB2Toolkit.Core.Models;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace CB2Toolkit.Core.Services;

public class EditorHistoryManager
{
    private readonly TextEditor _editor;
    private readonly Dictionary<string, (Stack<TextChangeHistory> Undo, Stack<TextChangeHistory> Redo)> _filesHistory = new();
    private Stack<TextChangeHistory> _customUndoStack = new();
    private Stack<TextChangeHistory> _customRedoStack = new();
    private bool _isUndoRedoOperating;

    public bool IsSuspended { get; set; }

    public EditorHistoryManager(TextEditor editor)
    {
        _editor = editor;
        _editor.Document.Changing += Document_Changing;
    }

    private void Document_Changing(object sender, DocumentChangeEventArgs e)
    {
        if (_isUndoRedoOperating || IsSuspended) return;

        var change = new TextChangeHistory
        {
            Offset = e.Offset,
            RemovedText = e.RemovalLength > 0 ? _editor.Document.GetText(e.Offset, e.RemovalLength) : string.Empty,
            AddedText = e.InsertedText?.Text ?? string.Empty
        };

        _customUndoStack.Push(change);
        _customRedoStack.Clear();
    }

    public void Undo()
    {
        if (_customUndoStack.Count == 0) return;

        var change = _customUndoStack.Pop();
        _isUndoRedoOperating = true;

        try
        {
            _editor.Document.Replace(change.Offset, change.AddedText.Length, change.RemovedText);
            _editor.CaretOffset = change.Offset + change.RemovedText.Length;
            _customRedoStack.Push(change);
        }
        finally
        {
            _isUndoRedoOperating = false;
        }
    }

    public void Redo()
    {
        if (_customRedoStack.Count == 0) return;

        var change = _customRedoStack.Pop();
        _isUndoRedoOperating = true;

        try
        {
            _editor.Document.Replace(change.Offset, change.RemovedText.Length, change.AddedText);
            _editor.CaretOffset = change.Offset + change.AddedText.Length;
            _customUndoStack.Push(change);
        }
        finally
        {
            _isUndoRedoOperating = false;
        }
    }

    public void SwitchFile(string? oldPath, string? newPath)
    {
        if (!string.IsNullOrEmpty(oldPath))
        {
            _filesHistory[oldPath] = (_customUndoStack, _customRedoStack);
        }

        if (!string.IsNullOrEmpty(newPath) && _filesHistory.TryGetValue(newPath, out var savedHistory))
        {
            _customUndoStack = savedHistory.Undo;
            _customRedoStack = savedHistory.Redo;
        }
        else
        {
            _customUndoStack = new Stack<TextChangeHistory>();
            _customRedoStack = new Stack<TextChangeHistory>();
        }
    }

    public void RenameFile(string oldPath, string newPath)
    {
        if (_filesHistory.TryGetValue(oldPath, out var history))
        {
            _filesHistory[newPath] = history;
            _filesHistory.Remove(oldPath);
        }
    }

    public void DeleteFile(string path)
    {
        _filesHistory.Remove(path);
    }
}