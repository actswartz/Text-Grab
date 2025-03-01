using Dapplo.Windows.User32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Extensions;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Views;

/// <summary>
/// Interaction logic for FullscreenGrabVideo.xaml
/// </summary>
public partial class FullscreenGrabVideo : Window
{
    #region Fields

    private System.Windows.Point clickedPoint = new();
    private TextBox? destinationTextBox;
    private DpiScale? dpiScale;
    private bool isComboBoxReady = false;
    private bool isSelecting = false;
    private bool isShiftDown = false;
    private Border selectBorder = new();
    private double selectLeft;
    private double selectTop;
    private System.Windows.Point shiftPoint = new();
    private double xShiftDelta;
    private double yShiftDelta;
    private HistoryInfo? historyInfo;
    private readonly bool usingTesseract;
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    // Video mode specific fields
    private DispatcherTimer captureTimer = new();
    private bool isContinuousCapturing = false;
    private double captureInterval = 2.0; // Default interval in seconds

    #endregion Fields

    #region Constructors

    public FullscreenGrabVideo()
    {
        InitializeComponent();
        App.SetTheme();
        usingTesseract = DefaultSettings.UseTesseract && TesseractHelper.CanLocateTesseractExe();
        
        // Initialize the timer
        captureTimer.Tick += CaptureTimer_Tick;
        captureTimer.Interval = TimeSpan.FromSeconds(captureInterval);
    }

    #endregion Constructors

    #region Properties

    public TextBox? DestinationTextBox
    {
        get { return destinationTextBox; }
        set
        {
            destinationTextBox = value;
            if (destinationTextBox != null)
                SendToEditTextToggleButton.IsChecked = true;
            else
                SendToEditTextToggleButton.IsChecked = false;
        }
    }

    public bool IsFreeze { get; set; } = false;
    public string? TextFromOCR { get; set; }
    private DisplayInfo? CurrentScreen { get; set; }

    #endregion Properties

    #region Methods

    public void SetImageToBackground()
    {
        BackgroundImage.Source = null;
        BackgroundImage.Source = ImageMethods.GetWindowBoundsImage(this);
        BackgroundBrush.Opacity = 0.2;
    }

    internal void KeyPressed(Key key, bool? isActive = null)
    {
        switch (key)
        {
            // This case is handled in the WindowUtilities.FullscreenKeyDown
            // case Key.Escape:
            //     WindowUtilities.CloseAllFullscreenGrabs();
            //     break;
            case Key.G:
                if (isActive is null)
                    NewGrabFrameToggleButton.IsChecked = !NewGrabFrameToggleButton.IsChecked;
                else
                    NewGrabFrameToggleButton.IsChecked = isActive.Value;
                break;
            case Key.F:
                if (isActive is null)
                    FreezeToggleButton.IsChecked = !FreezeToggleButton.IsChecked;
                else
                    FreezeToggleButton.IsChecked = isActive.Value;
                break;
            case Key.E:
                if (isActive is null)
                    SendToEditTextToggleButton.IsChecked = !SendToEditTextToggleButton.IsChecked;
                else
                    SendToEditTextToggleButton.IsChecked = isActive.Value;
                break;
            case Key.S:
                if (isActive is null)
                    StartCaptureToggleButton.IsChecked = !StartCaptureToggleButton.IsChecked;
                else
                    StartCaptureToggleButton.IsChecked = isActive.Value;
                break;
            case Key.Return:
                NextStepButton_Click(null, null);
                break;
        }
    }

    private void CancelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.CloseAllFullscreenGrabs();
    }

    private void CaptureAndCopyButton_Click(object sender, RoutedEventArgs e)
    {
        PerformOcrAndCopy();
    }

    private void CaptureAndEditButton_Click(object sender, RoutedEventArgs e)
    {
        PerformOcrAndEdit();
    }

    private void CaptureAndInsertButton_Click(object sender, RoutedEventArgs e)
    {
        PerformOcrAndInsert();
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PerformOcrAndCopy();
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PerformOcrAndEdit();
    }

    private void FreezeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        IsFreeze = FreezeToggleButton.IsChecked ?? false;

        if (IsFreeze)
            SetImageToBackground();
        else
            BackgroundBrush.Opacity = 0.01;
    }

    private void NewGrabFrameToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (NewGrabFrameToggleButton.IsChecked ?? false)
        {
            GrabFrame grabFrame = new();
            grabFrame.Show();
        }
    }

    private void NextStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (SendToEditTextToggleButton.IsChecked ?? false)
            PerformOcrAndEdit();
        else
            PerformOcrAndCopy();
    }

    private async Task PerformOcrAndCopy()
    {
        if (isSelecting)
            return;

        if (RegionClickCanvas.ContextMenu?.IsOpen ?? false)
            RegionClickCanvas.ContextMenu.IsOpen = false;

        try
        {
            string? grabbedText = await TryGrabTextAsync();

            if (string.IsNullOrEmpty(grabbedText))
                return;

            TextFromOCR = grabbedText;
            Clipboard.SetText(grabbedText);

            if (DefaultSettings.ShowToast)
                ToastNotificationUtilities.ShowToastNotification("Text Grab", "Text copied to clipboard");

            if (!(DefaultSettings.KeepFullscreenOpen || IsFreeze))
                WindowUtilities.CloseAllFullscreenGrabs();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error grabbing text: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task PerformOcrAndEdit()
    {
        if (isSelecting)
            return;

        if (RegionClickCanvas.ContextMenu?.IsOpen ?? false)
            RegionClickCanvas.ContextMenu.IsOpen = false;

        try
        {
            string? grabbedText = await TryGrabTextAsync();

            if (string.IsNullOrEmpty(grabbedText))
                return;

            TextFromOCR = grabbedText;

            if (DestinationTextBox is not null)
            {
                DestinationTextBox.SelectedText = grabbedText;
                DestinationTextBox.Focus();
            }
            else
            {
                EditTextWindow manipulateTextWindow = WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
                manipulateTextWindow.AddThisText(grabbedText);
            }

            if (!(DefaultSettings.KeepFullscreenOpen || IsFreeze))
                WindowUtilities.CloseAllFullscreenGrabs();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error grabbing text: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task PerformOcrAndInsert()
    {
        if (isSelecting)
            return;

        if (RegionClickCanvas.ContextMenu?.IsOpen ?? false)
            RegionClickCanvas.ContextMenu.IsOpen = false;

        try
        {
            string? grabbedText = await TryGrabTextAsync();

            if (string.IsNullOrEmpty(grabbedText))
                return;

            TextFromOCR = grabbedText;
            Clipboard.SetText(grabbedText);

            if (DefaultSettings.ShowToast)
                ToastNotificationUtilities.ShowToastNotification("Text Grab", "Text copied to clipboard");

            await WindowUtilities.TryInsertString(grabbedText);

            if (!(DefaultSettings.KeepFullscreenOpen || IsFreeze))
                WindowUtilities.CloseAllFullscreenGrabs();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error grabbing text: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RegionClickCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (isSelecting)
            e.Handled = true;
    }

    private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        isSelecting = true;
        clickedPoint = e.GetPosition(RegionClickCanvas);

        if (isShiftDown)
        {
            shiftPoint = clickedPoint;
            xShiftDelta = 0;
            yShiftDelta = 0;
        }

        RegionClickCanvas.Children.Remove(selectBorder);

        selectBorder = new Border
        {
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(65, 177, 225)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 65, 177, 225))
        };

        RegionClickCanvas.Children.Add(selectBorder);

        Canvas.SetLeft(selectBorder, clickedPoint.X);
        Canvas.SetTop(selectBorder, clickedPoint.Y);

        selectBorder.Width = 0;
        selectBorder.Height = 0;

        e.Handled = true;
    }

    private void RegionClickCanvas_MouseEnter(object sender, MouseEventArgs e)
    {
        if (DefaultSettings.FreezeOnStartup && !IsFreeze)
        {
            IsFreeze = true;
            FreezeToggleButton.IsChecked = true;
            SetImageToBackground();
        }
    }

    private void RegionClickCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (isSelecting)
        {
            isSelecting = false;
            RegionClickCanvas.ReleaseMouseCapture();
        }
    }

    private void RegionClickCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isSelecting)
            return;

        System.Windows.Point movingPoint = e.GetPosition(RegionClickCanvas);

        if (isShiftDown)
        {
            double xDelta = movingPoint.X - shiftPoint.X;
            double yDelta = movingPoint.Y - shiftPoint.Y;

            xShiftDelta += xDelta;
            yShiftDelta += yDelta;

            shiftPoint = movingPoint;

            double left = Canvas.GetLeft(selectBorder);
            double top = Canvas.GetTop(selectBorder);

            Canvas.SetLeft(selectBorder, left + xDelta);
            Canvas.SetTop(selectBorder, top + yDelta);
        }
        else
        {
            double width = Math.Abs(movingPoint.X - clickedPoint.X);
            double height = Math.Abs(movingPoint.Y - clickedPoint.Y);

            selectLeft = movingPoint.X < clickedPoint.X ? movingPoint.X : clickedPoint.X;
            selectTop = movingPoint.Y < clickedPoint.Y ? movingPoint.Y : clickedPoint.Y;

            Canvas.SetLeft(selectBorder, selectLeft);
            Canvas.SetTop(selectBorder, selectTop);

            selectBorder.Width = width;
            selectBorder.Height = height;
        }
    }

    private void RegionClickCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        isSelecting = false;
        RegionClickCanvas.ReleaseMouseCapture();

        if (selectBorder.Width < 5 || selectBorder.Height < 5)
        {
            RegionClickCanvas.Children.Remove(selectBorder);
            NextStepButton_Click(null, null);
        }
    }

    private void SendToEditTextToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (SendToEditTextToggleButton.IsChecked ?? false)
        {
            EditTextWindow manipulateTextWindow = WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
            DestinationTextBox = manipulateTextWindow.PassedTextControl;
        }
        else
        {
            DestinationTextBox = null;
        }
    }

    private async Task<string?> TryGrabTextAsync()
    {
        if (dpiScale is null)
            dpiScale = VisualTreeHelper.GetDpi(this);

        if (selectBorder.Width > 5 && selectBorder.Height > 5)
        {
            double selectWidth = selectBorder.Width;
            double selectHeight = selectBorder.Height;

            double left = Canvas.GetLeft(selectBorder);
            double top = Canvas.GetTop(selectBorder);

            // Get the screen position of the window
            System.Windows.Point windowPosition = this.GetAbsolutePosition();

            // Calculate the screen position of the selection
            double screenLeft = windowPosition.X + (left * dpiScale.DpiScaleX);
            double screenTop = windowPosition.Y + (top * dpiScale.DpiScaleY);
            double screenWidth = selectWidth * dpiScale.DpiScaleX;
            double screenHeight = selectHeight * dpiScale.DpiScaleY;

            System.Drawing.Rectangle bounds = new(
                (int)screenLeft,
                (int)screenTop,
                (int)screenWidth,
                (int)screenHeight);

            string? grabbedText = await OcrUtilities.GetRegionsText(bounds, usingTesseract);

            RegionClickCanvas.Children.Remove(selectBorder);

            return grabbedText;
        }
        else
        {
            System.Drawing.Rectangle bounds = this.GetWindowBounds();
            return await OcrUtilities.GetRegionsText(bounds, usingTesseract);
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Stop the timer if it's running
        if (captureTimer.IsEnabled)
            captureTimer.Stop();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        dpiScale = VisualTreeHelper.GetDpi(this);

        if (DefaultSettings.FreezeOnStartup)
        {
            IsFreeze = true;
            FreezeToggleButton.IsChecked = true;
            SetImageToBackground();
        }

        WindowState = WindowState.Maximized;

        // Get current screen
        if (WindowUtilities.GetMousePosition(out System.Windows.Point mousePosition))
        {
            DisplayInfo[] allScreens = DisplayInfo.AllDisplayInfos;
            foreach (DisplayInfo screen in allScreens)
            {
                if (screen.Bounds.Contains(new System.Drawing.Point((int)mousePosition.X, (int)mousePosition.Y)))
                {
                    CurrentScreen = screen;
                    break;
                }
            }
        }

        if (CurrentScreen is null)
            CurrentScreen = DisplayInfo.PrimaryDisplay;

        // Set the capture interval from settings
        captureInterval = DefaultSettings.VideoModeInterval;
        CaptureIntervalBox.Value = captureInterval;
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        // Stop the timer if it's running
        if (captureTimer.IsEnabled)
            captureTimer.Stop();
    }

    // Video mode specific methods
    private void StartCaptureToggleButton_Click(object sender, RoutedEventArgs e)
    {
        isContinuousCapturing = StartCaptureToggleButton.IsChecked ?? false;
        
        if (isContinuousCapturing)
        {
            // Start continuous capture
            captureTimer.Start();
            CaptureStatusBorder.Visibility = Visibility.Visible;
            
            // Disable other controls during capture
            FreezeToggleButton.IsEnabled = false;
            NewGrabFrameToggleButton.IsEnabled = false;
            NextStepDropDownButton.IsEnabled = false;
        }
        else
        {
            // Stop continuous capture
            captureTimer.Stop();
            CaptureStatusBorder.Visibility = Visibility.Collapsed;
            
            // Re-enable controls
            FreezeToggleButton.IsEnabled = true;
            NewGrabFrameToggleButton.IsEnabled = true;
            NextStepDropDownButton.IsEnabled = true;
        }
    }

    private void CaptureIntervalBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (double.TryParse(CaptureIntervalBox.Text, out double newInterval) && newInterval >= 0.5 && newInterval <= 10)
        {
            captureInterval = newInterval;
            captureTimer.Interval = TimeSpan.FromSeconds(captureInterval);
            
            // Save to settings
            DefaultSettings.VideoModeInterval = captureInterval;
            DefaultSettings.Save();
        }
    }

    private async void CaptureTimer_Tick(object? sender, EventArgs e)
    {
        // Perform OCR on the current screen or selection
        await PerformOcrAndCopy();
        
        // Flash the recording indicator
        RecordingIndicator.Opacity = 0.3;
        await Task.Delay(200);
        RecordingIndicator.Opacity = 1.0;
    }

    #endregion Methods
}
