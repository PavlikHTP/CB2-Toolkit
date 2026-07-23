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
using Microsoft.Win32;
using Fonts = CB2Toolkit.Core.Models.Enums.Fonts;

namespace CB2Toolkit.UIEditor.Views;

public partial class UIEditorView : LifecycleUserControl
{
    private bool _isDragging;
    private Point _dragStartPoint;
    private UIElementModel _draggedElement;

    private readonly Dictionary<UIElementModel, (double X, double Y, double Width, double Height)>
        _draggedElementsData = new();

    private readonly Dictionary<UIElementModel, (double X, double Y, double Width, double Height)>
        _resizedElementsData = new();

    private (double X, double Y, double Width, double Height) _initialGroupBounds;
    private bool _isUpdatingSelection;
    private Point _contextMenuSpawnPoint;
    private readonly UIServiceManager _historyManager;
    private bool _isPickingColor;
    private UIElementModel _resizingModel;
    private Point _lastMousePosition;

    public ObservableCollection<UIElementModel> Elements { get; set; } = new();

    public Array FontsList => Enum.GetValues(typeof(Fonts));

    public UIEditorView()
    {
        InitializeComponent();
        ElementsList.ItemsSource = Elements;
        VisualPreviewContainer.ItemsSource = Elements;
        _historyManager = new UIServiceManager(Elements);

        CollectionViewSource.GetDefaultView(Elements).GroupDescriptions.Add(new PropertyGroupDescription("GroupId"));
    }

    protected override Task OnViewLoadedAsync()
    {
        var settings = SettingsService.Instance.Current;
        if (!string.IsNullOrEmpty(settings.UIEditorCompilePath))
        {
            CompilePathInput.Text = settings.UIEditorCompilePath;
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
        SettingsService.Instance.Current.UIEditorCompilePath = CompilePathInput.Text;
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
        SettingsService.Instance.Current.UIEditorBackgroundPath = BackgroundPathInput.Text;
        _ = SettingsService.Instance.SaveAsync();
    }

    private void BackToMenu_Click(object sender, RoutedEventArgs e)
    {
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
                var loadedElements =
                    JsonSerializer.Deserialize<ObservableCollection<UIElementModel>>(jsonString, options);

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

            if (!string.IsNullOrEmpty(selected.GroupId) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                _isUpdatingSelection = true;
                try
                {
                    var groupItems = Elements.Where(el => el.GroupId == selected.GroupId).ToList();
                    foreach (var item in groupItems)
                    {
                        if (!ElementsList.SelectedItems.Contains(item))
                        {
                            ElementsList.SelectedItems.Add(item);
                        }
                    }
                }
                finally
                {
                    _isUpdatingSelection = false;
                }
            }
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
        if (e.ClickCount == 2)
        {
            e.Handled = true;

            if (sender is FrameworkElement fe && fe.DataContext is CollectionViewGroup group)
            {
                string oldGroupId = group.Name?.ToString();
                if (string.IsNullOrEmpty(oldGroupId)) return;

                var result = ModernMessageBox.Show(
                    Window.GetWindow(this),
                    "Enter new group name:",
                    "Rename Group",
                    ModernBoxType.Input,
                    oldGroupId
                );

                if (result != null && result.Result == ModernBoxResultType.OK &&
                    !string.IsNullOrWhiteSpace(result.InputText))
                {
                    RenameGroupById(oldGroupId, result.InputText.Trim());
                }
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

    private void ElementsList_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.OriginalSource == ElementsList ||
            e.OriginalSource is Border ||
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
        double containerWidth = PreviewBorder.ActualWidth;
        double containerHeight = PreviewBorder.ActualHeight;

        if (containerWidth > 0 && containerHeight > 0)
        {
            normX = Math.Clamp(_contextMenuSpawnPoint.X / containerWidth, 0.0, 1.0);
            normY = Math.Clamp(_contextMenuSpawnPoint.Y / containerHeight, 0.0, 1.0);
        }
        else
        {
            normX = 0.35;
            normY = 0.35;
        }
    }

    private void PreviewWorkspace_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuSpawnPoint = e.GetPosition(PreviewBorder);

        var hitResult = VisualTreeHelper.HitTest(PreviewBorder, e.GetPosition(PreviewBorder));
        if (hitResult != null)
        {
            DependencyObject depObj = hitResult.VisualHit;
            while (depObj != null && depObj != PreviewBorder)
            {
                if (depObj is FrameworkElement fe && fe.DataContext is UIElementModel model)
                {
                    if (!ElementsList.SelectedItems.Contains(model))
                    {
                        bool isModifierPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
                                                 Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                        if (!isModifierPressed)
                        {
                            ElementsList.UnselectAll();
                        }

                        ElementsList.SelectedItems.Add(model);

                        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && !string.IsNullOrEmpty(model.GroupId))
                        {
                            foreach (var el in Elements.Where(x => x.GroupId == model.GroupId))
                            {
                                if (!ElementsList.SelectedItems.Contains(el))
                                {
                                    ElementsList.SelectedItems.Add(el);
                                }
                            }
                        }
                    }

                    break;
                }

                depObj = VisualTreeHelper.GetParent(depObj);
            }
        }

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
            var hit = VisualTreeHelper.HitTest(PreviewBorder, e.GetPosition(PreviewBorder));
            if (hit != null)
            {
                DependencyObject dObj = hit.VisualHit;
                while (dObj != null && dObj != PreviewBorder)
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

        var hitResult = VisualTreeHelper.HitTest(PreviewBorder, e.GetPosition(PreviewBorder));
        if (hitResult == null)
        {
            ElementsList.UnselectAll();
            return;
        }

        DependencyObject depObj = hitResult.VisualHit;
        bool elementHit = false;

        while (depObj != null && depObj != PreviewBorder)
        {
            if (depObj is Thumb)
            {
                return;
            }

            if (depObj is FrameworkElement fe && fe.DataContext is UIElementModel model)
            {
                elementHit = true;
                _historyManager.SaveState();

                bool isAltPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
                bool isModifierPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
                                         Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

                if (!isModifierPressed)
                {
                    if (isAltPressed)
                    {
                        ElementsList.UnselectAll();
                        ElementsList.SelectedItems.Add(model);
                    }
                    else if (!ElementsList.SelectedItems.Contains(model))
                    {
                        ElementsList.UnselectAll();
                        if (!string.IsNullOrEmpty(model.GroupId))
                        {
                            foreach (var el in Elements.Where(x => x.GroupId == model.GroupId))
                            {
                                ElementsList.SelectedItems.Add(el);
                            }
                        }
                        else
                        {
                            ElementsList.SelectedItems.Add(model);
                        }
                    }
                }
                else
                {
                    if (!isAltPressed && !string.IsNullOrEmpty(model.GroupId))
                    {
                        foreach (var el in Elements.Where(x => x.GroupId == model.GroupId))
                        {
                            if (!ElementsList.SelectedItems.Contains(el))
                            {
                                ElementsList.SelectedItems.Add(el);
                            }
                        }
                    }
                    else
                    {
                        if (!ElementsList.SelectedItems.Contains(model))
                        {
                            ElementsList.SelectedItems.Add(model);
                        }
                    }
                }

                _draggedElement = model;
                _isDragging = true;
                _dragStartPoint = e.GetPosition(PreviewBorder);

                _draggedElementsData.Clear();
                double containerWidth = PreviewBorder.ActualWidth;
                double containerHeight = PreviewBorder.ActualHeight;

                foreach (var item in ElementsList.SelectedItems)
                {
                    if (item is UIElementModel el)
                    {
                        double normElementWidth = el.Width;
                        double normElementHeight = el.Height;

                        if (el.Type == ElementType.Text && containerWidth > 0 && containerHeight > 0)
                        {
                            if (VisualPreviewContainer.ItemContainerGenerator.ContainerFromItem(el) is FrameworkElement
                                container)
                            {
                                var grid = VisualTreeHelper.GetChild(container, 0) as FrameworkElement;
                                if (grid != null && grid.ActualWidth > 0)
                                {
                                    normElementWidth = grid.ActualWidth / containerWidth;
                                    normElementHeight = grid.ActualHeight / containerHeight;
                                }
                            }
                        }

                        _draggedElementsData[el] = (el.X, el.Y, normElementWidth, normElementHeight);
                    }
                }

                PreviewBorder.CaptureMouse();
                e.Handled = true;
                return;
            }

            depObj = VisualTreeHelper.GetParent(depObj);
        }

        if (!elementHit)
        {
            ElementsList.UnselectAll();
        }
    }

    private void PreviewWorkspace_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            PreviewBorder.ReleaseMouseCapture();
            _isDragging = false;
            _draggedElement = null;
            _draggedElementsData.Clear();
        }
    }

    private void PreviewWorkspace_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && _draggedElement != null)
        {
            double containerWidth = PreviewBorder.ActualWidth;
            double containerHeight = PreviewBorder.ActualHeight;

            if (containerWidth == 0 || containerHeight == 0) return;

            Point currentPoint = e.GetPosition(PreviewBorder);
            double deltaX = currentPoint.X - _dragStartPoint.X;
            double deltaY = currentPoint.Y - _dragStartPoint.Y;

            double normDeltaX = deltaX / containerWidth;
            double normDeltaY = deltaY / containerHeight;

            double minAllowedDeltaX = double.MinValue;
            double maxAllowedDeltaX = double.MaxValue;
            double minAllowedDeltaY = double.MinValue;
            double maxAllowedDeltaY = double.MaxValue;

            foreach (var kvp in _draggedElementsData)
            {
                var (origX, origY, normElementWidth, normElementHeight) = kvp.Value;

                double minXDelta = -origX;
                double maxXDelta = Math.Max(0.0, 1.0 - normElementWidth) - origX;

                double minYDelta = -origY;
                double maxYDelta = Math.Max(0.0, 1.0 - normElementHeight) - origY;

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
                double snappedX = Math.Round((primaryOrig.X + normDeltaX) / 0.01) * 0.01;
                double snappedY = Math.Round((primaryOrig.Y + normDeltaY) / 0.01) * 0.01;

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

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ElementsList.SelectedItems.Count == 0) return;

        if (sender is Thumb thumb)
        {
            double containerWidth = PreviewBorder.ActualWidth;
            double containerHeight = PreviewBorder.ActualHeight;

            if (containerWidth <= 0 || containerHeight <= 0) return;

            if (_resizingModel == null)
            {
                _resizingModel = ElementsList.SelectedItems[0] as UIElementModel;
                _lastMousePosition = Mouse.GetPosition(PreviewBorder);
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
                                normW = grid.ActualWidth / containerWidth;
                                normH = grid.ActualHeight / containerHeight;
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

            Point currentMousePosition = Mouse.GetPosition(PreviewBorder);
            double totalNormDeltaX = (currentMousePosition.X - _lastMousePosition.X) / containerWidth;
            double totalNormDeltaY = (currentMousePosition.Y - _lastMousePosition.Y) / containerHeight;

            double initialRight = _initialGroupBounds.X + _initialGroupBounds.Width;
            double initialBottom = _initialGroupBounds.Y + _initialGroupBounds.Height;

            double newGroupX = _initialGroupBounds.X;
            double newGroupY = _initialGroupBounds.Y;
            double newGroupWidth = _initialGroupBounds.Width;
            double newGroupHeight = _initialGroupBounds.Height;

            if (direction.Contains("W"))
            {
                newGroupX = Math.Clamp(_initialGroupBounds.X + totalNormDeltaX, 0.0, initialRight - 0.01);
                newGroupWidth = Math.Max(0.01, initialRight - newGroupX);
            }
            else if (direction.Contains("E"))
            {
                newGroupWidth = Math.Clamp(_initialGroupBounds.Width + totalNormDeltaX, 0.01,
                    1.0 - _initialGroupBounds.X);
            }

            if (direction.Contains("N"))
            {
                newGroupY = Math.Clamp(_initialGroupBounds.Y + totalNormDeltaY, 0.0, initialBottom - 0.01);
                newGroupHeight = Math.Max(0.01, initialBottom - newGroupY);
            }
            else if (direction.Contains("S"))
            {
                newGroupHeight = Math.Clamp(_initialGroupBounds.Height + totalNormDeltaY, 0.01,
                    1.0 - _initialGroupBounds.Y);
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
                    el.Width = Math.Round(Math.Max(0.005, origW * scaleX), 4);
                }

                if (direction.Contains("N") || direction.Contains("S"))
                {
                    double relY = origY - _initialGroupBounds.Y;
                    el.Y = Math.Round(newGroupY + relY * scaleY, 4);
                    el.Height = Math.Round(Math.Max(0.005, origH * scaleY), 4);
                }
            }

            UpdateSelectionBox();
        }
    }

    private void PreviewBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSelectionBox();
    }

    private void UpdateSelectionBox()
    {
        if (ElementsList.SelectedItems.Count == 0)
        {
            SelectionBox.Visibility = Visibility.Collapsed;
            return;
        }

        double containerWidth = PreviewBorder.ActualWidth;
        double containerHeight = PreviewBorder.ActualHeight;

        if (containerWidth <= 0 || containerHeight <= 0) return;

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxR = double.MinValue;
        double maxB = double.MinValue;

        foreach (UIElementModel el in ElementsList.SelectedItems)
        {
            double normW = el.Width;
            double normH = el.Height;

            if (el.Type == ElementType.Text)
            {
                if (VisualPreviewContainer.ItemContainerGenerator.ContainerFromItem(el) is FrameworkElement container)
                {
                    var grid = VisualTreeHelper.GetChild(container, 0) as FrameworkElement;
                    if (grid != null && grid.ActualWidth > 0)
                    {
                        normW = grid.ActualWidth / containerWidth;
                        normH = grid.ActualHeight / containerHeight;
                    }
                }
            }

            if (el.X < minX) minX = el.X;
            if (el.Y < minY) minY = el.Y;
            if (el.X + normW > maxR) maxR = el.X + normW;
            if (el.Y + normH > maxB) maxB = el.Y + normH;
        }

        Canvas.SetLeft(SelectionBox, minX * containerWidth);
        Canvas.SetTop(SelectionBox, minY * containerHeight);
        SelectionBox.Width = (maxR - minX) * containerWidth;
        SelectionBox.Height = (maxB - minY) * containerHeight;
        SelectionBox.Visibility = Visibility.Visible;

        UpdateThumbsSize(SelectionBox.Width, SelectionBox.Height);
    }

    private void UpdateThumbsSize(double boxWidth, double boxHeight)
    {
        double minDim = Math.Min(boxWidth, boxHeight);
        double cornerSize = minDim < 25 ? 4 : (minDim < 50 ? 5 : 6);
        double edgeThickness = minDim < 25 ? 2 : (minDim < 50 ? 3 : 4);
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

    private async void CompileUI()
{
    if (string.IsNullOrWhiteSpace(CompilePathInput.Text) || string.IsNullOrWhiteSpace(OutputNameInput.Text))
    {
        LoggerService.Instance.LogWarn("Compile path or output file name is empty.");
        return;
    }

    try
    {
        string baseFileName = Path.GetFileNameWithoutExtension(OutputNameInput.Text.Trim());
        string asFileName = baseFileName + ".as";
        string jsonFileName = baseFileName + ".json";

        string outputPath = Path.Combine(CompilePathInput.Text, asFileName);
        string jsonOutputPath = Path.Combine(CompilePathInput.Text, jsonFileName);

        StringBuilder sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("namespace UI");
        sb.AppendLine("{");

        foreach (var el in Elements)
        {
            sb.AppendLine($"    GUIElement[] {el.Name};");
        }

        sb.AppendLine("    bool[] states;");
        sb.AppendLine();

        sb.AppendLine("    void Load(uint count)");
        sb.AppendLine("    {");
        foreach (var el in Elements)
        {
            sb.AppendLine($"        {el.Name}.resize(count);");
        }

        sb.AppendLine("        states.resize(count);");
        sb.AppendLine("        for(uint i = 0; i < count; i++) states[i] = false;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    void Unload()");
        sb.AppendLine("    {");
        sb.AppendLine("        for(uint i = 0; i < states.size(); i++) { if(states[i]) Hide(i); }");
        foreach (var el in Elements)
        {
            sb.AppendLine($"        {el.Name}.resize(0);");
        }

        sb.AppendLine("        states.resize(0);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    void Toggle(Player player, int idx)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (states[idx]) Hide(idx);");
        sb.AppendLine("        else Show(player, idx);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    void Show(Player player, int idx)");
        sb.AppendLine("    {");
        sb.AppendLine("        Graphics gfx;");
        sb.AppendLine("        Server srv;");

        foreach (var el in Elements)
        {
            sb.AppendLine();
            string invariantX = el.X.ToString("F3", CultureInfo.InvariantCulture);
            string invariantY = el.Y.ToString("F3", CultureInfo.InvariantCulture);
            string invariantW = el.Width.ToString("F3", CultureInfo.InvariantCulture);
            string invariantH = el.Height.ToString("F3", CultureInfo.InvariantCulture);
            string invariantOpacity = el.Opacity.ToString("F4", CultureInfo.InvariantCulture);

            string call = "";
            switch (el.Type)
            {
                case ElementType.Rect:
                    call = $"gfx.CreateRect(player, {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                    break;
                case ElementType.Oval:
                    call = $"gfx.CreateOval(player, {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                    break;
                case ElementType.Text:
                    call = $"gfx.CreateText(player, {(int)el.Font}, \"{el.Text}\", {invariantX}, {invariantY}, false)";
                    break;
                case ElementType.Image:
                    call = $"gfx.CreateImage(player, \"{el.Text}\", {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                    break;
                case ElementType.ProgressBar:
                    string pTime = string.IsNullOrWhiteSpace(el.MiscValue) ? "5.0" : el.MiscValue;
                    call = $"gfx.CreateProgressBar(player, {pTime}, {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                    break;
            }

            sb.AppendLine($"        {el.Name}[idx] = {call};");
            sb.AppendLine($"        {el.Name}[idx].SetColor({el.R}, {el.G}, {el.B});");
            sb.AppendLine($"        {el.Name}[idx].SetOpacity({invariantOpacity}, 0.1f);");

            if (el.Type == ElementType.Text)
            {
                sb.AppendLine($"        {el.Name}[idx].SetScale({invariantW}, {invariantH});");
            }

            if (!string.IsNullOrWhiteSpace(el.MiscValue) && el.Type != ElementType.ProgressBar)
            {
                sb.AppendLine($"        {el.Name}[idx].SetCallback(\"{el.MiscValue}\");");
            }
        }

        sb.AppendLine();
        sb.AppendLine("        states[idx] = true;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    void Hide(int idx)");
        sb.AppendLine("    {");
        foreach (var el in Elements)
        {
            sb.AppendLine($"        {el.Name}[idx].Remove();");
        }

        sb.AppendLine("        states[idx] = false;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(Elements, options);

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
        await File.WriteAllTextAsync(jsonOutputPath, jsonString, Encoding.UTF8);

        LoggerService.Instance.LogInfo($"UI compiled successfully to {outputPath}");
    }
    catch (Exception ex)
    {
        LoggerService.Instance.LogError($"Compilation failed: {ex.Message}");
    }
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
                clone.X = Math.Round(Math.Clamp(selectedElement.X + 0.02, 0.0, 1.0 - selectedElement.Width), 3);
                clone.Y = Math.Round(Math.Clamp(selectedElement.Y + 0.02, 0.0, 1.0 - selectedElement.Height), 3);
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

        if (HotkeyMatcher.IsMatch(e, hotkeys.RedoKey, hotkeys.RedoModifiers))
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
            double step = isShiftPressed ? 0.05 : 0.005;

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

    private void GenerateUI_Click(object sender, RoutedEventArgs e)
    {

        CompileUI();
    }
}