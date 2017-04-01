#include "resource.h"
#include "ListManager.hpp"
#define PORT 2000
//per debugging della memoria
#define _CRTDBG_MAP_ALLOC 
#include <stdlib.h> 
#include <crtdbg.h>
//per wcout in main di prova
#include <fcntl.h>
#include <io.h>
#include <stdarg.h>

/*
* Server con 3 thread: uno per l'interfaccia, uno per l'invio della lista e l'altro per gestire i comandi
*/

HWND Hwnd;
HMENU Hmenu;
NOTIFYICONDATA notifyIconData;
TCHAR szTIP[64] = TEXT("Server - In Esecuzione");
TCHAR szClassName[] = TEXT("Server");
TCHAR szMessage[] = TEXT("Il server eseguirà in background.\nPer terminare fare click sull'icona nella tray area.");

LRESULT CALLBACK WindowProcedure(HWND, UINT, WPARAM, LPARAM);
int InitNotifyIconData();

int WINAPI WinMain(HINSTANCE hThisInstance, HINSTANCE hPrevInstance, LPSTR lpszArgument, int nCmdShow) {

	_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF); //per debug di memoria
	_setmode(_fileno(stdout), _O_U16TEXT); //per evitare problemi in wcout

	MSG message;            // messaggio ricevuto dalla coda dell'applicazione
	WNDCLASSEX wincl;        // Classe dell'applicazione

	wincl.hInstance = hThisInstance;
	wincl.lpszClassName = szClassName;
	wincl.lpfnWndProc = WindowProcedure;      //Procedura per la gestione dei messaggi
	wincl.style = CS_DBLCLKS;
	wincl.cbSize = sizeof(WNDCLASSEX);

	/* Uso delle icone e puntatore di default */
	if ((wincl.hIcon = LoadIcon(GetModuleHandle(NULL), MAKEINTRESOURCE(IDI_ICON1))) == NULL)
		return -1;
	if((wincl.hIconSm = LoadIcon(GetModuleHandle(NULL), MAKEINTRESOURCE(IDI_ICON1))) == NULL)
		return -1;
	if((wincl.hCursor = LoadCursor(NULL, IDC_ARROW)) == NULL)
		return -1;

	wincl.lpszMenuName = NULL;
	wincl.cbClsExtra = 0;
	wincl.cbWndExtra = 0;
	wincl.hbrBackground = (HBRUSH)(CreateSolidBrush(RGB(255, 255, 255))); //colore della finestra bianco

	if (!RegisterClassEx(&wincl))	// Registrazione della classe della nostra applicazione all'interno del s.o
		return -1;

	/* Creazione della finestra della nostra app che non visualizziamo */
	if ((Hwnd = CreateWindowEx(
		0,
		szClassName,			// Nome della finestra
		szClassName,			// Titolo
		WS_OVERLAPPEDWINDOW,	// Stile di default
		CW_USEDEFAULT,			// Posizione di default
		CW_USEDEFAULT,
		100,					// Dimensione 100x100
		100,
		HWND_DESKTOP,			// Collocata sul desktop
		NULL,
		hThisInstance,			// Mantenuta da questo programma
		NULL)) == NULL)
		return -1;
	/*
	* Inizializzazione dell'icona nella tray area
	*/
	if (InitNotifyIconData() != 0)
		return -1;
	if (!Shell_NotifyIcon(NIM_ADD, &notifyIconData))
		return -1;

	// Visualizzazione del box di dialogo alla partenza dell'applicazione
	MessageBox(Hwnd, szMessage, szClassName, MB_OK | MB_ICONINFORMATION);

	std::atomic_bool continua = true;
	/* Creazione del thread di lavoro */
	try {
		SocketStream socket(PORT);
		std::thread otherThread(func, std::ref(socket), std::ref(continua));
	

		/* Loop per estrarre i messaggi dalla coda. Se non ci sono messaggi si blocca.
		*  Termina il loop se riceve un messaggio di QUIT.
		*/
		BOOL bRet;
		while ((bRet=GetMessage(&message, NULL, 0, 0))!=0) {
			if (bRet == -1)
				break;
			TranslateMessage(&message);
			//Richiama la CALLBACK WindowProcedure passandole i contenuti del messaggio
			DispatchMessage(&message); 
		}
		/*while (GetMessage(&message, NULL, 0, 0)) {
			TranslateMessage(&message);
			DispatchMessage(&message);
		}*/

		if (message.wParam == -10) {
			continua = false;
			otherThread.join();
			throw socket_exception("Socket in secondary thread failed");
		}

		if (socket.getStatus() == true) {
			socket.closeConnection();
			socket.setStatus(false);
			continua = false;
			otherThread.join();
		}
		else
			otherThread.detach();
	}
	catch (socket_exception& e) {
		MessageBox(Hwnd, TEXT("Errore del socket"), szClassName, MB_OK | MB_ICONERROR);
		WSACleanup();
		return -1;
	}
	catch (std::system_error) {
		MessageBox(Hwnd, TEXT("Impossibile creare un nuovo thread"), szClassName, MB_OK | MB_ICONERROR);
		WSACleanup();
		return -1;
	}

	WSACleanup();
	return message.wParam;
}


/*  Funzione per la gestione dei messaggi di sistema
*   Params: hwnd(handle della finestra), message(evento che si è verificato), informazioni accessorie.
*/
LRESULT CALLBACK WindowProcedure(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) {

	/* Switch per la gestione dei messaggi */
	switch (message) {

	case WM_CREATE:		// Creazione del menu dell'icona

		ShowWindow(Hwnd, SW_HIDE);	//teniamo nascosta la finestra
		Hmenu = CreatePopupMenu();	//creiamo il contenuto del menu

		/* Il contenuto del menu è l'opzione exit di tipo string. Identifichiamo il click su questa opzione
		*  grazie al messaggio ID_TRAY_EXIT che abbiamo definito nel file resource.h
		*/
		if (!AppendMenu(Hmenu, MF_STRING, ID_TRAY_EXIT, TEXT("Exit"))) {
			MessageBox(Hwnd, TEXT("Impossibile caricare l'applicazione"), szClassName, MB_OK | MB_ICONERROR);
			Shell_NotifyIcon(NIM_DELETE, &notifyIconData);	//eliminiamo l'icona dalla tray area
			PostQuitMessage(-1);		//terminiamo l'applicazione con codice di errore
			return 0;
		}
		break;

	case WM_SYSCOMMAND:	// Caso in cui l'utente fa click nella finestra
		/* Per ottenere il risultato corretto dal parametro di WM_SYSCOMMAND, bisogna porre gli ultimi
		quattro bit a zero con una maschera, perchè sono usati dal sistema operativo */

		switch (wParam & 0xFFF0) {
			// Se la finestra dovesse comparire, premendo minimizza o chiudi la finestra viene fatta scomparire
		case SC_MINIMIZE:
		case SC_CLOSE:
			ShowWindow(Hwnd, SW_HIDE);
			return 0;
			break;
		}
		break;

	case WM_SYSICON: {	// Messaggio da parte dell'applicazione nella tray area: c'è stato un evento
						// Entriamo in questo "case" quando c'è un qualsiasi evento nella tray area.

		//Verifichiamo che l'evento interessa l'icona della nostra app
		if (wParam == ID_TRAY_APP_ICON)
			SetForegroundWindow(Hwnd);	//mettiamo la nostra applicazione in foreground

		//Il menu compare sia al click del tasto sinistro sia al click del tasto destro.
		if (lParam == WM_RBUTTONDOWN || lParam == WM_LBUTTONDOWN) {

			// Ottieni posizione corrente del mouse
			POINT curPoint;
			GetCursorPos(&curPoint);
			SetForegroundWindow(Hwnd);

			// Ottieni elemento del menu che è stato cliccato
			UINT clicked = TrackPopupMenu(Hmenu, TPM_RETURNCMD | TPM_NONOTIFY, curPoint.x, curPoint.y, 0, hwnd, NULL);

			SendMessage(hwnd, WM_NULL, 0, 0);	// Invio messaggio per far sparire il menu

			if (clicked == ID_TRAY_EXIT) {	// Se è stato cliccato Exit, elimina l'icona e invia messaggio di quit
				Shell_NotifyIcon(NIM_DELETE, &notifyIconData);
				PostQuitMessage(0);	//terminiamo l'applicazione con codice di uscita 0
			}
		}
	}
	break;

	case WM_NCHITTEST: {	// Cattura eventuali click nella finestra dell'applicazione e non li gestiamo

		UINT uHitTest = DefWindowProc(hwnd, WM_NCHITTEST, wParam, lParam);
		if (uHitTest == HTCLIENT)
			return HTCAPTION;
		else
			return uHitTest;
	}
	break;

	case WM_CLOSE:

		ShowWindow(Hwnd, SW_HIDE);	//nascondiamo la finestra
		return 0;
		break;

	case WM_DESTROY:

		PostQuitMessage(0);	//quando ad esempio dal task manager terminiamo l'applicazione
		break;

	}

	return DefWindowProc(hwnd, message, wParam, lParam);	//procedura che gestisce tutti i messaggi che non abbiamo gestito noi: ad esempio mostrare il menu al click.
}

/* Setup delle informazioni per l'icona */
int InitNotifyIconData()
{
	memset(&notifyIconData, 0, sizeof(NOTIFYICONDATA));

	notifyIconData.cbSize = sizeof(NOTIFYICONDATA);
	notifyIconData.hWnd = Hwnd;
	notifyIconData.uID = ID_TRAY_APP_ICON;
	notifyIconData.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
	notifyIconData.uCallbackMessage = WM_SYSICON;	//costante che usiamo nella WindowProcedure()
	if ((notifyIconData.hIcon = LoadIcon(GetModuleHandle(NULL), MAKEINTRESOURCE(IDI_ICON1))) == NULL)
		return -1;
	wcsncpy_s(notifyIconData.szTip, szTIP, sizeof(szTIP));	//assegnamo il messaggio da visualizzare al passaggio del mouse sull'icona
	return 0;
}