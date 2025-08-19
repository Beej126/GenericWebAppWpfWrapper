using System.Windows;
using System.Windows.Input;

namespace GenericWebAppWpfWrapper
{
    /// <summary>
    /// Interaction logic for UsageDialog.xaml
    /// </summary>
    public partial class UsageDialog : Window
    {
        public UsageDialog(string usageText)
        {
            InitializeComponent();
            UsageTextBlock.Text = usageText;
            
            // Set a reasonable maximum width based on screen size
            MaxWidth = SystemParameters.WorkArea.Width * 0.9;
            MinWidth = 400; // Ensure dialog isn't too narrow
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}