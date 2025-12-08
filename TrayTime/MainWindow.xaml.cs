using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Windows.Storage;

namespace TrayTime;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // When the user closes the window, hide it instead
        var appWindow = this.AppWindow;
        appWindow.Closing += (sender, args) =>
        {
            // Cancel the close; keep the app alive.
            args.Cancel = true;

            // Hide the window so it's no longer visible.
            sender.Hide();
        };
    }

    bool Not(bool b) => !b;

    //private void TimeZoneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    //{
    //    TimeZoneInfo selectedTimeZoneInfo = ((sender as ComboBox)!.SelectedItem as TimeZoneInfo)!;
    //    TimeNotifyIcon selectedTimeNotifyIcon = ((sender as ComboBox)!.Tag as TimeNotifyIcon)!;
    //    if (selectedTimeNotifyIcon == null || selectedTimeZoneInfo == null)
    //    {
    //        return;
    //    }

    //    selectedTimeNotifyIcon.TimeZone = selectedTimeZoneInfo;
    //    Manager.Instance!.SaveTimeZoneSettings();
    //}

    //private void AddTimeZoneClick(object sender, RoutedEventArgs e)
    //{
    //    //App.Instance.AddTimeZone(TimeZoneInfo.Local);
    //}

    // Debug tool
    private void CreateIndexClick(object sender, RoutedEventArgs e)
    {
        Indexer.ProcessFile();
    }


    //private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    //{
    //    // Set the chosen suggestion as the text
    //    sender.Text = (args.SelectedItem as CityIndex)?.Name;

    //    var cityDetails = Indexer.GetCityDetails((args.SelectedItem as CityIndex)!);
    //}

    // Debug tool
    async private void MapClick(object sender, RoutedEventArgs e)
    {
        // bugbug: aliases are missing, like Asia/Kolkata


        // Get TextReader for windowsZones.xml
        var uri = new Uri("ms-appx:///Assets/windowsZones.xml");
        var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
        var stream = await file.OpenStreamForReadAsync();
        var textReader = new StreamReader(stream);

        // Create XmlReader with settings to ignore DTD
        var settings = new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Ignore
        };
        using var xmlReader = System.Xml.XmlReader.Create(textReader, settings);
        Dictionary<string, string> map = new();


        // <mapZone other = "SA Western Standard Time" territory = "001" type = "America/La_Paz" />
        // <mapZone other = "SA Western Standard Time" territory = "AG" type = "America/Antigua" />
        // <mapZone other = "SA Western Standard Time" territory = "AI" type = "America/Anguilla" />
        // <mapZone other = "SA Western Standard Time" territory = "AW" type = "America/Aruba" />


        // Loop through all mapZone elements
        while (xmlReader.Read())
        {
            if (xmlReader.NodeType == System.Xml.XmlNodeType.Element && 
                xmlReader.Name == "mapZone")
            {
                string other = xmlReader.GetAttribute("other") ?? string.Empty;
                string territory = xmlReader.GetAttribute("territory") ?? string.Empty;
                string type = xmlReader.GetAttribute("type") ?? string.Empty;

                map.TryAdd(type, other);
            }
        }

        StringBuilder sb = new();
        foreach(var kvp in map)
        {
            sb.AppendLine($"{kvp.Key} : {kvp.Value}");
        }

        // TODO: Use WinUI clipboard instead
        // System.Windows.Forms.Clipboard.SetText(sb.ToString());
        Debug.WriteLine("Clipboard content (WinForms removed):");
        Debug.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Add a new time zone to the list
    /// </summary>
    async private void AddTimeZoneClick2(object sender, RoutedEventArgs e)
    {
        var dialog = new AddTimeZoneDialog()
        {
            XamlRoot = this.Content.XamlRoot
        };
        var result = await dialog.ShowAsync();

        if(result == ContentDialogResult.Primary)
        {
            Manager.Instance!.AddTimeZone(dialog.CityDetails!.TimeZoneInfo!, dialog.CityDetails.ToString());
        }
    }

    /// <summary>
    /// Remove a time zone from the lst
    /// </summary>
    private void DeleteTimeZone(object sender, RoutedEventArgs e)
    {
        TimeNotifyIcon icon = ((sender as Button)!.Tag as TimeNotifyIcon)!;
        Manager.Instance!.RemoveTimeZone(icon);
    }
}
