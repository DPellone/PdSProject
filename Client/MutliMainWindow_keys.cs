using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Net;
using System.IO;

namespace Client {

    /*
     * Enumerazione per calcolare la combinazione di modificatori premuti
     */
    [Flags]
    public enum command_t : byte { none = 0, shift = 1, ctrl = 2, alt = 4 }

    public partial class MultiMainWindow : Window
    {
        
        private command_t modifier = command_t.none;
        private bool capturing = false;


        /*
         * Metodo eseguito allo scatenarsi dell'evento PreviewKeyDown
         * Cattura e registra i modificatori che vengono premuti
         * Cattura e invia i tasti che vengono premuti
         */
        private void Client_PreviewKeyDown(object sender, KeyEventArgs e) {
                // Il tasto ALT è un tasto di sistema
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
                // Switch per gestire la pressione dei modificatori
            switch (key) {

                case Key.LeftShift:
                case Key.RightShift:
                    modifier = modifier | command_t.shift;
                    shiftRect.Fill = new SolidColorBrush(Colors.DeepSkyBlue);
                    e.Handled = true;
                    break;

                case Key.LeftCtrl:
                case Key.RightCtrl:
                    modifier = modifier | command_t.ctrl;
                    ctrlRect.Fill = new SolidColorBrush(Colors.DeepSkyBlue);
                    e.Handled = true;
                    break;

                case Key.LeftAlt:
                case Key.RightAlt:
                    modifier = modifier | command_t.alt;
                    altRect.Fill = new SolidColorBrush(Colors.DeepSkyBlue);
                    e.Handled = true;
                    break;

                default:
                    break;
            }

                // Se l'evento non è stato gestito, si procede all'invio del tasto premuto
            if (!e.Handled) {
                    // Preparazione dei dati da inviare
                int convertedKey = KeyInterop.VirtualKeyFromKey(key);
                byte[] buffer = new byte[1 + sizeof(int)];
                buffer[0] = (byte)modifier;
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder(convertedKey)).CopyTo(buffer, 1);

                    // Ottenimento dell'app in foreground a cui vogliamo inviare i comandi
                ForegroundApp focusedApp = foregroundBox.SelectedItem as ForegroundApp;
                if (focusedApp == null) {
                    e.Handled = true;
                    return;
                }

                    // Ricerca dei tab aventi quest'app in foreground
                foreach (InteractiveTabItem tab in tabItems) {
                    if (tab.foregroundApp == focusedApp.Name) {
                        MyTabItem myTab = tab.Content as MyTabItem;
                        if (myTab != null)
                                // Invio asincrono dei dati
                            try {
                                myTab.Stream.BeginWrite(buffer, 0, 1 + sizeof(int), new AsyncCallback(SendToServer), myTab);
                            } catch(IOException) {
                                ExceptionHandler.SendError(myTab);
                            } catch (ObjectDisposedException) { /* Eccezione già gestita dal thread lettore */ }
                    }
                }
                e.Handled = true;
            }
        }

        /*
         * Metodo per la gestione della terminazione dell'invio
         */
        private void SendToServer(IAsyncResult ar) {
            MyTabItem myTab = (MyTabItem)ar.AsyncState;
            try {
            myTab.Stream.EndWrite(ar);
            } catch (IOException) {
                ExceptionHandler.SendError(myTab);
            } catch (ObjectDisposedException) { /* Eccezione già gestita dal thread lettore */ }
        }

        /*
         * Metodo eseguito allo scatenarsi dell'evento PreviewKeyUp
         * Cattura e registra i modificatori che vengono rilasciati
         */
        private void Client_PreviewKeyUp(object sender, KeyEventArgs e) {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            switch (key) {

                case Key.LeftShift:
                case Key.RightShift:
                    modifier = modifier & ~command_t.shift;
                    shiftRect.Fill = new SolidColorBrush(Colors.White);
                    e.Handled = true;
                    break;

                case Key.LeftCtrl:
                case Key.RightCtrl:
                    modifier = modifier & ~command_t.ctrl;
                    ctrlRect.Fill = new SolidColorBrush(Colors.White);
                    e.Handled = true;
                    break;

                case Key.LeftAlt:
                case Key.RightAlt:
                    modifier = modifier & ~command_t.alt;
                    altRect.Fill = new SolidColorBrush(Colors.White);
                    e.Handled = true;
                    break;

                default:
                    break;
            }
        }

        /*
         * Metodo eseguito allo scatenarsi dell'evento Checked del ToggleSwitch
         * Aggiunge agli eventi PreviewKeyDown e PreviewKeyUp le funzioni precedenti
         * Abilita la cattura e l'invio dei tasti e dei modificatori
         */
        private void CheckboxChecked(object sender, RoutedEventArgs e) {
            shiftRect.Fill = new SolidColorBrush(Colors.White);
            ctrlRect.Fill = new SolidColorBrush(Colors.White);
            altRect.Fill = new SolidColorBrush(Colors.White);
            modifier = command_t.none;
            this.PreviewKeyDown += Client_PreviewKeyDown;
            this.PreviewKeyUp += Client_PreviewKeyUp;
            capturing = true;
        }

        /*
        * Metodo eseguito allo scatenarsi dell'evento Unchecked del ToggleSwitch
        * Rimuove dagli eventi PreviewKeyDown e PreviewKeyUp le funzioni precedenti
        * Disabilita la cattura e l'invio dei tasti e dei modificatori
        */
        private void CheckBoxUnchecked(object sender, RoutedEventArgs e) {
            this.PreviewKeyDown -= Client_PreviewKeyDown;
            this.PreviewKeyUp -= Client_PreviewKeyUp;
            capturing = false;
        }
    }
}