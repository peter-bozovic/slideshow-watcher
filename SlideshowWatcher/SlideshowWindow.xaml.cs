using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SlideshowWatcher
{
    /// <summary>
    /// Interaction logic for SlideshowWindow.xaml
    /// </summary>
    public partial class SlideshowWindow : Window
    {
        public SlideshowWindow()
        {
            InitializeComponent();
        }

        private bool fullScreen;

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (fullScreen)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
                fullScreen = false;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                fullScreen = true;
            }

        }

    }
}
