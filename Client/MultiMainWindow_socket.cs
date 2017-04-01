using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Net.Sockets;
using System.Net;
using System.Windows.Threading;
using System.Collections.Specialized;
using System.Threading;

namespace Client {

    /*
     * Classe che incapsula lo stream per lettura dal socket dei dati inviati dal server
     */
    public class MySocketListener {

            // Import delle funzioni della libreria di Windows per interpretare l'icona inviata dal server
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        extern static bool DestroyIcon(IntPtr handle);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        extern static IntPtr CreateIconFromResourceEx(IntPtr buffer, uint size, int isIcon, uint dwVer, int cx, int cy, uint flags);
        
        private volatile bool stop = false;
        private NetworkStream Stream;
        private MyTabItem item;

        /*
         * Costruttore della classe MySocketListener
         */
        public MySocketListener(MyTabItem main) {
            item = main;
            Stream = item.Stream;
        }

        /*
         * Funzione usata per interrompere il ciclo di lettura del thread sullo stream
         */
        public void Stop() { stop = true; }

        /*
         * Metodo eseguito da un thread in background per la ricezione dati dal socket
         * La signature della funzione deve rispettare il delegato ThreadStart
         */
        public void ThreadFcn()
        {
            int nread = 0;
            try {
                Byte[] myReadBuffer = new Byte[1024];

                    // Ricezione modifiche dal server
                while (!stop) {
                    Console.WriteLine("In attesa di ricevere dati dal server...");

                        // Ricezione tipo modifica: u_short
                    nread = Stream.Read(myReadBuffer, 0, sizeof(ushort));

                    if (!isReadCorrect(nread, sizeof(ushort)))
                        return;

                        // Conversione Network To Host Order
                    ushort mod_convertito = BitConverter.ToUInt16(myReadBuffer, 0);
                    int mod = IPAddress.NetworkToHostOrder((short)mod_convertito);
                    Console.WriteLine("Tipo modifica: {0}", mod);

                        // Ricezione PID del processo: DWORD
                        // La dimensione della DWORD è pari a quella di un uint in C#
                    nread = Stream.Read(myReadBuffer, 0, sizeof(uint));

                    if (!isReadCorrect(nread, sizeof(uint)))
                        return;
                    
                    uint pid = BitConverter.ToUInt32(myReadBuffer, 0);

                    Console.WriteLine("PID: {0}", pid);

                        // Switch sul tipo della modifica
                    switch (mod) {

                            // Caso 0: aggiunta di una nuova applicazione
                        case 0:
                                // Ricezione lunghezza del nome dell'applicazione
                            nread = Stream.Read(myReadBuffer, 0, sizeof(int));

                            if (!isReadCorrect(nread, sizeof(uint)))
                                return;
                            
                                // Conversione Network To Host Order
                            int len_conv = BitConverter.ToInt32(myReadBuffer, 0);
                            Console.WriteLine("lunghezza convertita: {0}", len_conv);
                            int name_len = IPAddress.NetworkToHostOrder(len_conv);
                            Console.WriteLine("lunghezza nome: {0}", name_len);

                            Byte[] buffer_name = new Byte[name_len];

                            String app_name = String.Empty;

                                // Ricezione del nome dell'applicazione
                            nread = Stream.Read(buffer_name, 0, name_len);

                            if (!isReadCorrect(nread, name_len))
                                return;
                            
                                // Conversione in String
                            try {
                                app_name = System.Text.UnicodeEncoding.Unicode.GetString(buffer_name);
                                app_name = app_name.Replace("\0", String.Empty);
                            } catch (ArgumentException) {
                                app_name = "Senza nome";
                            }

                            Console.WriteLine("Nome dell'app: {0}", app_name);

                                // Ricezione lunghezza dell'icona
                            nread = Stream.Read(myReadBuffer, 0, sizeof(int));

                            if (!isReadCorrect(nread, sizeof(uint)))
                                return;
                            
                                // Creazione dell'oggetto ApplicationItem con icona di default
                            ApplicationItem app = new ApplicationItem(item.ContainerTab.MainWindow.defaultIcon);
                            app.PID = pid;
                            app.Name = app_name;

                                // Conversione Network To Host Order
                            int icon_conv = BitConverter.ToInt32(myReadBuffer, 0);
                            int icon_len = IPAddress.NetworkToHostOrder(icon_conv);
                            Console.WriteLine("lunghezza icona convertita: {0}", icon_len);

                                // Se la dimensione dell'icona è valida, si sostituisce a quella di default
                            if (icon_len != 0 && icon_len < 1048576) {
                                Console.WriteLine("Icona presente");

                                    // Ricezione icona (in blocchi da 1024 byte)
                                Byte[] buffer_ICON = new Byte[icon_len];
                                int tot = 0, toread = 1024;
                                while (tot != icon_len) {
                                    if (toread > icon_len - tot)
                                        toread = icon_len - tot;
                                    nread = Stream.Read(buffer_ICON, tot, toread);

                                    if (nread == 0) {
                                        Console.WriteLine("Connessione chiusa durante lettura");
                                        return;
                                    }
                                    tot += nread;
                                }

                                if (tot != icon_len) {
                                    Console.WriteLine("Read fallita: {0}", nread);
                                    item.ContainerTab.MainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                                new Action(() => { item.ContainerTab.MainWindow.CloseTab(item.ContainerTab); }));
                                }

                                    // Codice unsafe perché si fa uso di puntatori
                                    // Lettura dell'icona tramite la funzione importata e conversione in bitmap WPF
                                unsafe { 
                                    fixed (byte* buffer = &buffer_ICON[0]) {
                                        IntPtr Hicon = CreateIconFromResourceEx((IntPtr)buffer, (uint)icon_len, 1, 0x00030000, 48, 48, 0);
                                        if (Hicon != null) {
                                            BitmapFrame bitmap = BitmapFrame.Create(Imaging.CreateBitmapSourceFromHIcon(Hicon, new Int32Rect(0, 0, 48, 48), BitmapSizeOptions.FromEmptyOptions()));
                                            if (bitmap.CanFreeze) {
                                                bitmap.Freeze();
                                                app.Icon = bitmap;
                                            }
                                            DestroyIcon(Hicon);
                                        }
                                    }
                                }

                            }

                                // Aggiunta nuova applicazione e notifica del cambiamento nella lista
                            
                            item.Dispatcher.Invoke(DispatcherPriority.Send,
                                new Action(() => { lock (item.applications) { item.applications.Add(app); } }));

                            item.Dispatcher.Invoke(DispatcherPriority.Send,
                                new Action(() => {
                                    item.listView_CollectionChanged(item.listView,
                                        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, app));
                                }));

                            break;

                            // Caso 1: rimozione di un'applicazione
                        case 1:
                            Console.WriteLine("Modifica: Rimozione");

                            // Rimozione dell'applicazione dalla lista
                            Monitor.Enter(item.applications);
                            foreach (ApplicationItem appItem in item.applications) {
                                if (appItem.PID == pid) {
                                    Console.WriteLine("Rimozione applicazione: {0}", appItem.Name);
                                    Monitor.Exit(item.applications);
                                    this.item.Dispatcher.Invoke(DispatcherPriority.Send,
                                        new Action(() => { lock(item.applications) { this.item.applications.Remove(appItem); } }));
                                    Monitor.Enter(item.applications);
                                    break;
                                }
                            }
                            Monitor.Exit(item.applications);
                            break;

                            // Caso 3: cambio di focus
                        case 2:
                            Console.WriteLine("Modifica: Change Focus");

                                // Pulizia della selezione precedente
                            this.item.ContainerTab.MainWindow.Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => { this.item.listView.SelectedItem = null; }));

                            
                                    // Applicazione che perde il focus
                            this.item.ContainerTab.MainWindow.Dispatcher.Invoke(DispatcherPriority.Send,
                                        new Action(() => {
                                            // Aggiornamento lista app in foreground
                                            int index = this.item.ContainerTab.MainWindow.foregroundApps.IndexOf(new ForegroundApp(item.ContainerTab.foregroundApp, 0));
                                            if (index != -1)
                                            {
                                                if (--this.item.ContainerTab.MainWindow.foregroundApps[index].Count <= 0)
                                                    this.item.ContainerTab.MainWindow.foregroundApps.RemoveAt(index);
                                            }
                                        }));
                            // Ricerca delle applicazioni coinvolte nel cambiamento
                            Monitor.Enter(item.applications);
                            foreach (ApplicationItem appItem in item.applications) {
                                    // Applicazione che guadagna il focus
                                if (appItem.PID == pid) {
                                    Console.WriteLine("Pid: {0} - applicazione: {1}", pid, appItem.Name);
                                    Monitor.Exit(item.applications);
                                    this.item.ContainerTab.MainWindow.Dispatcher.Invoke(DispatcherPriority.Send,
                                        new Action(() => {
                                            lock (item.applications) {
                                                // Evidenziazione elemento nella tab
                                                appItem.IsFocused = true; this.item.listView.SelectedItem = appItem;
                                                this.item.ContainerTab.foregroundApp = appItem.Name;
                                                // Aggiornamento lista delle app in foreground
                                                int index = this.item.ContainerTab.MainWindow.foregroundApps.IndexOf(new ForegroundApp(appItem.Name, 0));
                                                if (index != -1)
                                                    this.item.ContainerTab.MainWindow.foregroundApps[index].Count++;
                                                else {
                                                    ForegroundApp newapp = new ForegroundApp(appItem.Name, 1);
                                                    this.item.ContainerTab.MainWindow.foregroundApps.Add(newapp);
                                                    if (!this.item.ContainerTab.MainWindow.foregroundBox.IsEnabled)
                                                        this.item.ContainerTab.MainWindow.foregroundBox.SelectedItem = newapp;
                                                }
                                            }
                                        }));
                                    Monitor.Enter(item.applications);
                                } else if(appItem.IsFocused)
                                    appItem.IsFocused = false;
                            }
                            Monitor.Exit(item.applications);
                            // Aggiornamento delle percentuali
                            item.Dispatcher.Invoke(DispatcherPriority.Send,
                                             new Action(() => { item.percentageUpdate(); }));
                            break;

                        case 3:
                            break;
                        default:
                            Console.WriteLine("Modifica sconosciuta");
                            break;
                    }
                }
                Console.WriteLine("Thread - terminata ricezione dati dal server");
            } catch (NullReferenceException) {
                ExceptionHandler.ReceiveConnectionError(item);
            } catch (IOException) {
                ExceptionHandler.ReceiveConnectionError(item);
            } catch (ObjectDisposedException) {
                ExceptionHandler.ReceiveConnectionError(item);
            } catch (ArgumentOutOfRangeException) {
                ExceptionHandler.ReceiveConnectionError(item);
            } catch (OutOfMemoryException) {
                ExceptionHandler.MemoryError(item.ContainerTab.MainWindow);
            }
          }

        /*
         * Metodo per verificare la corretta lettura dei dati dallo Stream
         *  byteRead: numero di byte letti
         *  byteToRead: numero di byte che ci si aspettava di leggere
         */
        private bool isReadCorrect(int byteRead, int byteToRead) {
            if (byteRead == 0) {
                Console.WriteLine("Connessione chiusa durante lettura");
                return false;
            } else if (byteRead != byteToRead) {
                Console.WriteLine("Read fallita");
                MessageBox.Show("Server " + item.ContainerTab.Header as String + ": connessione interrotta.");
                item.ContainerTab.MainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                            new Action(() => { item.ContainerTab.MainWindow.CloseTab(item.ContainerTab); }));
                return false;
            }
            return true;
        }
    }

}