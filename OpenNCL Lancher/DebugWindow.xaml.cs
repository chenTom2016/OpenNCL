using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using OpenNCL_Lancher.Runtime;
using Windows.ApplicationModel.DataTransfer;

namespace OpenNCL_Lancher
{
    public sealed partial class DebugWindow : Window
    {
        private readonly DispatcherQueue _dispatch;
        private readonly ObservableCollection<DebugRow> _rows = new();
        private int _maxRows = 2000;

        public DebugWindow(string status)
        {
            InitializeComponent();
            _dispatch = DispatcherQueue;
            EventsList.ItemsSource = _rows;
            StatusLine.Text = status;

            BackendDebugHub.Event += OnEvent;
            Closed += (_, _) => BackendDebugHub.Event -= OnEvent;
        }

        private void OnEvent(BackendDebugEvent e)
        {
            _dispatch.TryEnqueue(() =>
            {
                var line = $"{e.Timestamp:HH:mm:ss.fff} [{e.Level}] {e.Category} | {e.Message}";
                _rows.Add(new DebugRow(line));
                while (_rows.Count > _maxRows) _rows.RemoveAt(0);
                if (_rows.Count > 0) EventsList.ScrollIntoView(_rows[^1]);
            });
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            _rows.Clear();
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var r in _rows) sb.AppendLine(r.Line);
            var p = new DataPackage();
            p.SetText(sb.ToString());
            Clipboard.SetContent(p);
        }
    }

    public sealed class DebugRow
    {
        public DebugRow(string line) { Line = line; }
        public string Line { get; }
    }
}

