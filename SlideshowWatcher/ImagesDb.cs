using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SlideshowWatcher
{

    public class ImagesDb
    {

        public class ImageItem : INotifyPropertyChanged
        {
            public string FileName { get; set; }
            public string FullPath { get; set; }
            public long Size { get; set; }
            public bool Deleted { get; set; }
            private bool current;
            public bool Current {
                get { return current; }
                set {
                    current = value;
                    Changed(this, "ListName");
                    Changed(this, "Bold");
                    Changed(this, "Color");
                }
            }
            public string ListName {
                get {
                    return FileName;
                }
            }
            public FontWeight Bold {
                get {

                    return Current ? FontWeights.Bold : FontWeights.Normal;
                }
            }
            public Brush Color {
                get {
                    return Deleted ? Brushes.LightGray : Brushes.Black;
                }
            }


            public event PropertyChangedEventHandler PropertyChanged;
            private void Changed(object sender, string propertyName)
            {
                PropertyChanged?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
            }
        }

        public ImagesDb()
        {
            ImagesList = new ObservableCollection<ImageItem>();
        }

        private static string[] ValidImageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
        private List<ImageItem> imagesCollection = new List<ImageItem>();

        public ObservableCollection<ImageItem> ImagesList { get; set; }
        public bool ShowDeleted {get; set; }

        public void LoadFromFolder(string folder)
        {
            var sw = Stopwatch.StartNew();
            if (!Path.IsPathRooted(folder))
                folder = Path.Combine(Environment.CurrentDirectory, folder);

            var files = new DirectoryInfo(folder).GetFiles().AsParallel()
                .Where(o => ValidImageExtensions.Contains(o.Extension, StringComparer.InvariantCultureIgnoreCase))
                .ToList();

            foreach(var item in files)
            {
                var imageItem = imagesCollection.AsParallel().Where(o => o.FileName == item.Name && o.Size == item.Length).FirstOrDefault();
                if (imageItem == null)
                {
                    imageItem = new ImageItem();
                    imageItem.FileName = item.Name;
                    imageItem.Size = item.Length;
                    imagesCollection.Add(imageItem);
                }
                imageItem.FullPath = item.FullName;
                imageItem.Deleted = false;
            }
            sw.Stop();
            Debug.WriteLine("Loaded form disk {0} images in {1}ms", files.Count, sw.ElapsedMilliseconds);
            ReloadImagesList();
        }

        public void ReloadImagesList()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (imagesCollection)
                {
                    ImagesList.Clear();
                    foreach (var item in imagesCollection)
                    {
                        if (ShowDeleted || !item.Deleted) ImagesList.Add(item);
                    }
                }
            }));
        }

        public void TrackChange(WatcherChangeTypes changeType, string fileName, string oldFileName = null)
        {
            var fileInfo = new FileInfo(fileName);
            if (fileInfo == null || (changeType == WatcherChangeTypes.Deleted && fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                || (ValidImageExtensions.Contains(fileInfo.Extension, StringComparer.InvariantCultureIgnoreCase) && !fileInfo.Attributes.HasFlag(FileAttributes.Hidden)))
            {
                lock (imagesCollection)
                {
                    switch (changeType)
                    {
                        case WatcherChangeTypes.Created:
                            var createdItem = imagesCollection.AsParallel().Where(o => o.FileName == fileInfo.Name && o.Size == fileInfo.Length).FirstOrDefault();
                            if (createdItem == null)
                            {
                                createdItem = new ImageItem();
                                createdItem.FileName = fileInfo.Name;
                                createdItem.Size = fileInfo.Length;
                                imagesCollection.Add(createdItem);
                            }
                            createdItem.FullPath = fileInfo.FullName;
                            createdItem.Deleted = false;
                            break;
                        case WatcherChangeTypes.Deleted:
                            if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                            {
                                imagesCollection.AsParallel().Where(o => o.FullPath.StartsWith(fileName, StringComparison.OrdinalIgnoreCase))
                                    .ForAll(item =>
                                    {
                                        item.Deleted = true;
                                    });
                            }
                            else
                            {
                                var deletedItem = imagesCollection.AsParallel().Where(o => o.FullPath == fileName).FirstOrDefault();
                                if (deletedItem != null)
                                {
                                    deletedItem.Deleted = true;
                                }
                            }
                            break;
                        case WatcherChangeTypes.Changed:
                            var changedItem = imagesCollection.AsParallel().Where(o => o.FullPath == fileInfo.FullName).FirstOrDefault();
                            if (changedItem == null)
                            {
                                changedItem = imagesCollection.AsParallel().Where(o => o.FileName == fileInfo.Name && o.Size == fileInfo.Length).FirstOrDefault();
                                if (changedItem == null)
                                {
                                    changedItem = new ImageItem();
                                    changedItem.FileName = fileInfo.Name;
                                    changedItem.FullPath = fileInfo.FullName;
                                    changedItem.Size = fileInfo.Length;
                                    imagesCollection.Add(changedItem);
                                }
                                else
                                {
                                    changedItem.FullPath = fileInfo.FullName;
                                }
                            }
                            else
                            {
                                changedItem.FileName = fileInfo.Name;
                                changedItem.Size = fileInfo.Length;
                            }
                            changedItem.Deleted = false;
                            break;
                        case WatcherChangeTypes.Renamed:
                            var renamedItem = imagesCollection.AsParallel().Where(o => o.FullPath == oldFileName).FirstOrDefault();
                            if (renamedItem != null)
                            {
                                renamedItem.FileName = fileInfo.Name;
                                renamedItem.FullPath = fileInfo.FullName;
                                renamedItem.Size = fileInfo.Length;
                            }
                            break;
                        case WatcherChangeTypes.All:
                            break;
                        default:
                            break;
                    }
                }
            }
            ReloadImagesList();
        }

        public bool HasImages()
        {
            return imagesCollection.AsParallel().Where(o => !o.Deleted).Count() > 0;
        }

        private int currentImage;

        public ImageSource NextImage()
        {
            var validImages = imagesCollection.AsParallel().Where(o => !o.Deleted).ToList();

            if (currentImage >= validImages.Count)
            {
                currentImage = 0;
            }
            var image = validImages[currentImage];
            foreach (var item in imagesCollection.Where(o => o.Current)) {
                item.Current = false;
            }
            image.Current = true;
            currentImage++;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(image.FullPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
