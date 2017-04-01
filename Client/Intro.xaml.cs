using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Net.Sockets;
using System.Security;

namespace Client
{
    /*
     * Finestra di collegamento visualizzata all'avvio del client
     * e ogni volta che si vuole creare una nuova connessione
     */
    public partial class Intro : Window
    {
        private IAsyncResult connectionResult;
        private TcpClient client;

        /*
         * Struttura di utilità usata per passare le informazioni alla callback di connessione
         */
        private class Client_info {
            public TcpClient client { get; set; }
            public String indirizzo { get; set; }
        }

        public Intro() {
            InitializeComponent();
            IPTextBox1.Focus();
        }

        /*
         * Metodo invocato dall'evento PreviewTextInput delle TextBox
         * Previene l'inserimento di caratteri diversi da cifre
         */
        public void IsNumberAllowed(object sender, TextCompositionEventArgs e) {
            Regex regex = new Regex("[0-9]+");
            if (!regex.IsMatch(e.Text))
                e.Handled = true;
        }

        /*
         * Metodo invocato dall'evento GotFocus delle TextBox
         * Seleziona automaticamente tutto il contenuto quando queste vengono selezionate
         */
        private void SelectAll(object sender, RoutedEventArgs e) {
            if (e.Source.GetType() == typeof(TextBox))
                ((TextBox)e.Source).SelectAll();
        }

        /*
         * Metodo usato per intercettare e bloccare il comando Incolla nelle TextBox
         * Tale comando scavalca il controllo della funzione IsNumberAllowed
         */
        private void DisablePasteTextbox(Object sender, ExecutedRoutedEventArgs e) {
            if (e.Command == ApplicationCommands.Paste) {
                e.Handled = true;
            }
        }

        /*
         * Metodo invocato dall'evento LostFocus delle TextBox
         * Se la casella è vuota quando perde il focus, assume il valore 0
         */
        private void DefaultValueOnLostFocus(Object sender, RoutedEventArgs e) {
            if (e.Source is TextBox) {
                TextBox source = e.Source as TextBox;
                if (source.Text == "") {
                    source.Text = "0";
                }
            }
        }

        /*
         * Metodo che riabilita l'interfaccia in caso di connessione non riuscita
         * Altrimenti crea la MultiMainWindow (se non seiste già) aggiungendo la tab con le varie informazioni
         */
        private void requestConnection(IAsyncResult result) {
            Client_info info = result.AsyncState as Client_info;
            
                // Caso in cui la connessione non è andata a buon fine
            if ((info==null) || (!info.client.Connected)) {
                MessageBox.Show("Impossibile stabilire una connesione");
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => {
                        // Riabilitazione dell'interfaccia
                    ConnectionButton.IsEnabled= true;
                    IPTextBox1.IsEnabled = IPTextBox2.IsEnabled = IPTextBox3.IsEnabled = IPTextBox4.IsEnabled = true;
                    PortTextBox.IsEnabled = true;
                    this.Cursor = Cursors.Arrow;
                }));
                return;
            }

            NetworkStream stream = info.client.GetStream();
            stream.ReadTimeout = 5000;
            
                // Ricerca tra le finestre dell'applicazione di una MultiMainWindow.
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
            {
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is MultiMainWindow)
                    {
                            // Se esiste gà una MultiMainWindow, viene aggiunta una nuova Tab
                        MultiMainWindow w = window as MultiMainWindow;
                        w.addTab(info.client, stream, info.indirizzo);
                        w.connessioni_attive.Add(info.indirizzo);
                        this.Close();
                        return;
                    }
                }
                    // Altrimenti viene creata la MultiMainWindow
                MultiMainWindow main = new MultiMainWindow(info.client, stream, info.indirizzo);
                main.connessioni_attive.Add(info.indirizzo);
                this.Close();
                main.Show();
            }));

            try {
                info.client.EndConnect(result);
            } catch (SocketException) {
                    // In caso di errore sul socket la nuova Tab viene chiusa
                ExceptionHandler.IntroConnectionError();
            } catch (ObjectDisposedException) {
                    // In caso di socket chiuso la nuova Tab viene chiusa
                ExceptionHandler.IntroConnectionError();
            }
        }

        /*
         * Metodo invocato dall'evento Click del pulsante
         * Conversione dei parametri e avvio del tentativo di connessione
         */
        private void ConnectionClick(object sender, RoutedEventArgs e) {

            string indirizzo = IPTextBox1.Text + "." + IPTextBox2.Text + "." + IPTextBox3.Text + "." + IPTextBox4.Text;
            if (indirizzo == "0.0.0.0") {
                MessageBox.Show("Impossibile connettersi all'host specificato", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Int32 porta;
            try {
                porta = Convert.ToInt32(PortTextBox.Text);
            }
            catch (FormatException) {
                porta = 2000;
            }
            catch (OverflowException) {
                porta = 2000;
            }

                // Verifica dell'esistenza di una tab connessa allo stesso indirizzo
            foreach (Window window in System.Windows.Application.Current.Windows) {
                if (window is MultiMainWindow) {
                    MultiMainWindow m = window as MultiMainWindow;
                    if (m.connessioni_attive.Contains(indirizzo)) {
                        MessageBox.Show("Connessione già effettuata verso " + indirizzo);
                        return;
                    }
                }
            }

            Console.WriteLine("Connessione verso: {0} - {1}",indirizzo,porta);

            try {
                client = new TcpClient();
                Client_info info = new Client_info();
                info.client = client;
                info.indirizzo = indirizzo;
                    // Richiesta di connessione asincrona per non bloccare l'interfaccia
                this.connectionResult = client.BeginConnect(indirizzo,porta, new AsyncCallback(requestConnection), info);
                    // Disabilitazione dell'interfaccia per evitare più richieste contemporanee
                ConnectionButton.IsEnabled = false;
                IPTextBox1.IsEnabled = IPTextBox2.IsEnabled = IPTextBox3.IsEnabled = IPTextBox4.IsEnabled = false;
                PortTextBox.IsEnabled = false;
                this.Cursor = Cursors.AppStarting;
            }
            catch (SecurityException) {
                MessageBox.Show("Accesso negato: permessi non sufficienti", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            catch (ObjectDisposedException) {
                MessageBox.Show("Errore di connessione", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            catch (ArgumentOutOfRangeException) {
                MessageBox.Show("Numero di porta non valido, valori ammessi: 1-65536", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (SocketException) {
                MessageBox.Show("Impossibile stabilire una connesione", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
    }
}
