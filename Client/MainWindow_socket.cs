using System;
using System.Threading;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Net.Sockets;
using System.Net;
using System.Windows.Threading;
using System.Collections.Specialized;

namespace Client {

    public class SocketListener {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        extern static bool DestroyIcon(IntPtr handle);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        extern static IntPtr CreateIconFromResourceEx(IntPtr buffer, uint size, int isIcon, uint dwVer, int cx, int cy, uint flags);

        private volatile bool stop = false;
        private NetworkStream Stream;
        private MainWindow mainWindow;

        public SocketListener(MainWindow main) {
            mainWindow = main;
            Stream = mainWindow.Stream;
        }

        public void Stop() { stop = true; }

        //metodo eseguito da un thread in background per la ricezione dati dal socket
        public void ThreadFcn()            //la signature della funzione deve rispettare il tipo delegate
        {
            int nread = 0;
            Byte[] myReadBuffer = new Byte[1024];
            try {
                //Ricezione modifiche dal server
                while (!stop) {
                    Console.WriteLine("In attesa di ricevere dati dal server...");

                    //Ricezione tipo modifica e PID : u_short + DWORD

                    nread = Stream.Read(myReadBuffer, 0, sizeof(ushort));     //catturare IOException

                    if (nread == 0) {
                        Console.WriteLine("Connessione chiusa durante lettura");

                        break;
                    } else if (nread != sizeof(ushort)) {
                        Console.WriteLine("Read fallita");
                        //Il client chiude
                        mainWindow.atClosingTime();
                    }

                    //ha letto il tipo di modifica
                    ushort mod_convertito = BitConverter.ToUInt16(myReadBuffer, 0);
                    Console.WriteLine("mod_convertito: {0}", mod_convertito);
                    int mod = IPAddress.NetworkToHostOrder((short)mod_convertito);
                    Console.WriteLine("Tipo modifica: {0}", mod);

                    //Leggo il PID del processo
                    nread = Stream.Read(myReadBuffer, 0, sizeof(uint)); //leggo la dimensione di un DWORD(long in c++), uint (c#)

                    if (nread == 0) {
                        Console.WriteLine("Connessione chiusa durante lettura");

                        break;
                    } else if (nread != sizeof(uint)) {
                        Console.WriteLine("Read fallita");
                        mainWindow.atClosingTime();
                    }

                    //salvo il pid
                    uint pid = BitConverter.ToUInt32(myReadBuffer, 0);

                    Console.WriteLine("PID: {0}", pid);

                    switch (mod) {
                        case 0://ricevo lunghezza del nome dell'applicazione
                            nread = Stream.Read(myReadBuffer, 0, sizeof(int));

                            if (nread == 0) {
                                Console.WriteLine("Connessione chiusa durante lettura");

                                break;
                            } else if (nread != sizeof(int)) {
                                Console.WriteLine("Read fallita");
                                mainWindow.atClosingTime();
                            }

                            //salvataggio lunghezza del nome
                            int len_conv = BitConverter.ToInt32(myReadBuffer, 0);
                            Console.WriteLine("lunghezza convertita: {0}", len_conv);
                            int name_len = IPAddress.NetworkToHostOrder(len_conv);
                            Console.WriteLine("lunghezza nome: {0}", name_len);

                            Byte[] buffer_name = new Byte[name_len];

                            String app_name = String.Empty;

                            //Lettura del nome dal socket
                            nread = Stream.Read(buffer_name, 0, name_len);

                            if (nread == 0) {
                                Console.WriteLine("Connessione chiusa durante lettura");

                                break;
                            } else if (nread != name_len) {
                                Console.WriteLine("Read fallita");
                                mainWindow.atClosingTime();
                            }

                            //conversione in string
                            app_name = System.Text.UnicodeEncoding.Unicode.GetString(buffer_name);
                            app_name = app_name.Replace("\0", String.Empty);

                            Console.WriteLine("Nome dell'app: {0}", app_name);

                            //ricevo lunghezza dell'icona

                            nread = Stream.Read(myReadBuffer, 0, sizeof(int));

                            if (nread == 0) {
                                Console.WriteLine("Connessione chiusa durante lettura");

                                break;
                            } else if (nread != sizeof(int)) {
                                Console.WriteLine("Read fallita");
                                mainWindow.atClosingTime();
                            }

                            ApplicationItem app = new ApplicationItem(mainWindow.defaultIcon);
                            app.PID = pid;
                            app.Name = app_name;

                            //conversione icon_length in formato host
                            int icon_conv = BitConverter.ToInt32(myReadBuffer, 0);
                            int icon_len = IPAddress.NetworkToHostOrder(icon_conv);
                            Console.WriteLine("lunghezza icona convertita: {0}", icon_len);

                            //Sostituire l'icona di default
                            if (icon_len != 0) {
                                Console.WriteLine("Icona presente");

                                //Salvataggio icona
                                Byte[] buffer_ICON = new Byte[icon_len];
                                int tot = 0, toread = 1024;
                                while (tot != icon_len) {
                                    if (toread > icon_len - tot)
                                        toread = icon_len - tot;
                                    nread = Stream.Read(buffer_ICON, tot, toread);

                                    if (nread == 0) {
                                        Console.WriteLine("Connessione chiusa durante lettura");

                                        break;
                                    }
                                    tot += nread;

                                }
                                if (tot != icon_len) {
                                    Console.WriteLine("Read fallita: {0}", nread);
                                    mainWindow.atClosingTime();
                                }

                                unsafe
                                {
                                    fixed (byte* buffer = new byte[1048576]) {
                                        uint i = 0;
                                        foreach (byte item in buffer_ICON)
                                            buffer[i++] = item;
                                        IntPtr Hicon = CreateIconFromResourceEx((IntPtr)buffer, i, 1, 0x00030000, 48, 48, 0);
                                        BitmapFrame bitmap = BitmapFrame.Create(Imaging.CreateBitmapSourceFromHIcon(Hicon, new Int32Rect(0, 0, 48, 48), BitmapSizeOptions.FromEmptyOptions()));
                                        bitmap.Freeze();
                                        app.Icon = bitmap;
                                        DestroyIcon(Hicon);
                                    }

                                }

                            }

                            // aggiunta nuova applicazione e notifica del cambiamento nella lista
                            lock (MainWindow._syncLock) {
                                mainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                    new Action(() => { mainWindow.applications.Add(app); }));

                                mainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                    new Action(() => {
                                        mainWindow.listView_CollectionChanged(mainWindow.listView,
                                            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, app));
                                    }));
                            }

                            break;
                        case 1:
                            // Modifica REMOVE
                            Console.WriteLine("Modifica: Rimozione");

                            lock (MainWindow._syncLock) {
                                try {

                                    foreach (ApplicationItem item in mainWindow.applications) {
                                        if (item.PID == pid) {
                                            Console.WriteLine("Rimozione applicazione: {0}", item.Name);
                                            mainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                                new Action(() => { mainWindow.applications.Remove(item); }));
                                            break;
                                        }
                                    }
                                } catch (InvalidOperationException exc) {
                                    Console.WriteLine("Exception: {0}", exc.Message);
                                }
                            }
                            break;
                        case 2:
                            //MODIFICA CHANGE FOCUS
                            Console.WriteLine("Modifica: Change Focus");

                            lock (MainWindow._syncLock) {
                                try {
                                    foreach (ApplicationItem item in mainWindow.applications) {
                                        if (item.PID == pid) {
                                            Console.WriteLine("Pid: {0} - applicazione: {1}", pid, item.Name);
                                            mainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                            new Action(() => {
                                                item.IsFocused = true; mainWindow.listView.SelectedItem = item;
                                                int index = mainWindow.foregroundApps.IndexOf(new ForegroundApp(item.Name, 0));
                                                if (index != -1)
                                                    mainWindow.foregroundApps[index].Count++; //  Aggiornamento conteggio app
                                                else
                                                    mainWindow.foregroundApps.Add(new ForegroundApp(item.Name, 1));
                                            }));

                                        } else if(item.IsFocused){
                                            Console.WriteLine("NO FOCUS-Pid: {0} - applicazione: {1}", item.PID, item.Name);
                                            mainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                             new Action(() => {
                                                 item.IsFocused = false;
                                                 int index = mainWindow.foregroundApps.IndexOf(new ForegroundApp(item.Name, 0));
                                                 if(--mainWindow.foregroundApps[index].Count <= 0)
                                                     mainWindow.foregroundApps.RemoveAt(index);
                                             }));
                                        }
                                    }
                                } catch (InvalidOperationException exc) {
                                    Console.WriteLine("Exception: {0}", exc.Message);
                                }
                            }
                            mainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                             new Action(() => { mainWindow.percentageUpdate(null, null); }));
                            break;
                        default:
                            Console.WriteLine("Modifica sconosciuta");
                            break;
                    }//fine switch case

                }// fine while
                Console.WriteLine("Thread - terminata ricezione dati dal server");

            } catch (NullReferenceException e) {
                Console.WriteLine("Exception message: {0}", e.Message);

            } catch (ThreadAbortException e) {
                /*TODO: da modificare/controllare*/
                Console.WriteLine("Thread - caught ThreadAbortException - resetting.");
                Console.WriteLine("Exception message: {0}", e.Message);
                Thread.ResetAbort();
            } catch (IOException e) {
                Console.WriteLine("Thread - caught IOException - connection closed");
                Console.WriteLine("Exception message: {0}", e.Message);
            }
        }
    }

}