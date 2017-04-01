using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Client {
    
        // Delegato per l'evento di chiusura della MultiMainWindow
    public delegate void ClosingHandler();

    /*
     * Classe della finestra principale dell'applicazione
     */
    public partial class MultiMainWindow : Window {

            // Lista di Tab dei server
        public ObservableCollection<InteractiveTabItem> tabItems { get; }
            // Icona di dafault per le applicazioni in lista
        public BitmapFrame defaultIcon { get; set; }
            // Lista delle app in foreground
        public ObservableCollection<ForegroundApp> foregroundApps { get; }
            // Lista degli indirizzi IP già connessi
        public List<string> connessioni_attive { get; set; }
            // Proprietà per la chiusura forzata della finestra in caso di errore
        public bool error { get; set; } = false;

            // Evento scatenato alla chiusura della MultiMainWindow
        public event ClosingHandler ClosingEvent;

        /*
         * Costruttore che inizializza la prima Tab
         */
        public MultiMainWindow(TcpClient client, NetworkStream stream, String indirizzo) {

            InitializeComponent();

                // Ottenimento dell'icona di default
            defaultIcon = BitmapFrame.Create(new Uri("pack://application:,,,/Resources/default.ico"));

            connessioni_attive = new List<string>();
            foregroundApps = new ObservableCollection<ForegroundApp>();
            foregroundBox.ItemsSource = foregroundApps;
            BindingOperations.EnableCollectionSynchronization(foregroundApps, foregroundApps);
            tabItems = new ObservableCollection<InteractiveTabItem>();
                // Creazione della prima Tab
            addTab(client, stream, indirizzo);

            tabControl.DataContext = tabItems;
        }

        /*
         * Metodo invocato dall'evento Closing
         * Se non è presente una condizione di errore, l'applicazione visualizza un pop-up di conferma
         * In entrambi i casi, le Tab vengono chiuse
         */
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (!error) {
                MessageBoxResult res = MessageBox.Show("Sei sicuro di voler terminare l'applicazione?\nTutte le connessioni verranno chiuse.", "Client - Chiusura", MessageBoxButton.YesNo);
                switch (res) {
                    case MessageBoxResult.Yes:
                            // Vengono invocati tutti gli atclosingtime delle tab
                        ClosingEvent();
                        break;

                    case MessageBoxResult.No:
                            // Annulamento della chiusura
                        e.Cancel = true;
                        break;
                }
            } else
                ClosingEvent();
        }

        /*
         * Metodo che chiude in maniera corretta una Tab
         */
        public void CloseTab(InteractiveTabItem tab) {
                // Chiusura della mainWindow se non ci sono più tab (richiamo window_close)
            if (tabItems.Count == 1) {
                this.Close();
                return;
            }

                // Rimozione della Tab
            MyTabItem mytab = tab.TabElement;
            ClosingEvent -= mytab.atClosingTime;
            mytab.atClosingTime();
            tabItems.Remove(tab);
            connessioni_attive.Remove(tab.RemoteHost);
                // Nel caso sia rimasta una sola tab, disattiviamo la scelta dell'app in foreground
            if (tabItems.Count == 1)
                foregroundBox.IsEnabled = false;
        }

        /*
         * Metodo invocato al click dell'opzione "Disconnetti"
         * Chiude la tab visualizzata
         */
        private void disconnect_Click(object sender, RoutedEventArgs e) {
            MyTabItem selected = tabControl.SelectedContent as MyTabItem;
            if(selected != null)
               CloseTab(selected.ContainerTab);
        }

        /*
         * Metodo per creare una nuova Tab e aggiungerla a quelle visualizzate
         */
        public void addTab(TcpClient client, NetworkStream stream, String indirizzo) {
            InteractiveTabItem first = new InteractiveTabItem(this);
            MyTabItem tab = new MyTabItem(first);
            first.TabElement = tab;
                // Impostazione header Tab con l'indirizzo IP
            first.NewHeader = first.RemoteHost = indirizzo;
                // Se l'indirizzo è di loopback, viene visualizzato nell'header
            if (indirizzo.StartsWith("127."))
                first.NewHeader = "Loopback";
            else
                    // Tentativo di risoluzione del nome host
                Dns.BeginGetHostEntry(indirizzo,
                    new AsyncCallback((IAsyncResult ar) => {
                        try {
                                // Se la risoluzione è riuscita, il nome host viene visualizzato nell'header
                            string hostName = Dns.EndGetHostEntry(ar).HostName;
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => { first.NewHeader = hostName; }));
                        } catch (SocketException) {
                                // Se non viene trovato un nome host, la tab mantiene l'indirizzo come header
                            Console.WriteLine("Server {0}: Nessun nome host trovato", indirizzo);
                        }
                    }), null);
            
            tab.Connection = client;
            tab.Stream = stream;
            tab.startWork();
            first.Content = tab;
                // Aggiunta della Tab tra quelle visualizzate
            tabItems.Add(first);
                // Viene evidenziata l'ultima tab creata
            tabControl.SelectedIndex = tabItems.Count-1;
                // Nel caso ci siano più tab, attiviamo la scelta dell'app in foreground tramite checkbox
            if (tabItems.Count > 1)
                foregroundBox.IsEnabled = true;
        }

        /*
         * Metodo invocato quando viene selezionata l'opzione "Connetti"
         */
        private void menu_F_conn_Click(object sender, RoutedEventArgs e) {
                // Quando si clicca connetti in Multimainwindow, viene aperta la Intro
            Intro intro = new Intro();
            intro.Show();
        }

        /*
         * Metodo invocato quando viene selezionata l'opzione "Esci"
         * Viene mostrato il popup per la chiusura della finestra
         */
        private void menu_F_esci_Click(object sender, RoutedEventArgs e) { this.Close(); }
    }

    
}
