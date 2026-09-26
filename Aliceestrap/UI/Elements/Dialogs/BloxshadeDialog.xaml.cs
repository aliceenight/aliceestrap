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
using Aliceestrap.UI.ViewModels.Dialogs;
using Aliceestrap.UI.ViewModels.Installer;
using Wpf.Ui.Mvvm.Interfaces;

namespace Aliceestrap.UI.Elements.Dialogs
{
    public partial class BloxshadeDialog
    {
        public NextAction CloseAction = NextAction.Terminate;

        public BloxshadeDialog()
        {
            InitializeComponent();
        }

        public void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
