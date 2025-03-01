using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.ApplicationModel;
using Wpf.Ui.Controls;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for GeneralSettings.xaml
/// </summary>
public partial class GeneralSettings : Page
{
    #region Fields

    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private readonly Brush BadBrush = new SolidColorBrush(Colors.Red);
    private readonly Brush GoodBrush = new SolidColorBrush(Colors.Transparent);
    private double InsertDelaySeconds = 1.5;
    private bool settingsSet = false;

    #endregion Fields

    public GeneralSettings()
    {
        InitializeComponent();

        if (!AppUtilities.IsPackaged())
            OpenExeFolderButton.Visibility = Visibility.Visible;
    }

    private void OpenExeFolderButton_Click(object sender, RoutedEventArgs args)
    {
        if (Path.GetDirectoryName(AppContext.BaseDirectory) is not string exePath)
            return;

        Uri source = new(exePath, UriKind.Absolute);
        System.Windows.Navigation.RequestNavigateEventArgs e = new(source, exePath);
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void AboutBTN_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<FirstRunWindow>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (DefaultSettings.AppTheme == AppTheme.System.ToString())
            SystemThemeRdBtn.IsChecked = true;
        else if (DefaultSettings.AppTheme == AppTheme.Light.ToString())
            LightThemeRdBtn.IsChecked = true;
        else if (DefaultSettings.AppTheme == AppTheme.Dark.ToString())
            DarkThemeRdBtn.IsChecked = true;

        string defaultLaunch = DefaultSettings.FirstWindowToShow;

        if (string.IsNullOrWhiteSpace(defaultLaunch))
            defaultLaunch = FirstWindow.GrabFrame.ToString();

        if (defaultLaunch == FirstWindow.Fullscreen.ToString())
            FullScreenRDBTN.IsChecked = true;
        else if (defaultLaunch == FirstWindow.GrabFrame.ToString())
            GrabFrameRDBTN.IsChecked = true;
        else if (defaultLaunch == FirstWindow.EditText.ToString())
            EditTextRDBTN.IsChecked = true;
        else if (defaultLaunch == FirstWindow.QuickSimpleLookup.ToString())
            QuickLookupRDBTN.IsChecked = true;
        else if (defaultLaunch == FirstWindow.FullscreenGrabVideo.ToString())
            VideoModeRDBTN.IsChecked = true;

        if (AppUtilities.IsPackaged())
        {
            StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");

            switch (startupTask.State)
            {
                case StartupTaskState.Disabled:
                    // Task is disabled but can be enabled.
                    StartupOnLoginCheckBox.IsChecked = false;
                    break;
                case StartupTaskState.DisabledByUser:
                    // Task is disabled and user must enable it manually.
                    StartupOnLoginCheckBox.IsChecked = false;
                    StartupOnLoginCheckBox.IsEnabled = false;

                    StartupTextBlock.Text += "\nDisabled in Task Manager";
                    break;
                case StartupTaskState.Enabled:
                    StartupOnLoginCheckBox.IsChecked = true;
                    break;
            }
        }
        else
        {
            StartupOnLoginCheckBox.IsChecked = DefaultSettings.StartupOnLogin;
        }

        List<WebSearchUrlModel> searcherSettings = Singleton<WebSearchUrlModel>.Instance.WebSearchers;

        foreach (WebSearchUrlModel searcher in searcherSettings)
            WebSearchersComboBox.Items.Add(searcher);

        WebSearchersComboBox.SelectedItem = Singleton<WebSearchUrlModel>.Instance.DefaultSearcher;

        ShowToastCheckBox.IsChecked = DefaultSettings.ShowToast;

        RunInBackgroundChkBx.IsChecked = DefaultSettings.RunInTheBackground;
        ReadBarcodesBarcode.IsChecked = DefaultSettings.TryToReadBarcodes;
        HistorySwitch.IsChecked = DefaultSettings.UseHistory;
        ErrorCorrectBox.IsChecked = DefaultSettings.CorrectErrors;
        CorrectToLatin.IsChecked = DefaultSettings.CorrectToLatin;
        NeverUseClipboardChkBx.IsChecked = DefaultSettings.NeverAutoUseClipboard;
        TryInsertCheckbox.IsChecked = DefaultSettings.TryInsert;
        InsertDelaySeconds = DefaultSettings.InsertDelay;
        SecondsTextBox.Text = InsertDelaySeconds.ToString("##.#", System.Globalization.CultureInfo.InvariantCulture);

        settingsSet = true;
    }

    private void ValidateTextIsNumber(object sender, TextChangedEventArgs e)
    {
        if (!settingsSet)
            return;

        if (sender is System.Windows.Controls.TextBox numberInputBox)
        {
            bool wasAbleToConvert = double.TryParse(numberInputBox.Text, out double parsedText);
            if (wasAbleToConvert && parsedText > 0 && parsedText < 10)
            {
                InsertDelaySeconds = parsedText;
                DefaultSettings.InsertDelay = InsertDelaySeconds;
                DelayTimeErrorSeconds.Visibility = Visibility.Collapsed;
                numberInputBox.BorderBrush = GoodBrush;
            }
            else
            {
                InsertDelaySeconds = 3;
                DelayTimeErrorSeconds.Visibility = Visibility.Visible;
                numberInputBox.BorderBrush = BadBrush;
            }
        }
    }

    private void FullScreenRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.FirstWindowToShow = FirstWindow.Fullscreen.ToString();
    }

    private void GrabFrameRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.FirstWindowToShow = FirstWindow.GrabFrame.ToString();
    }

    private void EditTextRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.FirstWindowToShow = FirstWindow.EditText.ToString();
    }

    private void QuickLookupRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.FirstWindowToShow = FirstWindow.QuickSimpleLookup.ToString();
        DefaultSettings.Save();
    }

    private void VideoModeRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.FirstWindowToShow = FirstWindow.FullscreenGrabVideo.ToString();
        DefaultSettings.Save();
    }

    private void RunInBackgroundChkBx_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        if (sender is not ToggleSwitch runInBackgroundSwitch)
            return;

        DefaultSettings.RunInTheBackground = runInBackgroundSwitch.IsChecked is true;
        ImplementAppOptions.ImplementBackgroundOption(DefaultSettings.RunInTheBackground);
        DefaultSettings.Save();
    }

    private void SystemThemeRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.AppTheme = AppTheme.System.ToString();
        App.SetTheme();
    }

    private void LightThemeRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.AppTheme = AppTheme.Light.ToString();
        App.SetTheme();
    }

    private void DarkThemeRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.AppTheme = AppTheme.Dark.ToString();
        App.SetTheme();
    }

    private void ReadBarcodesBarcode_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TryToReadBarcodes = true;
    }

    private void ReadBarcodesBarcode_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TryToReadBarcodes = false;
    }

    private void HistorySwitch_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.UseHistory = true;
    }

    private void HistorySwitch_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.UseHistory = false;
    }

    private void ErrorCorrectBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.CorrectErrors = true;
    }

    private void ErrorCorrectBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.CorrectErrors = false;
    }

    private void CorrectToLatin_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.CorrectToLatin = true;
    }

    private void CorrectToLatin_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.CorrectToLatin = false;
    }

    private void NeverUseClipboardChkBx_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.NeverAutoUseClipboard = true;
    }

    private void NeverUseClipboardChkBx_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.NeverAutoUseClipboard = false;
    }

    private async void StartupOnLoginCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.StartupOnLogin = true;
        await ImplementAppOptions.ImplementStartupOption(true);
    }

    private async void StartupOnLoginCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.StartupOnLogin = false;
        await ImplementAppOptions.ImplementStartupOption(false);
    }

    private void TryInsertCheckbox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TryInsert = true;
    }

    private void TryInsertCheckbox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TryInsert = false;
    }

    private void ShowToastCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.ShowToast = true;
    }

    private void ShowToastCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.ShowToast = false;
    }

    private void WebSearchersComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!settingsSet
            || sender is not ComboBox comboBox
            || comboBox.SelectedItem is not WebSearchUrlModel newDefault)
            return;

        Singleton<WebSearchUrlModel>.Instance.DefaultSearcher = newDefault;
    }
}
