using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;
using Rectangle = System.Drawing.Rectangle;

namespace Text_Grab.Views
{
    /// <summary>
    /// Interaction logic for FullscreenGrabVideo.xaml
    /// </summary>
    public partial class FullscreenGrabVideo : Window
    {
        private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
        private readonly DispatcherTimer captureTimer;
        private readonly Stopwatch stopwatch = new();
        
        private bool isCapturing = false;
        private string textFromOCR = string.Empty;
        private TextBox? destinationTextBox;
        private Language selectedLanguage;
        private int captureInterval = 2000; // Default 2 seconds
        
        public string TextFromOCR 
        { 
            get => textFromOCR; 
            set => textFromOCR = value; 
        }

        public TextBox? DestinationTextBox
        {
            get => destinationTextBox;
            set => destinationTextBox = value;
        }

        public FullscreenGrabVideo()
        {
            InitializeComponent();
            
            // Set up the language selection
            selectedLanguage = OcrEngine.AvailableRecognizerLanguages[0];
            
            // Initialize the timer
            captureTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(captureInterval)
            };
            captureTimer.Tick += CaptureTimer_Tick;
            
            // Set up the UI
            CaptureIntervalTextBox.Text = (captureInterval / 1000.0).ToString("0.0");
            
            // Don't start capture automatically
            isCapturing = false;
            StartCaptureButton.Content = "Start Capture";
        }

        private async void CaptureTimer_Tick(object? sender, EventArgs e)
        {
            if (!isCapturing)
                return;

            await CaptureAndProcessText();
        }

        private async Task CaptureAndProcessText()
        {
            stopwatch.Restart();

            // Capture the screen area
            string grabbedText = await GetTextFromCurrentScreen();

            // Update UI with the grabbed text
            if (!string.IsNullOrEmpty(grabbedText))
            {
                TextFromOCR = grabbedText;
                ResultTextBlock.Text = grabbedText;
            }

            stopwatch.Stop();
            StatusTextBlock.Text = $"Capture time: {stopwatch.ElapsedMilliseconds}ms";
        }

        private void StartCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            isCapturing = !isCapturing;
            
            if (isCapturing)
            {
                StartCaptureButton.Content = "Stop Capture";
                captureTimer.Start();
            }
            else
            {
                StartCaptureButton.Content = "Start Capture";
                captureTimer.Stop();
            }
        }

        private void ApplyIntervalButton_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(CaptureIntervalTextBox.Text, out double intervalSeconds))
            {
                captureInterval = (int)(intervalSeconds * 1000);
                captureTimer.Interval = TimeSpan.FromMilliseconds(captureInterval);
                
                try
                {
                    DefaultSettings.VideoModeInterval = captureInterval;
                    DefaultSettings.Save();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving VideoModeInterval setting: {ex.Message}");
                }
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextFromOCR))
                return;

            System.Windows.Clipboard.SetText(TextFromOCR);
            NotificationUtilities.ShowToast("Text copied to clipboard");
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextFromOCR))
                return;

            // Use TryInsertString instead of SendStringToActiveWindow
            _ = WindowUtilities.TryInsertString(TextFromOCR);
        }

        private async Task<string> GetTextFromCurrentScreen()
        {
            Rectangle bounds = new(
                (int)Left,
                (int)Top,
                (int)Width,
                (int)Height
            );

            try
            {
                var bitmap = ScreenshotUtilities.GetScreenshotOfRegion(bounds);
                var ocrResult = await OcrUtilities.GetOcrResultFromBitmap(bitmap, selectedLanguage);
                return ocrResult?.Text ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing screen: {ex.Message}");
                return string.Empty;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
            else if (e.Key == Key.Space)
            {
                StartCaptureButton_Click(sender, e);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextFromOCR))
                return;

            EditTextWindow etw = new();
            etw.AddThisText(TextFromOCR);
            etw.Show();
        }
    }
}
