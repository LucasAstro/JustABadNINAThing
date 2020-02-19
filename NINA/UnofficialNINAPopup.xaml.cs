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

namespace NINA
{
    /// <summary>
    /// Interaction logic for UnofficialNINAPopup.xaml
    /// </summary>
    public partial class UnofficialNINAPopup : Window
    {

        public UnofficialNINAPopup()
        {
            InitializeComponent();
         }

        private void DoYouAgreeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
