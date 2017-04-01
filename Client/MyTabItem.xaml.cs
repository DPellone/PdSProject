using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Client {
    /*
     * Nuovo controllo WPF che incapsula la logica per la gestione
     * e la visualizzazione di un singolo server
     */
    public partial class MyTabItem : UserControl {

        public InteractiveTabItem ContainerTab { get; private set; }

            // Socket connesso e stream per leggere e scrivere
        private TcpClient _socket;
        private NetworkStream _stream;

        private MySocketListener listener;
        private Thread ReceiveThread;

        private DateTime clientStart, lastPercUpdate;
        private System.Timers.Timer percentageTimer;
        
        public ObservableCollection<ApplicationItem> applications { get; }

        public TcpClient Connection {

            get { return _socket; }
            set { _socket = value; }
        }

        public NetworkStream Stream {

            get { return _stream; }
            set { _stream = value; }
        }

        /*
         * Costruttore che richiede come parametro il Tab che conterrà questo oggetto
         */
        public MyTabItem(InteractiveTabItem container) {

            InitializeComponent();

            ContainerTab = container;

                // Ogni MyTabItem si iscrive all'evento che si genera nella MultiMainWindow quando la finestra viene chiusa
            ContainerTab.MainWindow.ClosingEvent += atClosingTime;

                // Impostazione del timer per il calcolo delle percentuali ad intervalli di 1 secondo
            clientStart = lastPercUpdate = DateTime.Now;
            percentageTimer = new System.Timers.Timer(1000);
                // Timer impostato come periodico
            percentageTimer.AutoReset = true;
                // Funzione da richiamare allo scadere del timer
            percentageTimer.Elapsed += (obj, e) => {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
         new Action(() => { percentageUpdate(); }));
            };
            
            applications = new ObservableCollection<ApplicationItem>();

                // Binding della lista alla listView
            listView.ItemsSource = applications;

                // Abilitazione dell'accesso alla lista da parte di più thread
            BindingOperations.EnableCollectionSynchronization(applications, applications);
        }

        /*
         * Metodo per cominciare la raccolta di informazioni dal server
         * In caso di eccezione, vengono fatti due tentativi
         */
        public void startWork() {
            uint attempt = 2;
            while (attempt != 0) {
                try {

                        // Creazione di un thread secondario che riceve dati dal server e aggiorna la lista
                    listener = new MySocketListener(this);
                    ReceiveThread = new Thread(listener.ThreadFcn);

                    Console.WriteLine("Main thread: Call Start, to start ThreadFcn.");

                    ReceiveThread.IsBackground = true;
                    ReceiveThread.Start();

                    percentageTimer.Start();

                    Console.WriteLine("Main thread: Call Join(), to wait until ThreadFcn ends.");

                    attempt = 0;

                } catch (OutOfMemoryException) {
                    ExceptionHandler.MemoryError(attempt, this.ContainerTab.MainWindow);
                }
            }
        }

        /*
         * Metodo che ferma l'ascolto del server e libera le risorse occupate
         */
        public void atClosingTime() {
            listener.Stop();
            try {
                Connection.Client.Shutdown(SocketShutdown.Both);
            } catch (SocketException) {
                MessageBox.Show("Errore di connessione.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            } catch (ObjectDisposedException) {
                /* Il socket è già stato chiuso */
            } finally {
                ReceiveThread.Join();
            }
        }

        /*
         * Metodo invocato dall'evento CollectionChanged
         * Sposta la visualizzazione sull'ultimo elemento aggiunto nella lista di applicazioni
         */
        public void listView_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add) {
                listView.ScrollIntoView(listView.Items[listView.Items.Count - 1]);
            }
        }
        
        /*
         * Metodo richiamato ad ogni ciclo del timer e quando si riceve una modifica di cambio focus
         * Ricalcola le percentuali di tempo in foreground delle diverse applicazioni
         */
        public void percentageUpdate() {
            lock (applications) {
                TimeSpan lastTimeUpdate = DateTime.Now - lastPercUpdate;
                TimeSpan totalTimeOfExecution = DateTime.Now - clientStart;

                foreach (ApplicationItem app in applications) {
                    if (app.IsFocused)
                        app.TimeOfExecution += lastTimeUpdate;
                    try
                    {
                        app.Percentage = (int)(app.TimeOfExecution.TotalMilliseconds / totalTimeOfExecution.TotalMilliseconds * 100);
                    }
                    catch (DivideByZeroException) {
                        app.Percentage = 0;
                    }
                }

                lastPercUpdate = DateTime.Now;
            }

        }
    }
}
