using Newtonsoft.Json;
using System.ComponentModel;

namespace ImmersiveLights.Models
{
    public class SceneModel : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Title { get; set; }

        public string FileName { get; set; }

        [JsonIgnore]
        public string Image { get; set; }

        public bool CanDelete { get; set; }

        [JsonIgnore]
        public bool IsAddItem { get; set; }

        private bool _isActived = false;

        [JsonIgnore]
        public bool IsActived
        {
            get { return _isActived; }
            set
            {
                _isActived = value;
                RaisePropertyChanged("IsActived");
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}
