using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Client {
    static class ExceptionHandler {

        static public void ReceiveConnectionError(MyTabItem item) {
            MessageBox.Show("Errore di connessione.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (item.ContainerTab.MainWindow.tabItems.Count == 1)
                item.ContainerTab.MainWindow.error = true;               // Per evitare di eseguire il codice di Window_closing
            item.ContainerTab.MainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { item.ContainerTab.MainWindow.CloseTab(item.ContainerTab); }));
        }

        static public void MemoryError(uint attempt, MultiMainWindow main) {
            if (attempt > 1) {
                attempt--;
                System.GC.Collect();
            } else {
                MessageBox.Show("Errore irreversibile di memoria.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                main.error = true;
                main.Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => { Application.Current.Shutdown(); }));
            }
        }

        static public void MemoryError(MultiMainWindow main) {
            MessageBox.Show("Errore irreversibile di memoria.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            main.error = true;
            main.Dispatcher.Invoke(DispatcherPriority.Send, new Action(() => { Application.Current.Shutdown(); }));
        }

        static public void SendError(MyTabItem item) {
            MessageBoxResult res = MessageBox.Show("Impossibile inviare il comando, vuoi chiudere la connessione a " + item.ContainerTab.Header as String + "?",
                "Attenzione", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
                item.ContainerTab.MainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { item.ContainerTab.MainWindow.CloseTab(item.ContainerTab); }));
        }

        static public void IntroConnectionError() {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MultiMainWindow)
                {
                    MultiMainWindow w = window as MultiMainWindow;
                    ExceptionHandler.ReceiveConnectionError(w.tabItems[w.tabItems.Count - 1].TabElement);
                    break;
                }
            }
        }
    }
}
