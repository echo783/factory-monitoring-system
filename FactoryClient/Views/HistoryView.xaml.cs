using FactoryClient.Models;
using FactoryClient.ViewModels;
using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace FactoryClient.Views
{
    public partial class HistoryView : UserControl
    {
        private readonly HistoryViewModel _vm = new();

        public HistoryView()
        {
            InitializeComponent();
            DataContext = _vm;
            Loaded += HistoryView_Loaded;
        }

        private async void HistoryView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            await _vm.LoadAsync();
        }

        private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (HistoryGrid.SelectedItem is not ProductionEventDto item)
                {
                    PreviewImage.Source = null;
                    return;
                }

                if (string.IsNullOrWhiteSpace(item.ImagePath))
                {
                    PreviewImage.Source = null;
                    return;
                }

                if (!File.Exists(item.ImagePath))
                {
                    PreviewImage.Source = null;
                    return;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(item.ImagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                PreviewImage.Source = bitmap;
            }
            catch
            {
                PreviewImage.Source = null;
            }
        }
    }
}