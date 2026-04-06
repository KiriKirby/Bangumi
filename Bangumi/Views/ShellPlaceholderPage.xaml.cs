using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Bangumi.Views
{
    public sealed partial class ShellPlaceholderPage : Page
    {
        public sealed class PlaceholderState
        {
            public string Title { get; set; }

            public string Description { get; set; }
        }

        public ShellPlaceholderPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is PlaceholderState state)
            {
                TitleTextBlock.Text = state.Title;
                DescriptionTextBlock.Text = state.Description;
            }
        }
    }
}
