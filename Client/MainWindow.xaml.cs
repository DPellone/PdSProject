
using System;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Net.Sockets;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using System.Windows.Threading;
using System.Collections.Generic;

namespace Client {
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private TcpClient _socket;       //rappresenta il socket connesso
        private NetworkStream _stream;   //stream per leggere e scrivere

        private SocketListener listener;
        private Thread ReceiveThread;

        private DateTime clientStart, lastPercUpdate;
        private System.Timers.Timer percentageTimer;

        public BitmapFrame defaultIcon { get; set; }            //icona di default per le applicazioni in lista
        public ObservableCollection<ApplicationItem> applications { get; }//lista delle applicazioni
        public ObservableCollection<ForegroundApp> foregroundApps { get; }

        public TcpClient Connection{   //proprieta settata dalla finestra Intro

            get { return _socket; }
            set { _socket = value; }
        }

        public NetworkStream Stream{   //proprieta settata dalla finestra Intro

            get { return _stream; }
            set { _stream = value; }
        }

        public static object _syncLock { get; set; } = new object();

        public MainWindow() {

            InitializeComponent();

            clientStart = lastPercUpdate = DateTime.Now;
            percentageTimer = new System.Timers.Timer(1000);
            percentageTimer.AutoReset = true;
            percentageTimer.Elapsed += (obj, e) => {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
         new Action(() => { percentageUpdate(null, null); }));
            };

            //Ottenimento dell'icona di default
            defaultIcon = BitmapFrame.Create(new Uri("pack://application:,,,/Resources/default.ico"));

            /* TEST DI VISUALIZZAZIONE DELLA LISTA*/
            applications = new ObservableCollection<ApplicationItem>();
            foregroundApps = new ObservableCollection<ForegroundApp>();
            
            //Binding della lista alla listView
            listView.ItemsSource = applications;
            foregroundBox.ItemsSource = foregroundApps;

            BindingOperations.EnableCollectionSynchronization(applications, _syncLock);
            BindingOperations.EnableCollectionSynchronization(foregroundApps, _syncLock);
        }

        public void startWork() {
            try {
                //Creazione di un thread secondario che riceve dati dal server e aggiorna la lista
                listener = new SocketListener(this);
                ReceiveThread = new Thread(listener.ThreadFcn);
                
                Console.WriteLine("Main thread: Call Start, to start ThreadFcn.");

                ReceiveThread.Name = "Listener";
                ReceiveThread.IsBackground = true;
                ReceiveThread.Start();

                percentageTimer.Start();

                Console.WriteLine("Main thread: Call Join(), to wait until ThreadFcn ends.");

                //ReceiveThread.Join();

                //this.Close();

            } catch (ThreadStateException e) {
                /*TODO: da modificare/controllare*/
                Console.WriteLine("Exception: {0}", e.Message);
            }
        }

        public void atClosingTime() {

            listener.Stop();
            Connection.Client.Shutdown(SocketShutdown.Both);
            //Stream.Dispose();
            ReceiveThread.Join();
            Intro winIntro = new Intro();
            winIntro.Show();
            this.Close();

        }

        public void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        //scroll della lista sempre all'ultimo elemento
        public void listView_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //quando vengono aggiunte nuove applicazioni
            if (e.Action == NotifyCollectionChangedAction.Add)
            {

                listView.ScrollIntoView(listView.Items[listView.Items.Count - 1]);
            }
        }

        private void Client_Closed(object sender, EventArgs e) {
            atClosingTime();
        }

        public void percentageUpdate(object source, System.Timers.ElapsedEventArgs e) {
            lock (_syncLock) {
                TimeSpan lastTimeUpdate = DateTime.Now - lastPercUpdate;
                TimeSpan totalTimeOfExecution = DateTime.Now - clientStart;

                foreach (ApplicationItem app in applications) {
                    if (app.IsFocused)
                        app.TimeOfExecution += lastTimeUpdate;
                    app.Percentage = (int)(app.TimeOfExecution.TotalMilliseconds / totalTimeOfExecution.TotalMilliseconds * 100);
                }

                lastPercUpdate = DateTime.Now;
            }

        }
    }

    public class ForegroundApp {
        private int v;

        public ForegroundApp(string name, int v) {
            Name = name;
            this.v = v;
        }

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

    public class ApplicationItem : INotifyPropertyChanged {

        private int _percentage = 0;
        private String _status = "In esecuzione";
        private bool _isFocused = false;

        public String Name { get; set; }
        public ImageSource Icon { get; set; }
        public uint PID { get; set; } = 0;
        public TimeSpan TimeOfExecution { get; set; } = new TimeSpan(0);

        public int Percentage {
            get { return _percentage; }
            set {
                if (value != _percentage) {
                    _percentage = value;
                    NotifyPropertyChanged();
                }
            }
        }
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

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "") {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
