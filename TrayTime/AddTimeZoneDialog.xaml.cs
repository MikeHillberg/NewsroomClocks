using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TrayTime
{
    public sealed partial class AddTimeZoneDialog : ContentDialog, INotifyPropertyChanged
    {
        public AddTimeZoneDialog()
        {
            InitializeComponent();
        }

        async private void AutoSuggestBox_TextChanged(
            AutoSuggestBox sender,
            AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var searchText = sender.Text;
                if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
                {
                    sender.ItemsSource = null;
                    return;
                }

                try
                {
                    var cities = await CityInfoLocation.GetCityIndices();
                    var filteredCities = cities
                        .Where(city => city.Name.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
                        .Take(10) // Limit to 10 suggestions
                                  //.Select(city => city.Item3)
                        .ToList();

                    sender.ItemsSource = filteredCities;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error filtering cities: {ex.Message}");
                    sender.ItemsSource = null;
                }
            }
        }

        private async void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            var location = (args.SelectedItem as CityInfoLocation)!;
            sender.Text = location.Name;

            var cityInfo = await location.GetCityInfoAsync();

            await cityInfo.LoadTimeZoneInfoAsync();
            CityInfo = cityInfo;
            Debug.WriteLine(cityInfo.TimeZoneInfo?.DisplayName);
        }

        private CityInfo? _cityInfo;
        internal CityInfo? CityInfo
        {
            get => _cityInfo;
            set
            {
                _cityInfo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsValid));
            }
        }

        bool IsValid => _cityInfo != null;


        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
