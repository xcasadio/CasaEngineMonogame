using System;
using System.ComponentModel;
using System.Windows;

namespace EditorWpf.Logs
{
    public class LogEntry
    {
        public DateTime DateTime { get; set; }

        public int Index { get; set; }

        public string Message { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            Application.Current.Dispatcher.BeginInvoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
