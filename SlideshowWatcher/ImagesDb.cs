using Newtonsoft.Json;
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
            private bool deleted;
            public bool Deleted {
                get { return deleted; }
                set {
                    deleted = value;
                    Changed(this, "ListName");
                    Changed(this, "Bold");
                    Changed(this, "Color");
                }
            }
            private bool exclude;
            public bool Exclude {
                get { return exclude; }
                set {
                    exclude = value;
                    Changed(this, "ListName");
                    Changed(this, "Bold");
                    Changed(this, "Color");
                }
            }
            public long DisplayCount { get; set; }
            private string tag;
            public string Tag {
                get { return tag; }
                set {
                    tag = value;
                    Changed(this, "ListName");
                    Changed(this, "Bold");
                    Changed(this, "Color");
                }
            }

            private bool current;
            [JsonIgnore]
            public bool Current {
                get { return current; }
                set {
                    current = value;
                    Changed(this, "ListName");
                    Changed(this, "Bold");
                    Changed(this, "Color");
                }
            }
            [JsonIgnore]
            public string ListName {
                get {
                    if (!string.IsNullOrEmpty(Tag)) return Tag;
                    return FileName;
                }
            }
            [JsonIgnore]
            public FontWeight Bold {
                get {

                    return Current ? FontWeights.Bold : FontWeights.Normal;
                }
            }
            [JsonIgnore]
            public Brush Color {
                get {
                    if (Deleted) return Brushes.DarkRed;
                    if (Exclude) return Brushes.LightGray;
                    return Brushes.Black;
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Changed(object sender, string propertyName)
            {
                PropertyChanged?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
            }
        }


        private const string jsonFilePath = @"d:\slideshow.json";
        public ImagesDb()
        {
            ImagesList = new ObservableCollection<ImageItem>();
            Limit = 20;
            var file = new FileInfo(jsonFilePath);
            if (file.Exists)
            {
                using (var reader = file.OpenText())
                {
                    string json = reader.ReadToEnd();
                    imagesCollection = JsonConvert.DeserializeObject<List<ImageItem>>(json);
                }
            }
        }

        private void SaveJson()
        {
            File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(imagesCollection, Formatting.Indented));
        }

        private static string[] ValidImageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
        private List<ImageItem> imagesCollection = new List<ImageItem>();

        public ObservableCollection<ImageItem> ImagesList { get; set; }
        public bool ShowDeleted { get; set; }
        public bool ShowExcluded { get; set; }
        public long Limit { get; set; }

        public void LoadFromFolder(string folder)
        {
            imagesCollection.AsParallel().ForAll(item => item.Deleted = true);
            var sw = Stopwatch.StartNew();
            if (!Path.IsPathRooted(folder))
                folder = Path.Combine(Environment.CurrentDirectory, folder);

            var files = new DirectoryInfo(folder).GetFiles("*", SearchOption.AllDirectories).AsParallel()
                .Where(o => ValidImageExtensions.Contains(o.Extension, StringComparer.InvariantCultureIgnoreCase) &&
                            !o.Attributes.HasFlag(FileAttributes.Hidden) && !o.Attributes.HasFlag(FileAttributes.System) && !o.Attributes.HasFlag(FileAttributes.Temporary))
                .ToList();

            foreach (var item in files)
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
            imagesCollection = imagesCollection.OrderBy(o => o.DisplayCount).ToList();
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
                        if ((ShowDeleted || !item.Deleted) && (ShowExcluded || !item.Exclude)) ImagesList.Add(item);
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

                            var validImages = GetValidImages();
                            var current = validImages[currentImage];
                            var currentIndex = imagesCollection.IndexOf(current);
                            imagesCollection.Remove(createdItem);
                            imagesCollection.Insert(currentIndex, createdItem);

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
                                renamedItem.Deleted = false;
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

        private List<ImageItem> GetValidImages()
        {
            return imagesCollection.AsParallel().Where(o => !o.Deleted && !o.Exclude).ToList();
        }

        public ImageSource NextImage()
        {
            var validImages = GetValidImages();

            if (currentImage >= validImages.Count || currentImage > Limit)
            {
                currentImage = 0;
                imagesCollection = imagesCollection.OrderBy(o => o.DisplayCount).ToList();
                validImages = GetValidImages();
                ReloadImagesList();
            }
            var image = validImages[currentImage];
            foreach (var item in imagesCollection.Where(o => o.Current))
            {
                item.Current = false;
            }
            image.Current = true;
            currentImage++;
            image.DisplayCount++;
            SaveJson();

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(image.FullPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public void SetAsNext(ImageItem image)
        {
            var validImages = GetValidImages();
            var current = validImages[currentImage];
            var currentIndex = imagesCollection.IndexOf(current);
            imagesCollection.Remove(image);
            imagesCollection.Insert(currentIndex, image);
            ReloadImagesList();
        }
    }
}
