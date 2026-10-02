using System.Windows;
using System.Windows.Controls;

using Aliceestrap.UI.Elements.Controls;

namespace Aliceestrap.UI.Elements.Dialogs
{
    public partial class UpdateDialog
    {
        public bool Accepted { get; private set; } = false;

        public UpdateDialog(IReadOnlyList<GithubRelease> releases)
        {
            InitializeComponent();

            SubtitleText.Text = String.Format(Strings.Dialog_Update_Subtitle, releases[0].TagName, $"v{App.Version}");

            if (releases.Count == 1)
            {
                AddNotes(releases[0].Body);
            }
            else
            {
                foreach (var release in releases)
                {
                    var header = new TextBlock
                    {
                        Text = release.TagName,
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, NotesPanel.Children.Count == 0 ? 0 : 10, 0, 5)
                    };
                    header.SetResourceReference(TextBlock.ForegroundProperty, "ShellGlowBrush");

                    NotesPanel.Children.Add(header);

                    AddNotes(release.Body);
                }
            }

            Loaded += delegate { UpdateButton.Focus(); };
        }

        private void AddNotes(string? body)
        {
            var lines = (body ?? "")
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            if (lines.Count == 0)
            {
                var empty = MakeText(Strings.Dialog_Update_NoNotes, secondary: true);
                empty.Margin = new Thickness(0, 0, 0, 5);
                NotesPanel.Children.Add(empty);
                return;
            }

            foreach (string line in lines)
            {
                if (line.StartsWith('#'))
                {
                    var heading = MakeText(line.TrimStart('#').Trim());
                    heading.FontWeight = FontWeights.SemiBold;
                    heading.Margin = new Thickness(0, NotesPanel.Children.Count == 0 ? 0 : 6, 0, 4);
                    NotesPanel.Children.Add(heading);
                }
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition());

                    var bullet = new TextBlock { Text = "•", Margin = new Thickness(2, 0, 9, 0), FontSize = 13 };
                    bullet.SetResourceReference(TextBlock.ForegroundProperty, "ShellGlowBrush");

                    var text = MakeText(line[2..].Trim());
                    Grid.SetColumn(text, 1);

                    row.Children.Add(bullet);
                    row.Children.Add(text);
                    NotesPanel.Children.Add(row);
                }
                else
                {
                    var text = MakeText(line);
                    text.Margin = new Thickness(0, 0, 0, 5);
                    NotesPanel.Children.Add(text);
                }
            }
        }

        private static MarkdownTextBlock MakeText(string markdown, bool secondary = false)
        {
            var text = new MarkdownTextBlock
            {
                MarkdownText = markdown,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            };

            text.SetResourceReference(TextBlock.ForegroundProperty, secondary ? "TextFillColorSecondaryBrush" : "TextFillColorPrimaryBrush");

            return text;
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            Accepted = true;
            Close();
        }

        private void Later_Click(object sender, RoutedEventArgs e) => Close();
    }
}
