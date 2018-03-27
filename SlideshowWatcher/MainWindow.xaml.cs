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
        private List<ImageSource> Images = new List<ImageSource>();
        private static string[] ValidImageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
        private static string[] TransitionEffects = new[] { "Fade" };
        private string TransitionType, strImagePath = "";
        private int CurrentSourceIndex, CurrentCtrlIndex, EffectIndex = 0, IntervalTimer = 1;

        private ImagesDb images = new ImagesDb();
        private FileSystemWatcher watcher;


        public MainWindow()
        {
            InitializeComponent();

            //Initialize Image control, Image directory path and Image timer.
            IntervalTimer = Convert.ToInt32(ConfigurationManager.AppSettings["IntervalTime"]);
            strImagePath = ConfigurationManager.AppSettings["ImagesPath"];
            ImageControls = new[] { myImage, myImage2 };

            images.LoadFromFolder(ConfigurationManager.AppSettings["ImagesPath"]);

            timerImageChange = new DispatcherTimer();
            timerImageChange.Interval = new TimeSpan(0, 0, IntervalTimer);
            timerImageChange.Tick += new EventHandler(timerImageChange_Tick);

            watcher = new FileSystemWatcher();
            watcher.Path = ConfigurationManager.AppSettings["ImagesPath"];
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size;
            watcher.Changed += Watcher_Event;
            watcher.Created += Watcher_Event;
            watcher.Deleted += Watcher_Event;
            watcher.Renamed += Watcher_Renamed;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (!e.FullPath.Contains(".tmp.drivedownload")) Debug.WriteLine($"{ DateTime.Now.ToString("s") } : {e.ChangeType} - {e.OldFullPath} => {e.FullPath}");
            images.TrackChange(e.ChangeType, e.FullPath, e.OldFullPath);
        }

        private void Watcher_Event(object sender, FileSystemEventArgs e)
        {
            if (!e.FullPath.Contains(".tmp.drivedownload")) Debug.WriteLine($"{ DateTime.Now.ToString("s") } : {e.ChangeType} - {e.FullPath}");
            images.TrackChange(e.ChangeType, e.FullPath);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PlaySlideShow();
            timerImageChange.IsEnabled = true;
        }

        private bool fullScreen;

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if(fullScreen)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
                fullScreen = false;
            } else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                fullScreen = true;
            }

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
                if (!images.HasImages()) return;

                alternateImage = !alternateImage;

                var imageOne = alternateImage ? 0 : 1;
                var imageTwo = alternateImage ? 1 : 0;

                Image imgFadeOut = ImageControls[imageOne];
                Image imgFadeIn = ImageControls[imageTwo];

                ImageSource newSource = images.NextImage();
                imgFadeIn.Source = newSource;

                TransitionType = TransitionEffects[EffectIndex].ToString();

                Storyboard StboardFadeOut = (Resources[string.Format("{0}Out", TransitionType.ToString())] as Storyboard).Clone();
                StboardFadeOut.Begin(imgFadeOut);
                Storyboard StboardFadeIn = Resources[string.Format("{0}In", TransitionType.ToString())] as Storyboard;
                StboardFadeIn.Begin(imgFadeIn);
            }
            catch (Exception ex) { }
        }
    }
}
