using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media;

namespace Client {

    /*
     * Classe che estende l'oggetto WPF TabItem per permettere la variazione
     * del titolo dinamicamente ed esporre le proprietà del server sottostante
     */
    public class InteractiveTabItem : TabItem, INotifyPropertyChanged {

            // Proprietà usata per notificare la variazione dell'header all'interfaccia
        public object NewHeader {
            get { return Header; }
            set {
                Header = value;
                NotifyPropertyChanged("Header");
            }
        }

        public MultiMainWindow MainWindow { get; private set; }

        public MyTabItem TabElement { get; set; }

        public String foregroundApp { get; set; }

        public string RemoteHost { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public InteractiveTabItem(MultiMainWindow main) { MainWindow = main; }

        /*
         * Metodo invocato alla variazione dell'header
         * Invoca l'evento PropertyChanged per notificare la modifica all'interfaccia
         */
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "") {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    /*
     * Classe usata per matenere le informazioni sulle app in foreground
     * Contiene il nome e il numero di server che hanno tale app in foreground
     */
    public class ForegroundApp {

        public ForegroundApp(string name, int count) {
            Name = name;
            Count = count;
        }

        /*
         * Override dell'operatore di uguaglianza
         * Vengono confrontati solo i nomi delle app e non il loro conteggio
         */
        public override bool Equals(object obj) {
            if (obj == null)
                return false;
            ForegroundApp p = obj as ForegroundApp;
            if ((object)p == null)
                return false;
            return Name == p.Name;
        }

        public string Name { get; set; }
        public int Count { get; set; }
    }

    /*
     * Classe che incapsula le informazioni sulle applicazioni
     * in esecuzione sui server
     * Implementa l'interfaccia INotifyPropertyChanged per permettere
     * l'aggiornamento dell'interfaccia in caso di cambio di focus
     * o di aggiornamento delle percentuali
     */
    public class ApplicationItem : INotifyPropertyChanged {

        private int _percentage = 0;
        private String _status = "In esecuzione";
        private bool _isFocused = false;

        public String Name { get; set; }
        public ImageSource Icon { get; set; }
        public uint PID { get; set; } = 0;
        public TimeSpan TimeOfExecution { get; set; } = new TimeSpan(0);

            // Proprietà usata per notificare la variazione della percentuale all'interfaccia
        public int Percentage {
            get { return _percentage; }
            set {
                if (value != _percentage) {
                    _percentage = value;
                    NotifyPropertyChanged();
                }
            }
        }
            // Proprietà usata per notificare la variazione dello status all'interfaccia
        public String Stato {
            get { return _status; }
            private set {
                if (value != _status) {
                    _status = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public bool IsFocused {
            get { return _isFocused; }
            set {
                if (value != _isFocused) {
                    _isFocused = value;
                    if (_isFocused)
                        Stato = "In foreground";
                    else
                        Stato = "In esecuzione";
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        
        public ApplicationItem(ImageSource defaultIcon) {
            Name = "Default Name";
            Icon = defaultIcon;
        }

        /*
         * Metodo invocato alla variazione dello status o della percentuale
         * Invoca l'evento PropertyChanged per notificare la modifica all'interfaccia
         */
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "") {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}