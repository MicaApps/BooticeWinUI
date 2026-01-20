using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BooticeWinUI.Models
{
    public class BcdEntry : INotifyPropertyChanged
    {
        private string _identifier;
        private string _description;
        private string _device;
        private string _path;
        private string _locale;

        public string Identifier
        {
            get => _identifier;
            set => SetProperty(ref _identifier, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string Device
        {
            get => _device;
            set => SetProperty(ref _device, value);
        }

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public string Locale
        {
            get => _locale;
            set => SetProperty(ref _locale, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
