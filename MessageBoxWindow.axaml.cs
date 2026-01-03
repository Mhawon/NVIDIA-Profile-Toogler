using Avalonia.Controls;
using System.Threading.Tasks;

namespace NVIDIA_Profil_Toogler
{
    public partial class MessageBoxWindow : Window
    {
        // This constructor is for the designer.
        public MessageBoxWindow()
        {
            InitializeComponent();
        }

        private MessageBoxWindow(string title, string message)
        {
            InitializeComponent();
            Title = title;
            var messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock");
            if (messageTextBlock != null)
            {
                messageTextBlock.Text = message;
            }
            var okButton = this.FindControl<Button>("OkButton");
            if (okButton != null)
            {
                okButton.Click += (s, e) => Close();
            }
        }

        public static Task Show(Window parent, string title, string message)
        {
            var window = new MessageBoxWindow(title, message)
            {
                // This is a bit of a hack to get the theme from the parent
                RequestedThemeVariant = parent.RequestedThemeVariant
            };
            return window.ShowDialog(parent);
        }
    }
}
