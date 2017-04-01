using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Sockets;
using System.Net;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace Client {

    [Flags]
    public enum modifier_t : byte { none = 0, shift = 1, ctrl = 2, alt = 4 }

    public class KeyData {
        public modifier_t modifier;
        public int key;

        public KeyData(modifier_t m, int k) { modifier = m; key = k; }
    }

    public partial class MainWindow : Window {
        
        private modifier_t modifier = modifier_t.none;
        private bool capturing = false;

        private void Client_PreviewKeyDown(object sender, KeyEventArgs e) {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            switch (key) {

                case Key.LeftShift:
                case Key.RightShift:
                    modifier = modifier | modifier_t.shift;
                    shiftRect.Fill = new SolidColorBrush(Colors.DeepSkyBlue);
                    e.Handled = true;
                    break;

                case Key.LeftCtrl:
                case Key.RightCtrl:
                    modifier = modifier | modifier_t.ctrl;
                    ctrlRect.Fill = new SolidColorBrush(Colors.DeepSkyBlue);
                    e.Handled = true;
                    break;

                case Key.LeftAlt:
                case Key.RightAlt:
                    modifier = modifier | modifier_t.alt;
                    altRect.Fill = new SolidColorBrush(Colors.DeepSkyBlue);
                    e.Handled = true;
                    break;

                default:
                    break;
            }
            if (!e.Handled) {
                int convertedKey = KeyInterop.VirtualKeyFromKey(key);
                byte[] buffer = new byte[1 + sizeof(int)];
                buffer[0] = (byte)modifier;
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder(convertedKey)).CopyTo(buffer, 1);
                Stream.BeginWrite(buffer, 0, 1 + sizeof(int), new AsyncCallback(SendToServer), Stream);
                e.Handled = true;
            }
        }

        private void SendToServer(IAsyncResult ar) {
            NetworkStream netStream = (NetworkStream)ar.AsyncState;
            netStream.EndWrite(ar);
        }

        private void Client_PreviewKeyUp(object sender, KeyEventArgs e) {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            switch (key) {

                case Key.LeftShift:
                case Key.RightShift:
                    modifier = modifier & ~modifier_t.shift;
                    shiftRect.Fill = new SolidColorBrush(Colors.White);
                    e.Handled = true;
                    break;

                case Key.LeftCtrl:
                case Key.RightCtrl:
                    modifier = modifier & ~modifier_t.ctrl;
                    ctrlRect.Fill = new SolidColorBrush(Colors.White);
                    e.Handled = true;
                    break;

                case Key.LeftAlt:
                case Key.RightAlt:
                    modifier = modifier & ~modifier_t.alt;
                    altRect.Fill = new SolidColorBrush(Colors.White);
                    e.Handled = true;
                    break;

                default:
                    break;
            }
        }

        private void HorizontalToggleSwitch_Checked(object sender, RoutedEventArgs e) {
            shiftRect.Fill = new SolidColorBrush(Colors.White);
            ctrlRect.Fill = new SolidColorBrush(Colors.White);
            altRect.Fill = new SolidColorBrush(Colors.White);
            modifier = modifier_t.none;
            this.PreviewKeyDown += Client_PreviewKeyDown;
            this.PreviewKeyUp += Client_PreviewKeyUp;
            capturing = true;
        }

        private void HorizontalToggleSwitch_Unchecked(object sender, RoutedEventArgs e) {
            this.PreviewKeyDown -= Client_PreviewKeyDown;
            this.PreviewKeyUp -= Client_PreviewKeyUp;
            capturing = false;
        }
    }
}