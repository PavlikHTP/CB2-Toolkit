using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CB2Toolkit.CodeEditor.Models.Enums;
using CB2Toolkit.CodeEditor.Utils;
using CB2Toolkit.CodeEditor.Views;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Enums;
using CB2Toolkit.Core.Models.Settings;
using CB2Toolkit.Core.Services;
using CB2Toolkit.Core.Utilities.Extensions;
using Microsoft.Win32;
using Fonts = CB2Toolkit.Core.Models.Enums.Fonts;

namespace CB2Toolkit.UIEditor.Views;

public partial class UIEditorView : LifecycleUserControl
{
    private bool _isDragging;
    private bool _dragThresholdExceeded;
    private Point _dragStartPoint;
    private UIElementModel _draggedElement;

    private readonly Dictionary<UIElementModel, (double X, double Y, double Width, double Height)>
        _draggedElementsData = new();

    private readonly Dictionary<UIElementModel, (double X, double Y, double Width, double Height)>
        _resizedElementsData = new();

    private (double X, double Y, double Width, double Height) _initialGroupBounds;
    private UIElementModel _resizingModel;
    private Point _lastMousePosition;
    private bool _isUpdatingSelection;
    private Point _contextMenuSpawnPoint;
    private readonly UIServiceManager _historyManager;
    private bool _isPickingColor;
    private double _zoomPercent = 100;
    private double _panX;
    private double _panY;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _panStartX;
    private double _panStartY;
    private bool _isUpdatingScrollbar;
    private bool _squareAspectLock;

    private bool _isMarqueeSelecting;
    private Point _marqueeStartPoint;
    public ObservableCollection<UIElementModel> Elements { get; set; } = new();

    public Array FontsList => Enum.GetValues(typeof(Fonts));

    public UIEditorView()
    {
        InitializeComponent();
        ElementsList.ItemsSource = Elements;
        VisualPreviewContainer.ItemsSource = Elements;
        _historyManager = new UIServiceManager(Elements);

        CollectionViewSource.GetDefaultView(Elements).GroupDescriptions.Add(new PropertyGroupDescription("GroupId"));

        PathTextBoxHelper.Attach(CompilePathInput);
        PathTextBoxHelper.Attach(BackgroundPathInput);
    }

    protected override Task OnViewLoadedAsync()
    {
        var settings = SettingsService.Instance.Current;
        if (!string.IsNullOrEmpty(settings.UIEditorCompilePath))
        {
            CompilePathInput.Text = settings.UIEditorCompilePath;
            FontService.Instance.Initialize(settings.UIEditorCompilePath);
        }

        if (!string.IsNullOrEmpty(settings.UIEditorBackgroundPath))
        {
            BackgroundPathInput.Text = settings.UIEditorBackgroundPath;
        }

        return Task.CompletedTask;
    }

    private void CompilePathInput_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveCompilePathSetting();
    }

    private void CompilePathInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveCompilePathSetting();
            Keyboard.ClearFocus();
        }
    }

    private void SaveCompilePathSetting()
    {
        SettingsService.Instance.Current.UIEditorCompilePath = CompilePathInput.Text.SanitizePath();
        FontService.Instance.Initialize(CompilePathInput.Text);
        _ = SettingsService.Instance.SaveAsync();
    }

    private void BackgroundPathInput_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveBackgroundPathSetting();
    }

    private void BackgroundPathInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveBackgroundPathSetting();
            Keyboard.ClearFocus();
        }
    }

    private void SaveBackgroundPathSetting()
    {
        SettingsService.Instance.Current.UIEditorBackgroundPath = BackgroundPathInput.Text.SanitizePath();
        _ = SettingsService.Instance.SaveAsync();
    }

    private void BackToMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_historyManager.HasUnsavedChanges)
        {
            var result = ModernMessageBox.Show(
                Window.GetWindow(this),
                "You have unsaved changes. Are you sure you want to go back?",
                "Unsaved Changes",
                ModernBoxType.Question);

            if (result.Result != ModernBoxResultType.Yes)
                return;
        }

        Window currentWindow = Window.GetWindow(this);
        if (currentWindow != null)
        {
            dynamic mainWindow = currentWindow;
            mainWindow.NavigateToMenu();
        }
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Project Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            CompilePathInput.Text = dialog.FolderName;
            SaveCompilePathSetting();
        }
    }

    private void BrowseCompilePath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Compile Output Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            CompilePathInput.Text = dialog.FolderName;
            SaveCompilePathSetting();
        }
    }

    private void ImportUI_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import UI Workspace",
            Filter = "UI Workspace (*.json)|*.json|All files (*.*)|*.*"
        };

        if (!string.IsNullOrWhiteSpace(CompilePathInput.Text) && Directory.Exists(CompilePathInput.Text))
        {
            dialog.InitialDirectory = CompilePathInput.Text;
        }

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string jsonString = File.ReadAllText(dialog.FileName);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                ObservableCollection<UIElementModel> loadedElements = null;

                try
                {
                    var workspace = JsonSerializer.Deserialize<UIWorkspaceData>(jsonString, options);
                    if (workspace?.Elements != null)
                    {
                        loadedElements = workspace.Elements;
                        var settings = SettingsService.Instance.Current;
                        settings.UIEditorRefWidth = workspace.RefWidth > 0 ? workspace.RefWidth : 1920;
                        settings.UIEditorRefHeight = workspace.RefHeight > 0 ? workspace.RefHeight : 1080;
                        _ = SettingsService.Instance.SaveAsync();
                    }
                }
                catch
                {
                    loadedElements = JsonSerializer.Deserialize<ObservableCollection<UIElementModel>>(jsonString, options);
                }

                if (loadedElements != null)
                {
                    Elements.Clear();
                    foreach (var el in loadedElements)
                    {
                        Elements.Add(el);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }

    private void ElementsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        PropertiesPanel.Visibility = ElementsList.SelectedItem != null ? Visibility.Visible : Visibility.Collapsed;

        if (ElementsList.SelectedItem is UIElementModel selected)
        {
            GroupPropertySection.Visibility =
                !string.IsNullOrEmpty(selected.GroupId) ? Visibility.Visible : Visibility.Collapsed;
            GroupNameTextBox.Text = selected.GroupId ?? "";
            UpdateHexTextBox(selected);
        }

        Dispatcher.InvokeAsync(UpdateSelectionBox);
    }

    private void RenameSelectedGroup(string newGroupName)
    {
        if (ElementsList.SelectedItem is UIElementModel selected && !string.IsNullOrEmpty(selected.GroupId))
        {
            RenameGroupById(selected.GroupId, newGroupName);
        }
    }

    private void RenameGroupById(string oldGroupId, string newGroupId)
    {
        if (string.IsNullOrEmpty(oldGroupId) || oldGroupId == newGroupId) return;

        _historyManager.SaveState();
        var groupItems = Elements.Where(el => el.GroupId == oldGroupId).ToList();
        foreach (var el in groupItems)
        {
            el.GroupId = newGroupId;
        }

        CollectionViewSource.GetDefaultView(Elements).Refresh();
    }

    private void GroupHeaderGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CollectionViewGroup group)
        {
            string groupId = group.Name?.ToString();
            if (string.IsNullOrEmpty(groupId)) return;

            if (e.ClickCount >= 2)
            {
                e.Handled = true;

                var result = ModernMessageBox.Show(
                    Window.GetWindow(this),
                    "Enter new group name:",
                    "Rename Group",
                    ModernBoxType.Input,
                    groupId
                );

                if (result != null && result.Result == ModernBoxResultType.OK &&
                    !string.IsNullOrWhiteSpace(result.InputText))
                {
                    RenameGroupById(groupId, result.InputText.Trim());
                }
            }
            else
            {
                e.Handled = true;
                ElementsList.UnselectAll();
                foreach (var el in Elements.Where(x => x.GroupId == groupId))
                    ElementsList.SelectedItems.Add(el);
            }
        }
    }

    private void GroupNameInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RenameSelectedGroup(GroupNameTextBox.Text);
            Keyboard.ClearFocus();
        }
    }

    private void GroupNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        RenameSelectedGroup(GroupNameTextBox.Text);
    }

    private void PreviewBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            PreviewBorder.CaptureMouse();
            _isPanning = true;
            _panStartPoint = e.GetPosition(PreviewBorder);
            _panStartX = _panX;
            _panStartY = _panY;
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left) return;

        // Detect clicks on SelectionBox Thumbs — Thumb handles MouseLeftButtonDown
        // which prevents PreviewWorkspace_MouseLeftButtonDown from firing.
        // Here we pre-select the element under the cursor before the Thumb drag starts.
        Point thumbPos = e.GetPosition(PreviewContentRoot);
        DependencyObject src = e.OriginalSource as DependencyObject;
        if (src != null)
        {
            DependencyObject walk = src;
            while (walk != null && walk != PreviewContentRoot)
            {
                if (walk is Thumb)
                {
                    UIElementModel model = PickElement(thumbPos);
                    if (model != null && !ElementsList.SelectedItems.Contains(model))
                    {
                        SelectElement(model, e);
                    }
                    return;
                }
                walk = VisualTreeHelper.GetParent(walk);
            }
        }

        if (e.OriginalSource == ElementsList ||
            e.OriginalSource is ScrollViewer)
        {
            ElementsList.UnselectAll();
        }
    }

    private void AddElement(ElementType type, string defaultName, string icon, double spawnX = 0.35,
        double spawnY = 0.35)
    {
        _historyManager.SaveState();
        double width = type == ElementType.Text ? 1.0 : 0.4;
        double height = type == ElementType.Text ? 1.0 : 0.2;

        double maxX = Math.Max(0.0, 1.0 - width);
        double maxY = Math.Max(0.0, 1.0 - height);

        var el = new UIElementModel
        {
            Name = $"{defaultName}{Elements.Count + 1}",
            Type = type,
            X = Math.Round(Math.Clamp(spawnX, 0.0, maxX), 3),
            Y = Math.Round(Math.Clamp(spawnY, 0.0, maxY), 3),
            Width = Math.Round(width, 3),
            Height = Math.Round(height, 3),
            R = 255,
            G = 255,
            B = 255,
            Opacity = 1.0,
            Text = type == ElementType.Text ? "SAMPLE TEXT NODE" : (type == ElementType.Image ? "ui_bg.webm" : ""),
            MiscValue = type == ElementType.ProgressBar ? "50" : "",
            Font = Fonts.FontDefault
        };
        Elements.Add(el);
        ElementsList.SelectedItem = el;
        Dispatcher.InvokeAsync(UpdateSelectionBox);
    }

    private void GetContextMenuSpawnPosition(out double normX, out double normY)
    {
        double cw = ContainerWidth;
        double ch = ContainerHeight;

        if (cw > 0 && ch > 0)
        {
            normX = Math.Clamp(_contextMenuSpawnPoint.X / cw, 0.0, 1.0);
            normY = Math.Clamp(_contextMenuSpawnPoint.Y / ch, 0.0, 1.0);
        }
        else
        {
            normX = 0.35;
            normY = 0.35;
        }
    }

    private void PreviewWorkspace_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuSpawnPoint = e.GetPosition(PreviewContentRoot);

        if (PreviewBorder.ContextMenu != null)
        {
            var selectedItems = ElementsList.SelectedItems.Cast<UIElementModel>().ToList();
            bool hasGroup = selectedItems.Any(x => !string.IsNullOrEmpty(x.GroupId));

            foreach (var item in PreviewBorder.ContextMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    if (menuItem.Header.ToString().Contains("Group Selected"))
                    {
                        menuItem.Visibility = (selectedItems.Count > 1 && !hasGroup)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }
                    else if (menuItem.Header.ToString().Contains("Ungroup Selected"))
                    {
                        menuItem.Visibility = hasGroup ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }
    }

    private void AddRect_Click(object sender, RoutedEventArgs e)
    {
        GetContextMenuSpawnPosition(out double x, out double y);
        AddElement(ElementType.Rect, "background", "🟦", x, y);
    }

    private void AddOval_Click(object sender, RoutedEventArgs e)
    {
        GetContextMenuSpawnPosition(out double x, out double y);
        AddElement(ElementType.Oval, "circleNode", "⭕", x, y);
    }

    private void AddText_Click(object sender, RoutedEventArgs e)
    {
        GetContextMenuSpawnPosition(out double x, out double y);
        AddElement(ElementType.Text, "titleText", "📝", x, y);
    }

    private void AddImage_Click(object sender, RoutedEventArgs e)
    {
        GetContextMenuSpawnPosition(out double x, out double y);
        AddElement(ElementType.Image, "textureNode", "🖼", x, y);
    }

    private void AddProgressBar_Click(object sender, RoutedEventArgs e)
    {
        GetContextMenuSpawnPosition(out double x, out double y);
        AddElement(ElementType.ProgressBar, "loadingBar", "📊", x, y);
    }

    private void DeleteElement_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedElement();
    }

    private void DeleteSelectedElement()
    {
        if (ElementsList.SelectedItems.Count > 0)
        {
            _isUpdatingSelection = true;
            try
            {
                _historyManager.SaveState();
                var selectedList = ElementsList.SelectedItems.Cast<UIElementModel>().ToList();
                ElementsList.UnselectAll();
                foreach (var model in selectedList)
                {
                    Elements.Remove(model);
                }
            }
            finally
            {
                _isUpdatingSelection = false;
                PropertiesPanel.Visibility = Visibility.Collapsed;
                Dispatcher.InvokeAsync(UpdateSelectionBox);
            }
        }
    }

    private void PreviewWorkspace_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        PreviewBorder.Focus();

        if (_isPickingColor)
        {
            var hit = VisualTreeHelper.HitTest(PreviewContentRoot, e.GetPosition(PreviewContentRoot));
            if (hit != null)
            {
                DependencyObject dObj = hit.VisualHit;
                while (dObj != null && dObj != PreviewContentRoot)
                {
                    if (dObj is FrameworkElement fe && fe.DataContext is UIElementModel targetModel)
                    {
                        _historyManager.SaveState();
                        var selectedList = ElementsList.SelectedItems.Cast<UIElementModel>().ToList();
                        foreach (var el in selectedList)
                        {
                            el.R = targetModel.R;
                            el.G = targetModel.G;
                            el.B = targetModel.B;
                        }

                        break;
                    }

                    dObj = VisualTreeHelper.GetParent(dObj);
                }
            }

            _isPickingColor = false;
            Mouse.OverrideCursor = null;
            e.Handled = true;
            return;
        }

        Point clickPos = e.GetPosition(PreviewContentRoot);
        UIElementModel hitModel = PickElement(clickPos);

        if (hitModel != null)
        {
            SelectElement(hitModel, e);
            if (e.ClickCount < 2 && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                StartElementDrag(hitModel, clickPos);
            return;
        }

        Point screenPos = ContentToScreen(clickPos.X, clickPos.Y);
        StartMarqueeSelection(screenPos);
    }

    private UIElementModel PickElement(Point clickPos)
    {
        // Use InputHitTest with the same coordinate system as mouse events.
        // Convert clickPos from PreviewContentRoot coords to VisualPreviewContainer coords.
        Point localPos = PreviewContentRoot.TranslatePoint(clickPos, VisualPreviewContainer);

        // InputHitTest uses WPF's input hit testing — same as what determines e.OriginalSource.
        IInputElement inputHit = VisualPreviewContainer.InputHitTest(localPos);
        if (inputHit is DependencyObject dObj)
        {
            DependencyObject walk = dObj;
            while (walk != null && walk != VisualPreviewContainer)
            {
                if (walk is FrameworkElement fe && fe.DataContext is UIElementModel model && Elements.Contains(model))
                    return model;
                walk = VisualTreeHelper.GetParent(walk);
            }
        }

        // Fallback: enumerate elements in reverse (topmost first) and check model bounds.
        // This catches elements whose container layout isn't ready (Canvas.GetLeft = NaN).
        double cw = ContainerWidth;
        if (cw <= 0) cw = PreviewContentRoot.Width;
        if (cw <= 0) return null;
        double ch = ContainerHeight;
        if (ch <= 0) ch = PreviewContentRoot.Height;
        if (ch <= 0) return null;

        double nx = clickPos.X / cw;
        double ny = clickPos.Y / ch;

        for (int i = Elements.Count - 1; i >= 0; i--)
        {
            var el = Elements[i];
            if (nx >= el.X && nx <= el.X + el.Width && ny >= el.Y && ny <= el.Y + el.Height)
                return el;
        }

        return null;
    }

    private void SelectElement(UIElementModel model, MouseButtonEventArgs e)
    {
        _historyManager.SaveState();

        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (ctrl || shift)
        {
            if (ElementsList.SelectedItems.Contains(model))
                ElementsList.SelectedItems.Remove(model);
            else
                ElementsList.SelectedItems.Add(model);
            UpdateSelectionBox();
            return;
        }

        if (ElementsList.SelectedItems.Contains(model) && ElementsList.SelectedItems.Count > 1)
        {
            UpdateSelectionBox();
            return;
        }

        ElementsList.UnselectAll();

        if (e.ClickCount >= 2 && !string.IsNullOrEmpty(model.GroupId))
        {
            foreach (var el in Elements.Where(x => x.GroupId == model.GroupId))
                ElementsList.SelectedItems.Add(el);
        }
        else
        {
            ElementsList.SelectedItems.Add(model);
        }

        UpdateSelectionBox();
    }

    private void StartElementDrag(UIElementModel model, Point clickPos)
    {
        if (_isMarqueeSelecting) return;

        _draggedElement = model;
        _isDragging = true;
        _dragThresholdExceeded = false;
        _dragStartPoint = clickPos;
        _draggedElementsData.Clear();

        double cw = ContainerWidth;
        double ch = ContainerHeight;

        foreach (var item in ElementsList.SelectedItems)
        {
            if (item is UIElementModel el)
            {
                double normW = el.Width;
                double normH = el.Height;

                if (el.Type == ElementType.Text && cw > 0 && ch > 0)
                {
                    if (VisualPreviewContainer.ItemContainerGenerator.ContainerFromItem(el) is FrameworkElement container)
                    {
                        var grid = VisualTreeHelper.GetChild(container, 0) as FrameworkElement;
                        if (grid != null && grid.ActualWidth > 0)
                        {
                            normW = grid.ActualWidth / cw;
                            normH = grid.ActualHeight / ch;
                        }
                    }
                }

                _draggedElementsData[el] = (el.X, el.Y, normW, normH);
            }
        }

        PreviewBorder.CaptureMouse();
    }

    private void StartMarqueeSelection(Point clickPos)
    {
        bool isModifierPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
                                  Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (!isModifierPressed)
            ElementsList.UnselectAll();

        _isMarqueeSelecting = true;
        _marqueeStartPoint = clickPos;
        MarqueeRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(MarqueeRect, clickPos.X);
        Canvas.SetTop(MarqueeRect, clickPos.Y);
        MarqueeRect.Width = 0;
        MarqueeRect.Height = 0;
        PreviewBorder.CaptureMouse();
    }

    private void PreviewBorder_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            PreviewBorder.ReleaseMouseCapture();
            _isPanning = false;
            e.Handled = true;
        }
    }

    private void PreviewWorkspace_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMarqueeSelecting)
        {
            CompleteMarqueeSelection();
            PreviewBorder.ReleaseMouseCapture();
            _isMarqueeSelecting = false;
            MarqueeRect.Visibility = Visibility.Collapsed;
            return;
        }

        if (_isDragging)
        {
            PreviewBorder.ReleaseMouseCapture();
            _isDragging = false;
            _draggedElement = null;
            _draggedElementsData.Clear();
        }
        if (_isPanning)
        {
            PreviewBorder.ReleaseMouseCapture();
            _isPanning = false;
        }
    }

    private void CompleteMarqueeSelection()
    {
        double cw = ContainerWidth;
        double ch = ContainerHeight;
        if (cw <= 0 || ch <= 0) return;

        double x = Canvas.GetLeft(MarqueeRect);
        double y = Canvas.GetTop(MarqueeRect);
        double w = MarqueeRect.Width;
        double h = MarqueeRect.Height;

        if (w < 3 && h < 3) return;

        double scale = PreviewScaleTransform.ScaleX;
        double bw = PreviewBorder.ActualWidth;
        double bh = PreviewBorder.ActualHeight;
        double halfCw = PreviewContentRoot.Width / 2.0;
        double halfCh = PreviewContentRoot.Height / 2.0;

        double normLeft = ((x - bw / 2.0 - _panX) / scale + halfCw) / cw;
        double normTop = ((y - bh / 2.0 - _panY) / scale + halfCh) / ch;
        double normRight = ((x + w - bw / 2.0 - _panX) / scale + halfCw) / cw;
        double normBottom = ((y + h - bh / 2.0 - _panY) / scale + halfCh) / ch;

        bool isModifierPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
                                  Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (!isModifierPressed)
            ElementsList.UnselectAll();

        foreach (var el in Elements)
        {
            double elRight = el.X + el.Width;
            double elBottom = el.Y + el.Height;
            bool intersects = el.X < normRight && elRight > normLeft && el.Y < normBottom && elBottom > normTop;
            if (intersects)
            {
                if (!ElementsList.SelectedItems.Contains(el))
                    ElementsList.SelectedItems.Add(el);
            }
        }

        Dispatcher.InvokeAsync(UpdateSelectionBox);
    }

    private void PreviewWorkspace_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            Point current = e.GetPosition(PreviewBorder);
            double dx = current.X - _panStartPoint.X;
            double dy = current.Y - _panStartPoint.Y;
            _panX = _panStartX + dx;
            _panY = _panStartY + dy;
            ClampPan();
            PreviewTranslateTransform.X = _panX;
            PreviewTranslateTransform.Y = _panY;
            UpdateScrollbars();
            Dispatcher.InvokeAsync(UpdateSelectionBox);
            return;
        }

        if (_isMarqueeSelecting)
        {
            Point cur = e.GetPosition(PreviewBorder);
            double x = Math.Min(_marqueeStartPoint.X, cur.X);
            double y = Math.Min(_marqueeStartPoint.Y, cur.Y);
            double w = Math.Abs(cur.X - _marqueeStartPoint.X);
            double h = Math.Abs(cur.Y - _marqueeStartPoint.Y);
            Canvas.SetLeft(MarqueeRect, x);
            Canvas.SetTop(MarqueeRect, y);
            MarqueeRect.Width = w;
            MarqueeRect.Height = h;
            return;
        }

        if (_isDragging && _draggedElement != null)
        {
            double cw = ContainerWidth;
            double ch = ContainerHeight;

            if (cw <= 0 || ch <= 0) return;

            Point currentPoint = e.GetPosition(PreviewContentRoot);
            double deltaX = currentPoint.X - _dragStartPoint.X;
            double deltaY = currentPoint.Y - _dragStartPoint.Y;

            // Drag threshold — don't budge elements on micro-movements
            if (!_dragThresholdExceeded)
            {
                _dragThresholdExceeded = Math.Abs(deltaX) > 4.0 || Math.Abs(deltaY) > 4.0;
                if (!_dragThresholdExceeded) return;
            }

            double normDeltaX = deltaX / cw;
            double normDeltaY = deltaY / ch;

            double minAllowedDeltaX = double.MinValue;
            double maxAllowedDeltaX = double.MaxValue;
            double minAllowedDeltaY = double.MinValue;
            double maxAllowedDeltaY = double.MaxValue;

            foreach (var kvp in _draggedElementsData)
            {
                var (origX, origY, normElementWidth, normElementHeight) = kvp.Value;

                double minXDelta = -origX;
                double maxXDelta = (1.0 - normElementWidth) - origX;

                double minYDelta = -origY;
                double maxYDelta = (1.0 - normElementHeight) - origY;

                if (minXDelta > minAllowedDeltaX) minAllowedDeltaX = minXDelta;
                if (maxXDelta < maxAllowedDeltaX) maxAllowedDeltaX = maxXDelta;
                if (minYDelta > minAllowedDeltaY) minAllowedDeltaY = minYDelta;
                if (maxYDelta < maxAllowedDeltaY) maxAllowedDeltaY = maxYDelta;
            }

            if (minAllowedDeltaX <= maxAllowedDeltaX)
                normDeltaX = Math.Clamp(normDeltaX, minAllowedDeltaX, maxAllowedDeltaX);
            else normDeltaX = 0;

            if (minAllowedDeltaY <= maxAllowedDeltaY)
                normDeltaY = Math.Clamp(normDeltaY, minAllowedDeltaY, maxAllowedDeltaY);
            else normDeltaY = 0;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) &&
                _draggedElementsData.TryGetValue(_draggedElement, out var primaryOrig))
            {
                double snappedX = Math.Round((primaryOrig.X + normDeltaX) / 0.001) * 0.001;
                double snappedY = Math.Round((primaryOrig.Y + normDeltaY) / 0.001) * 0.001;

                normDeltaX = snappedX - primaryOrig.X;
                normDeltaY = snappedY - primaryOrig.Y;

                if (minAllowedDeltaX <= maxAllowedDeltaX)
                    normDeltaX = Math.Clamp(normDeltaX, minAllowedDeltaX, maxAllowedDeltaX);
                else normDeltaX = 0;

                if (minAllowedDeltaY <= maxAllowedDeltaY)
                    normDeltaY = Math.Clamp(normDeltaY, minAllowedDeltaY, maxAllowedDeltaY);
                else normDeltaY = 0;
            }

            foreach (var kvp in _draggedElementsData)
            {
                var el = kvp.Key;
                var (origX, origY, _, _) = kvp.Value;

                el.X = Math.Round(origX + normDeltaX, 3);
                el.Y = Math.Round(origY + normDeltaY, 3);
            }

            UpdateSelectionBox();
        }
    }

    private void PropertyInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender is TextBox textBox)
            {
                _historyManager.SaveState();
                BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
                be?.UpdateSource();

                if (_squareAspectLock && ElementsList.SelectedItem is UIElementModel selected)
                {
                    double cw = ContainerWidth;
                    double ch = ContainerHeight;
                    if (cw > 0 && ch > 0)
                    {
                        if (sender == WidthTextBox)
                        {
                            double h = selected.Width * (cw / ch);
                            selected.Height = Math.Round(h, 4);
                        }
                        else if (sender == HeightTextBox)
                        {
                            double w = selected.Height * (ch / cw);
                            selected.Width = Math.Round(w, 4);
                        }
                    }
                }

                Keyboard.ClearFocus();
                Dispatcher.InvokeAsync(UpdateSelectionBox);
            }
        }
    }

    private void Eyedropper_Click(object sender, RoutedEventArgs e)
    {
        if (ElementsList.SelectedItems.Count == 0) return;

        _isPickingColor = true;
        Mouse.OverrideCursor = Cursors.Cross;
    }

    private void UpdateHexTextBox(UIElementModel el)
    {
        if (HexColorTextBox != null)
        {
            HexColorTextBox.Text = $"#{el.R:X2}{el.G:X2}{el.B:X2}";
        }
    }

    private void HexColorTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ApplyHexColor();
    }

    private void HexColorTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyHexColor();
            Keyboard.ClearFocus();
        }
    }

    private void ApplyHexColor()
    {
        if (ElementsList.SelectedItem is UIElementModel selected && !string.IsNullOrWhiteSpace(HexColorTextBox.Text))
        {
            string hex = HexColorTextBox.Text.TrimStart('#');
            if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int val))
            {
                _historyManager.SaveState();
                int r = (val >> 16) & 0xFF;
                int g = (val >> 8) & 0xFF;
                int b = val & 0xFF;

                foreach (UIElementModel item in ElementsList.SelectedItems)
                {
                    item.R = r;
                    item.G = g;
                    item.B = b;
                }
            }
        }
    }

    private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ElementsList.SelectedItem is UIElementModel selected)
        {
            UpdateHexTextBox(selected);
        }
    }

    private void ColorSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _historyManager.SaveState();
    }

    private void ColorSlider_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            _historyManager.SaveState();
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ElementsList.SelectedItems.Count == 0) return;

        if (sender is Thumb thumb)
        {
            double cw = ContainerWidth;
            double ch = ContainerHeight;

            if (cw <= 0 || ch <= 0) return;

            if (_resizingModel == null)
            {
                _resizingModel = ElementsList.SelectedItems[0] as UIElementModel;
                _lastMousePosition = Mouse.GetPosition(PreviewContentRoot);
                _resizedElementsData.Clear();

                var targetElements = ElementsList.SelectedItems.Cast<UIElementModel>().ToList();

                double minX = double.MaxValue, minY = double.MaxValue;
                double maxR = double.MinValue, maxB = double.MinValue;

                foreach (var el in targetElements)
                {
                    double normW = el.Width;
                    double normH = el.Height;

                    if (el.Type == ElementType.Text)
                    {
                        if (VisualPreviewContainer.ItemContainerGenerator.ContainerFromItem(el) is FrameworkElement
                            container)
                        {
                            var grid = VisualTreeHelper.GetChild(container, 0) as FrameworkElement;
                            if (grid != null && grid.ActualWidth > 0)
                            {
                                normW = grid.ActualWidth / cw;
                                normH = grid.ActualHeight / ch;
                            }
                        }
                    }

                    _resizedElementsData[el] = (el.X, el.Y, el.Width, el.Height);

                    if (el.X < minX) minX = el.X;
                    if (el.Y < minY) minY = el.Y;
                    if (el.X + normW > maxR) maxR = el.X + normW;
                    if (el.Y + normH > maxB) maxB = el.Y + normH;
                }

                _initialGroupBounds = (minX, minY, maxR - minX, maxB - minY);

                DragCompletedEventHandler completedHandler = null;
                completedHandler = (s, args) =>
                {
                    thumb.DragCompleted -= completedHandler;
                    _resizingModel = null;
                    _resizedElementsData.Clear();
                };
                thumb.DragCompleted += completedHandler;
            }

            string direction = thumb.Tag as string;
            if (string.IsNullOrEmpty(direction)) return;

            Point currentMousePosition = Mouse.GetPosition(PreviewContentRoot);
            double deltaPx = currentMousePosition.X - _lastMousePosition.X;
            double deltaPy = currentMousePosition.Y - _lastMousePosition.Y;

            double initialRight = _initialGroupBounds.X + _initialGroupBounds.Width;
            double initialBottom = _initialGroupBounds.Y + _initialGroupBounds.Height;

            double newGroupX = _initialGroupBounds.X;
            double newGroupY = _initialGroupBounds.Y;
            double newGroupWidth = _initialGroupBounds.Width;
            double newGroupHeight = _initialGroupBounds.Height;

            if (direction.Contains("W"))
            {
                double normDeltaX = deltaPx / cw;
                newGroupX = Math.Clamp(_initialGroupBounds.X + normDeltaX, 0.0, Math.Max(0.0, initialRight - 0.001));
                newGroupWidth = Math.Max(0.001, initialRight - newGroupX);
            }
            else if (direction.Contains("E"))
            {
                double maxW = Math.Max(0.001, 1.0 - _initialGroupBounds.X);
                double normDeltaW = deltaPx / cw;
                newGroupWidth = Math.Clamp(_initialGroupBounds.Width + normDeltaW, 0.001, maxW);
            }

            if (direction.Contains("N"))
            {
                double normDeltaY = deltaPy / ch;
                newGroupY = Math.Clamp(_initialGroupBounds.Y + normDeltaY, 0.0, Math.Max(0.0, initialBottom - 0.001));
                newGroupHeight = Math.Max(0.001, initialBottom - newGroupY);
            }
            else if (direction.Contains("S"))
            {
                double maxH = Math.Max(0.001, 1.0 - _initialGroupBounds.Y);
                double normDeltaH = deltaPy / ch;
                newGroupHeight = Math.Clamp(_initialGroupBounds.Height + normDeltaH, 0.001, maxH);
            }

            double scaleX = _initialGroupBounds.Width > 0 ? newGroupWidth / _initialGroupBounds.Width : 1.0;
            double scaleY = _initialGroupBounds.Height > 0 ? newGroupHeight / _initialGroupBounds.Height : 1.0;

            foreach (var kvp in _resizedElementsData)
            {
                var el = kvp.Key;
                var (origX, origY, origW, origH) = kvp.Value;

                if (direction.Contains("W") || direction.Contains("E"))
                {
                    double relX = origX - _initialGroupBounds.X;
                    el.X = Math.Round(newGroupX + relX * scaleX, 4);
                    el.Width = Math.Round(Math.Max(0.001, origW * scaleX), 4);
                }

                if (direction.Contains("N") || direction.Contains("S"))
                {
                    double relY = origY - _initialGroupBounds.Y;
                    el.Y = Math.Round(newGroupY + relY * scaleY, 4);
                    el.Height = Math.Round(Math.Max(0.001, origH * scaleY), 4);
                }
            }

            UpdateSelectionBox();
        }
    }

    private void PreviewBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePreviewScale();
        RefreshFontForViewport();
        UpdateSelectionBox();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _zoomPercent = e.NewValue;
        if (ZoomLabel != null)
        {
            ZoomLabel.Text = _zoomPercent >= 100
                ? $"{(int)_zoomPercent}%"
                : $"{_zoomPercent:F0}%";
        }
        UpdatePreviewScale();
        RefreshFontForViewport();
    }

    private void PreviewWorkspace_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            double oldScale = PreviewScaleTransform.ScaleX;
            Point mousePos = e.GetPosition(PreviewBorder);
            double availW = PreviewBorder.ActualWidth;
            double availH = PreviewBorder.ActualHeight;
            double cw = PreviewContentRoot.Width;
            double ch = PreviewContentRoot.Height;

            double contentX = (mousePos.X - availW / 2.0 - _panX) / oldScale + cw / 2.0;
            double contentY = (mousePos.Y - availH / 2.0 - _panY) / oldScale + ch / 2.0;

            double delta = e.Delta > 0 ? 10 : -10;
            double newValue = Math.Clamp(ZoomSlider.Value + delta, ZoomSlider.Minimum, ZoomSlider.Maximum);
            ZoomSlider.Value = newValue;

            double newScale = PreviewScaleTransform.ScaleX;
            _panX = mousePos.X - availW / 2.0 - (contentX - cw / 2.0) * newScale;
            _panY = mousePos.Y - availH / 2.0 - (contentY - ch / 2.0) * newScale;
            ClampPan();
            PreviewTranslateTransform.X = _panX;
            PreviewTranslateTransform.Y = _panY;
            UpdateScrollbars();

            e.Handled = true;
            return;
        }

        Point pos = e.GetPosition(PreviewBorder);
        double bh = PreviewBorder.ActualHeight;

        if (pos.Y >= bh - PreviewHScrollBar.ActualHeight)
        {
            double scroll = e.Delta > 0 ? 30.0 : -30.0;
            _panX += scroll;
            ClampPan();
            PreviewTranslateTransform.X = _panX;
            UpdateScrollbars();
            Dispatcher.InvokeAsync(UpdateSelectionBox);
            e.Handled = true;
        }
        else
        {
            double scroll = e.Delta > 0 ? 30.0 : -30.0;
            _panY += scroll;
            ClampPan();
            PreviewTranslateTransform.Y = _panY;
            UpdateScrollbars();
            Dispatcher.InvokeAsync(UpdateSelectionBox);
            e.Handled = true;
        }
    }

    private void RefreshFontForViewport()
    {
        int refH = SettingsService.Instance.Current.UIEditorRefHeight;
        if (refH <= 0) refH = 1080;
        FontService.Instance.PreviewHeight = refH * 0.5;
        FontService.Instance.InvalidateCache();
        Dispatcher.InvokeAsync(RefreshFontBindings);
    }

    private void UpdatePreviewScale()
    {
        if (PreviewBorder == null || PreviewContentRoot == null || PreviewScaleTransform == null) return;

        double availableWidth = PreviewBorder.ActualWidth;
        double availableHeight = PreviewBorder.ActualHeight;

        if (availableWidth <= 0 || availableHeight <= 0) return;

        int refW = SettingsService.Instance.Current.UIEditorRefWidth;
        int refH = SettingsService.Instance.Current.UIEditorRefHeight;
        if (refW <= 0) refW = 1920;
        if (refH <= 0) refH = 1080;

        double aspectRatio = (double)refW / refH;

        double fitWidth = availableWidth;
        double fitHeight = fitWidth / aspectRatio;

        if (fitHeight > availableHeight)
        {
            fitHeight = availableHeight;
            fitWidth = fitHeight * aspectRatio;
        }

        PreviewContentRoot.Width = refW * 0.5;
        PreviewContentRoot.Height = refH * 0.5;

        double fitScale = fitWidth / refW;
        double scale = fitScale * (_zoomPercent / 100.0);

        PreviewScaleTransform.ScaleX = scale;
        PreviewScaleTransform.ScaleY = scale;

        ClampPan();
        PreviewTranslateTransform.X = _panX;
        PreviewTranslateTransform.Y = _panY;

        UpdateScrollbars();
        Dispatcher.InvokeAsync(UpdateSelectionBox);
    }

    private double ContainerWidth => PreviewContentRoot.ActualWidth;
    private double ContainerHeight => PreviewContentRoot.ActualHeight;

    private Point ContentToScreen(double contentX, double contentY)
    {
        double scale = PreviewScaleTransform.ScaleX;
        double cw = PreviewContentRoot.Width;
        double ch = PreviewContentRoot.Height;
        double bw = PreviewBorder.ActualWidth;
        double bh = PreviewBorder.ActualHeight;
        double sx = bw / 2.0 + (contentX - cw / 2.0) * scale + _panX;
        double sy = bh / 2.0 + (contentY - ch / 2.0) * scale + _panY;
        return new Point(sx, sy);
    }

    private void ClampPan()
    {
        double availableWidth = PreviewBorder.ActualWidth;
        double availableHeight = PreviewBorder.ActualHeight;
        double scale = PreviewScaleTransform.ScaleX;
        double contentW = PreviewContentRoot.Width * scale;
        double contentH = PreviewContentRoot.Height * scale;

        double maxPanX = (contentW + availableWidth) / 2.0;
        double maxPanY = (contentH + availableHeight) / 2.0;

        _panX = Math.Clamp(_panX, -maxPanX, maxPanX);
        _panY = Math.Clamp(_panY, -maxPanY, maxPanY);
    }

    private void UpdateScrollbars()
    {
        if (PreviewBorder == null || PreviewContentRoot == null || PreviewScaleTransform == null) return;

        double availableWidth = PreviewBorder.ActualWidth;
        double availableHeight = PreviewBorder.ActualHeight;
        double scale = PreviewScaleTransform.ScaleX;
        double contentW = PreviewContentRoot.Width * scale;
        double contentH = PreviewContentRoot.Height * scale;

        const double scrollRange = 1000;

        _isUpdatingScrollbar = true;

        if (availableWidth > 0)
        {
            double maxPanX = (contentW + availableWidth) / 2.0;
            if (contentW > availableWidth)
            {
                PreviewHScrollBar.Visibility = Visibility.Visible;
                PreviewHScrollBar.Minimum = 0;
                PreviewHScrollBar.Maximum = scrollRange;
                PreviewHScrollBar.ViewportSize = scrollRange * (availableWidth / contentW);
                PreviewHScrollBar.Value = (scrollRange / 2.0) - (_panX / maxPanX) * (scrollRange / 2.0);
            }
            else
            {
                PreviewHScrollBar.Visibility = Visibility.Collapsed;
            }
        }

        if (availableHeight > 0)
        {
            double maxPanY = (contentH + availableHeight) / 2.0;
            if (contentH > availableHeight)
            {
                PreviewVScrollBar.Visibility = Visibility.Visible;
                PreviewVScrollBar.Minimum = 0;
                PreviewVScrollBar.Maximum = scrollRange;
                PreviewVScrollBar.ViewportSize = scrollRange * (availableHeight / contentH);
                PreviewVScrollBar.Value = (scrollRange / 2.0) + (_panY / maxPanY) * (scrollRange / 2.0);
            }
            else
            {
                PreviewVScrollBar.Visibility = Visibility.Collapsed;
            }
        }

        _isUpdatingScrollbar = false;
    }

    private void PreviewScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingScrollbar) return;
        if (PreviewBorder == null || PreviewContentRoot == null || PreviewScaleTransform == null) return;

        double availableWidth = PreviewBorder.ActualWidth;
        double availableHeight = PreviewBorder.ActualHeight;
        double scale = PreviewScaleTransform.ScaleX;
        double contentW = PreviewContentRoot.Width * scale;
        double contentH = PreviewContentRoot.Height * scale;
        const double scrollRange = 1000;

        if (sender == PreviewHScrollBar && contentW > availableWidth)
        {
            double maxPanX = (contentW + availableWidth) / 2.0;
            _panX = -((e.NewValue - scrollRange / 2.0) / (scrollRange / 2.0) * maxPanX);
            ClampPan();
            PreviewTranslateTransform.X = _panX;
        }
        else if (sender == PreviewVScrollBar && contentH > availableHeight)
        {
            double maxPanY = (contentH + availableHeight) / 2.0;
            _panY = (e.NewValue - scrollRange / 2.0) / (scrollRange / 2.0) * maxPanY;
            ClampPan();
            PreviewTranslateTransform.Y = _panY;
        }
        Dispatcher.InvokeAsync(UpdateSelectionBox);
    }

    private void SquareAspectToggle_Click(object sender, RoutedEventArgs e)
    {
        _squareAspectLock = SquareAspectToggle.IsChecked == true;
    }

    private void RefreshFontBindings()
    {
        foreach (var item in VisualPreviewContainer.Items)
        {
            if (VisualPreviewContainer.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter container)
                RefreshFontBindingsRecursive(container);
        }
    }

    private void RefreshFontBindingsRecursive(DependencyObject element)
    {
        if (element == null) return;

        if (element is TextBlock tb)
        {
            BindingOperations.GetBindingExpressionBase(tb, TextBlock.FontSizeProperty)?.UpdateTarget();
            BindingOperations.GetBindingExpressionBase(tb, TextBlock.FontFamilyProperty)?.UpdateTarget();
        }

        int count = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < count; i++)
            RefreshFontBindingsRecursive(VisualTreeHelper.GetChild(element, i));
    }

    private void UpdateSelectionBox()
    {
        if (ElementsList.SelectedItems.Count == 0)
        {
            SelectionBox.Visibility = Visibility.Collapsed;
            return;
        }

        double minScrX = double.MaxValue, minScrY = double.MaxValue;
        double maxScrR = double.MinValue, maxScrB = double.MinValue;
        bool any = false;

        foreach (UIElementModel el in ElementsList.SelectedItems)
        {
            if (VisualPreviewContainer.ItemContainerGenerator.ContainerFromItem(el) is FrameworkElement container)
            {
                if (container.ActualWidth <= 0 || container.ActualHeight <= 0) continue;

                FrameworkElement inner;
                if (el.Type == ElementType.Text)
                    inner = VisualTreeHelper.GetChild(container, 0) as FrameworkElement;
                else
                    inner = container;

                if (inner == null) continue;

                Point tl = container.TranslatePoint(new Point(0, 0), SelectionOverlayCanvas);
                Point br = inner.TranslatePoint(new Point(inner.ActualWidth, inner.ActualHeight), SelectionOverlayCanvas);

                if (tl.X < minScrX) minScrX = tl.X;
                if (tl.Y < minScrY) minScrY = tl.Y;
                if (br.X > maxScrR) maxScrR = br.X;
                if (br.Y > maxScrB) maxScrB = br.Y;
                any = true;
            }
        }

        if (!any)
        {
            SelectionBox.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(SelectionBox, minScrX);
        Canvas.SetTop(SelectionBox, minScrY);
        SelectionBox.Width = maxScrR - minScrX;
        SelectionBox.Height = maxScrB - minScrY;
        SelectionBox.Visibility = Visibility.Visible;

        UpdateThumbsSize();
    }

    private void UpdateThumbsSize()
    {
        double cornerSize = 5.0;
        double edgeThickness = 3.0;
        double offset = -cornerSize / 2.0;

        ResizeNW.Width = cornerSize;
        ResizeNW.Height = cornerSize;
        ResizeNW.Margin = new Thickness(offset, offset, 0, 0);
        ResizeNE.Width = cornerSize;
        ResizeNE.Height = cornerSize;
        ResizeNE.Margin = new Thickness(0, offset, offset, 0);
        ResizeSW.Width = cornerSize;
        ResizeSW.Height = cornerSize;
        ResizeSW.Margin = new Thickness(offset, 0, 0, offset);
        ResizeSE.Width = cornerSize;
        ResizeSE.Height = cornerSize;
        ResizeSE.Margin = new Thickness(0, 0, offset, offset);

        ResizeN.Height = edgeThickness;
        ResizeN.Margin = new Thickness(cornerSize / 2, -edgeThickness / 2, cornerSize / 2, 0);
        ResizeS.Height = edgeThickness;
        ResizeS.Margin = new Thickness(cornerSize / 2, 0, cornerSize / 2, -edgeThickness / 2);
        ResizeW.Width = edgeThickness;
        ResizeW.Margin = new Thickness(-edgeThickness / 2, cornerSize / 2, 0, cornerSize / 2);
        ResizeE.Width = edgeThickness;
        ResizeE.Margin = new Thickness(0, cornerSize / 2, -edgeThickness / 2, cornerSize / 2);
    }

    private void View_PreviewKeyDown(object sender, KeyEventArgs e) => HandleInputEvent(e);

    private void View_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not TextBox)
        {
            Focus();
        }
    }

    private bool CompileUI()
    {
        if (string.IsNullOrWhiteSpace(CompilePathInput.Text) || string.IsNullOrWhiteSpace(OutputNameInput.Text))
        {
            LoggerService.Instance.LogWarn("Compile path or output file name is empty.");
            return false;
        }

        int refW = SettingsService.Instance.Current.UIEditorRefWidth;
        int refH = SettingsService.Instance.Current.UIEditorRefHeight;
        if (refW <= 0) refW = 1920;
        if (refH <= 0) refH = 1080;
        double widthScale = refW / (double)Math.Min(refW, refH);

        string baseFileName = Path.GetFileNameWithoutExtension(OutputNameInput.Text.Trim());
        string asFileName = baseFileName + ".as";
        string jsonFileName = baseFileName + ".json";

        string outputPath = Path.Combine(CompilePathInput.Text, asFileName);
        string jsonOutputPath = Path.Combine(CompilePathInput.Text, jsonFileName);

        var sb = new StringBuilder();

        sb.AppendLine($"namespace {baseFileName}");
        sb.AppendLine("{");

        foreach (var el in Elements)
        {
            sb.AppendLine($"    GUIElement {el.Name};");
        }

        sb.AppendLine("    void Show(Player player)");
        sb.AppendLine("    {");

        foreach (var el in Elements)
        {
            sb.AppendLine();
            string invariantX = el.X.ToString("F3", CultureInfo.InvariantCulture);
            string invariantY = el.Y.ToString("F3", CultureInfo.InvariantCulture);
            string invariantW = (el.Width * widthScale).ToString("F3", CultureInfo.InvariantCulture);
            string invariantH = el.Height.ToString("F3", CultureInfo.InvariantCulture);
            string invariantOpacity = el.Opacity.ToString("F4", CultureInfo.InvariantCulture);

            string call = "";
            switch (el.Type)
            {
                case ElementType.Rect:
                    call = $"graphics.CreateRect(player, {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                    break;
                case ElementType.Oval:
                    call = $"graphics.CreateOval(player, {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                    break;
                case ElementType.Text:
                    call = $"graphics.CreateText(player, {(int)el.Font}, \"{el.Text}\", {invariantX}, {invariantY}, false)";
                    break;
                case ElementType.Image:
                    call = $"graphics.CreateImage(player, \"{el.Text}\", {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                    break;
                case ElementType.ProgressBar:
                    string pTime = string.IsNullOrWhiteSpace(el.MiscValue) ? "5.0" : el.MiscValue;
                    call = $"graphics.CreateProgressBar(player, {pTime}, {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                    break;
            }

            sb.AppendLine($"        {el.Name} = {call};");
            sb.AppendLine($"        {el.Name}.SetColor({el.R}, {el.G}, {el.B});");
            sb.AppendLine($"        {el.Name}.SetOpacity({invariantOpacity}, 0.1f);");
            sb.AppendLine($"        {el.Name}.SetAspect(true);");

            if (!string.IsNullOrWhiteSpace(el.MiscValue) && el.Type != ElementType.ProgressBar)
            {
                sb.AppendLine($"        {el.Name}.SetCallback(\"{el.MiscValue}\");");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    void Hide()");
        sb.AppendLine("    {");
        foreach (var el in Elements)
        {
            sb.AppendLine($"        {el.Name}.Remove();");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"void OnInitialize()");
        sb.AppendLine("{");
        sb.AppendLine($"    {baseFileName}::Show(NULL);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"void OnTerminate()");
        sb.AppendLine("{");
        sb.AppendLine($"    {baseFileName}::Hide();");
        sb.AppendLine("}");

        var workspace = new UIWorkspaceData
        {
            RefWidth = refW,
            RefHeight = refH,
            SquareViewport = false,
            Elements = Elements
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(workspace, options);

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        File.WriteAllText(jsonOutputPath, jsonString, Encoding.UTF8);

        LoggerService.Instance.LogInfo($"UI compiled successfully to {outputPath}");
        return true;
    }

    private void HandleInputEvent(KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox)
            return;

        HotkeySettings hotkeys = SettingsService.Instance.Current.Hotkeys;

        if (HotkeyMatcher.IsMatch(e, hotkeys.SelectAllKey, hotkeys.SelectAllModifiers))
        {
            ElementsList.SelectAll();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.DeleteKey, hotkeys.DeleteModifiers))
        {
            DeleteSelectedElement();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.RunCompilerKey, hotkeys.RunCompilerModifiers))
        {
            CompileUI();
            e.Handled = true;
            return;
        }

        var selectedElement = ElementsList.SelectedItem as UIElementModel;

        if (HotkeyMatcher.IsMatch(e, hotkeys.RenameKey, hotkeys.RenameKey) || e.Key == Key.F2)
        {
            if (ElementsList.SelectedItem != null)
            {
                ElementNameTextBox.Focus();
                ElementNameTextBox.SelectAll();
                e.Handled = true;
                return;
            }
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.DuplicateKey, hotkeys.DuplicateModifiers))
        {
            if (selectedElement != null)
            {
                _historyManager.SaveState();
                var clone = _historyManager.CloneElement(selectedElement);
                clone.Name = $"{selectedElement.Name}_copy";
                double maxCloneX = Math.Max(0.0, 1.0 - selectedElement.Width);
                double maxCloneY = Math.Max(0.0, 1.0 - selectedElement.Height);
                clone.X = Math.Round(Math.Clamp(selectedElement.X + 0.02, 0.0, maxCloneX), 3);
                clone.Y = Math.Round(Math.Clamp(selectedElement.Y + 0.02, 0.0, maxCloneY), 3);
                Elements.Add(clone);
                ElementsList.SelectedItem = clone;
                Dispatcher.InvokeAsync(UpdateSelectionBox);
            }

            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.UndoKey, hotkeys.UndoModifiers))
        {
            _historyManager.Undo(el => ElementsList.SelectedItem = el);
            Dispatcher.InvokeAsync(UpdateSelectionBox);
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.RedoKey, hotkeys.RedoModifiers) ||
            (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control))
        {
            _historyManager.Redo(el => ElementsList.SelectedItem = el);
            Dispatcher.InvokeAsync(UpdateSelectionBox);
            e.Handled = true;
            return;
        }

        if (ElementsList.SelectedItems.Count > 0 &&
            (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down))
        {
            _historyManager.SaveState();
            bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            double step = isShiftPressed ? 0.01 : 0.001;

            var selectedList = ElementsList.SelectedItems.Cast<UIElementModel>().ToList();

            foreach (var el in selectedList)
            {
                double targetX = el.X;
                double targetY = el.Y;

                switch (e.Key)
                {
                    case Key.Left: targetX -= step; break;
                    case Key.Right: targetX += step; break;
                    case Key.Up: targetY -= step; break;
                    case Key.Down: targetY += step; break;
                }

                double maxX = Math.Max(0.0, 1.0 - el.Width);
                double maxY = Math.Max(0.0, 1.0 - el.Height);

                el.X = Math.Round(Math.Clamp(targetX, 0.0, maxX), 3);
                el.Y = Math.Round(Math.Clamp(targetY, 0.0, maxY), 3);
            }

            UpdateSelectionBox();
            e.Handled = true;
            return;
        }
    }

    private void GroupSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = ElementsList.SelectedItems.Cast<UIElementModel>().ToList();
        if (selected.Count < 2)
        {
            LoggerService.Instance.LogError("Select at least 2 elements to group.");
            return;
        }

        _historyManager.SaveState();

        string newGroupId = "Group 1";
        int counter = 1;
        while (Elements.Any(el => el.GroupId == newGroupId))
        {
            counter++;
            newGroupId = $"Group {counter}";
        }

        foreach (var el in selected)
        {
            el.GroupId = newGroupId;
        }

        LoggerService.Instance.LogInfo($"Successfully grouped {selected.Count} elements.");
        CollectionViewSource.GetDefaultView(Elements).Refresh();
    }

    private void UngroupSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = ElementsList.SelectedItems.Cast<UIElementModel>().ToList();
        if (selected.Count == 0) return;

        _historyManager.SaveState();

        foreach (var el in selected)
        {
            el.GroupId = null;
        }

        LoggerService.Instance.LogInfo("Disbanded selected groups.");
        CollectionViewSource.GetDefaultView(Elements).Refresh();
    }

    private void FontComboBox_DropDownOpened(object sender, EventArgs e)
    {
        _historyManager.SaveState();
    }

    private async void GenerateUI_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button ?? GenerateButton;
        string original = btn.Content as string ?? "✨ Generate UI";

        btn.IsEnabled = false;
        try
        {
            _historyManager.MarkSaved();
            bool success = CompileUI();
            btn.Content = success ? "✓ Generated!" : "✗ Failed";
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Generate UI failed: {ex.Message}");
            btn.Content = "✗ Error";
        }

        await Task.Delay(1500);
        btn.Content = original;
        btn.IsEnabled = true;
    }
}