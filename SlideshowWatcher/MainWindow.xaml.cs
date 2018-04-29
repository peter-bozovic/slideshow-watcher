using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SlideshowWatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private DispatcherTimer timerImageChange;
        private Image[] ImageControls;
        private static string[] ValidImageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
        private string strImagePath = "";

        public ImagesDb Images { get; set; }
        private FileSystemWatcher watcher;

        private SlideshowWindow slideshow = new SlideshowWindow();

        public MainWindow()
        {
            Images = new ImagesDb();
            DataContext = this;

            InitializeComponent();

            txtInterval.Text = ConfigurationManager.AppSettings["IntervalTime"];
            txtInterval.TextChanged += TxtInterval_TextChanged;
            chkListDeleted.Checked += ReloadImagesList;
            chkListDeleted.Unchecked += ReloadImagesList;
            chkListExcluded.Checked += ReloadImagesList;
            chkListExcluded.Unchecked += ReloadImagesList;
            lstImages.MouseDoubleClick += LstImages_MouseDoubleClick;

            //Initialize Image control, Image directory path and Image timer.
            strImagePath = ConfigurationManager.AppSettings["ImagesPath"];
            ImageControls = new[] { slideshow.myImage, slideshow.myImage2 };

            Images.LoadFromFolder(ConfigurationManager.AppSettings["ImagesPath"]);

            //lstImages.ItemsSource = images.ImagesCollection;

            timerImageChange = new DispatcherTimer();
            timerImageChange.Interval = new TimeSpan(0, 0, Convert.ToInt32(txtInterval.Text));
            timerImageChange.Tick += new EventHandler(timerImageChange_Tick);

            watcher = new FileSystemWatcher();
            watcher.Path = ConfigurationManager.AppSettings["ImagesPath"];
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.DirectoryName;
            watcher.Changed += Watcher_Event;
            watcher.Created += Watcher_Event;
            watcher.Deleted += Watcher_Event;
            watcher.Renamed += Watcher_Renamed;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            slideshow.Show();
        }

        private void LstImages_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Images.SetAsNext(lstImages.SelectedItem as ImagesDb.ImageItem);
        }

        private void ReloadImagesList(object sender, RoutedEventArgs e)
        {
            Images.ReloadImagesList();
        }

        private void TxtInterval_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtInterval.Text, out int value))
            {
                timerImageChange.Interval = new TimeSpan(0, 0, value);
            }
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (!e.FullPath.Contains(".tmp.drivedownload")) Debug.WriteLine($"{ DateTime.Now.ToString("s") } : {e.ChangeType} - {e.OldFullPath} => {e.FullPath}");
            Images.TrackChange(e.ChangeType, e.FullPath, e.OldFullPath);
        }

        private void Watcher_Event(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created && Directory.Exists(e.FullPath))
            {
                foreach (string file in Directory.GetFiles(e.FullPath))
                {
                    var eventArgs = new FileSystemEventArgs(
                        WatcherChangeTypes.Created,
                        System.IO.Path.GetDirectoryName(file),
                        System.IO.Path.GetFileName(file));
                    Watcher_Event(sender, eventArgs);
                }
            }
            else
            {
                if (!e.FullPath.Contains(".tmp.drivedownload")) Debug.WriteLine($"{ DateTime.Now.ToString("s") } : {e.ChangeType} - {e.FullPath}");
                Images.TrackChange(e.ChangeType, e.FullPath);
            }

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PlaySlideShow();
            timerImageChange.IsEnabled = true;
        }

          private void timerImageChange_Tick(object sender, EventArgs e)
        {
            PlaySlideShow();
        }

        private bool alternateImage;
        private void PlaySlideShow()
        {
            try
            {
                if (!Images.HasImages()) return;

                alternateImage = !alternateImage;

                var imageOne = alternateImage ? 0 : 1;
                var imageTwo = alternateImage ? 1 : 0;

                Image imgFadeOut = ImageControls[imageOne];
                Image imgFadeIn = ImageControls[imageTwo];

                ImageSource newSource = Images.NextImage();
                //lstImages.GetBindingExpression(ListBox.ItemsSourceProperty).UpdateTarget();
                imgFadeIn.Source = newSource;

                Storyboard StboardFadeOut = (Resources["FadeOut"] as Storyboard).Clone();
                StboardFadeOut.Begin(imgFadeOut);
                Storyboard StboardFadeIn = Resources["FadeIn"] as Storyboard;
                StboardFadeIn.Begin(imgFadeIn);
            }
            catch (Exception ex) { }
        }
    }

}
