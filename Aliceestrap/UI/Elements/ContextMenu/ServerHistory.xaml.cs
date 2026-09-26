using Aliceestrap.Integrations;
using Aliceestrap.UI.ViewModels.ContextMenu;

namespace Aliceestrap.UI.Elements.ContextMenu
{
    public partial class ServerHistory
    {
        public ServerHistory(ActivityWatcher watcher)
        {
            var viewModel = new ServerHistoryViewModel(watcher);

            viewModel.RequestCloseEvent += (_, _) => Close();

            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
