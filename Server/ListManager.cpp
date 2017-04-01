#include "Listmanager.hpp"
#define MAXSTR 100
#define MAXEXT 10
#define pair std::pair<DWORD, ApplicationItem>
#define SHIFT 1
#define CTRL 2
#define ALT 4


/*
Funzione che viene richiamata per ogni finestra rilevata da EnumWindow()
Se la finestra non è visibile o se il suo nome è vuoto, ritorna subito;
altrimenti crea una struttura ApplicationItem con i parametri della finestra
e la inserisce nella lista passata come parametro
*/
BOOL CALLBACK MyWindowsProc(__in HWND hwnd, __in LPARAM lParam) {
	//Finestra non visibile
	if (!IsWindowVisible(hwnd))
		return TRUE;

	//Ottenimento del processo e verifica se il processo è già stato inserito
	DWORD proc; GetWindowThreadProcessId(hwnd, &proc);
	if (((std::map<DWORD, ApplicationItem>*)lParam)->find(proc) != ((std::map<DWORD, ApplicationItem>*)lParam)->end())
		return TRUE;

	//Altrimenti stiamo processando una nuova applicazione che non è gia in lista
	HANDLE process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, proc);
	if (process == NULL)
		return TRUE;

	//Ottenimento del percorso del processo
	TCHAR* file_name = new TCHAR[MAXSTR];
	DWORD maxstr = MAXSTR;
	if (QueryFullProcessImageName(process, 0, file_name, &maxstr) == 0 || maxstr >= MAXSTR) {
		CloseHandle(process);
		delete[] file_name;
		return TRUE;
	}

	ApplicationItem app;

	//Ottenimento del nome del processo
	TCHAR* buff = new TCHAR[MAXSTR + 1];
	TCHAR* ext = new TCHAR[MAXEXT + 1];
	// Prendiamo dal nome completo del file il nome dell'eseguibile
	if (splitpath(file_name, NULL, 0, NULL, 0, buff, MAXSTR, ext, MAXEXT) != 0) {
		delete[] ext; delete[] buff; delete[] file_name;
		CloseHandle(process);
		return TRUE;
	}

	app.Name = buff; app.Name += ext;
	app.Exec_name = file_name;

	delete[] buff; delete[] ext; delete[] file_name; 

	CloseHandle(process);

	//Salvataggio nella lista
	((std::map<DWORD, ApplicationItem>*)lParam)->insert(pair(proc, app));

	return TRUE;
}

/*
Creazione della lista da zero, semplicemente richiamando la funzione EnumWindows()
Alla funzione viene passata la callback e la lista
*/
void ListManager::buildList(std::map<DWORD, ApplicationItem>& list) {

	list.clear();
	// Per ogni applicazione in foreground eseguiamo la MyWindowsProc passando la lista delle app
	if (!EnumWindows(MyWindowsProc, (LPARAM)&list))
		throw std::runtime_error("Failed to enumerate windows!");
}

/*
Funzione principale della classe, eseguita dal thread che gestisce la lista.
Fino a che il programma non viene terminato, viene richiesta una
nuova lista di applicazioni ogni refreshTime millisecondi; questa lista viene confrontata con
quella del ListManager per determinare i programmi nuovi e quelli terminati, per
poi sostituire la vecchia lista.
*/
void ListManager::refreshList() {
	std::map<DWORD, ApplicationItem> list;
	DWORD newForeground = 0;
	int count = 0;

	//il ciclo è interrotto quando il client chiude la connessionessione

	while (socket.getStatus() == true) {
		count++;
		buildList(list);	//lista temporanea       

		for each (pair app in list) {
			std::map<DWORD, ApplicationItem>::iterator i = applications.find(app.first);
			if (i != applications.end()) {
				// Se l'applicazione esiste la cancello dalla lista vecchia
				applications.erase(i);
			}
			else {
				// Altrimenti c'è una nuova applicazione e quindi aggiungiamo la modifica di tipo add
				Modification m(app.first, app.second);
				modifications.push_back(m);
				count = 0;
			}
		}
		// Creiamo le modifiche di tipo remove per tutte le applicazioni terminate (non trovate nella lista nuova)
		for each (pair app in applications) {
			Modification m(rem, app.first);
			modifications.push_back(m);
			count = 0;
		}

		applications.swap(list);

		//vedo se è cambiata l'applicazione col focus
		GetWindowThreadProcessId(GetForegroundWindow(), &newForeground);

		if (newForeground != focusedApplication) {

			focusedApplication = newForeground;

			Modification m(chf, focusedApplication);
			modifications.push_back(m);
			count = 0;
		}

		if (count == 10) {
			count = 0;
			Modification m(heartbeat, 0);
			modifications.push_back(m);
		}

		//invio delle modifiche al client
		sendToClient();

		std::this_thread::sleep_for(std::chrono::milliseconds(refreshTime));

	}
}

void ListManager::setRefreshTime(unsigned long time) {
	if (time > 500 && time < 10000)
		refreshTime = time;
}

void ListManager::sendToClient() {

	if (modifications.empty()) return;

	char* send_buf = nullptr;
	int length = 0;
	bool noIcon = false;
	int nmod = modifications.size(); //numero di elementi in lista

	try {

		for each (Modification m in modifications) {
			//tipo+pid :info da inviare sempre per tutti i tipi di modifica
			if ((send_buf = m.getSerializedMod(length)) != nullptr) {

				//la prima informazione è fissa
				socket.sendData(send_buf, length);
				length = 0;
				free(send_buf);

			}

			//Modifica ADD:
			if ((send_buf = m.getSerializedName(length)) != nullptr) {
				//invio dim nome
				u_long length_net= htonl(u_long(length));
				
				socket.sendData(((char*)&length_net), sizeof(int));
				//invio il nome
				socket.sendData(send_buf, length);
				length = 0;
				free(send_buf);

				if ((send_buf = m.getSerializedIcon(length)) != nullptr) {
					//invio dim icona

					u_long length_net = htonl(u_long(length));

					socket.sendData(((char*)&length_net), sizeof(int));
					//invio icona
					socket.sendData(send_buf, length);
					length = 0;
					free(send_buf);
				}
				else {
					length = 0;
					//Se entra qua non è stato allocato nessun buffer => no free in caso di eccezione in sendData.
					noIcon = true;		
					socket.sendData(((char*)&length), sizeof(int));
				}

			}
		}
		//al termine dell'invio cancello la lista
		modifications.clear();

	}
	catch (std::overflow_error& e) {
		std::wcerr << e.what() << std::endl;
		// la memcpy_s fallisce dentro getSerializedName
		//forziamo la chiusura della connessione
		socket.closeConnection();
		modifications.clear();		//la lista è disponibile per altre connessioni

		//termina il metodo refreshlist
		socket.setStatus(false);
	}
	catch (socket_exception) {

	}
	catch (std::exception& e) {
		std::wcerr << e.what() << std::endl;
		
		/*TODO: rilasciare eventuali risorse quando la send fallisce*/
		if(!noIcon)
			free(send_buf);
		
		modifications.clear();		//lista disponibile per altre connessioni

		socket.setStatus(false);	//durante l'invio c'è stato un errore, il ciclo dentro refreshList termina.	
	}
}


void tmpThread(ListManager* manager, SocketStream* s) {
	char buffer[1 + sizeof(int)];
	INPUT input[8];
	int nOfInput = 0;

	INPUT shiftDown, ctrlDown, altDown, keyDown, keyUp, shiftUp, ctrlUp, altUp;

	shiftDown.type = ctrlDown.type = altDown.type = keyDown.type 
		= keyUp.type = shiftUp.type = ctrlUp.type = altUp.type = INPUT_KEYBOARD;
	shiftUp.ki.dwFlags = ctrlUp.ki.dwFlags = altUp.ki.dwFlags = keyUp.ki.dwFlags = KEYEVENTF_KEYUP;
	shiftDown.ki.dwFlags = ctrlDown.ki.dwFlags = altDown.ki.dwFlags = keyDown.ki.dwFlags = 0;
	shiftUp.ki.time = ctrlUp.ki.time = altUp.ki.time = keyUp.ki.time =
		shiftDown.ki.time = ctrlDown.ki.time = altDown.ki.time = keyDown.ki.time = 0;
	shiftUp.ki.dwExtraInfo = ctrlUp.ki.dwExtraInfo = altUp.ki.dwExtraInfo = keyUp.ki.dwExtraInfo =
		shiftDown.ki.dwExtraInfo = ctrlDown.ki.dwExtraInfo = altDown.ki.dwExtraInfo = keyDown.ki.dwExtraInfo = 0;
	shiftDown.ki.wVk = shiftUp.ki.wVk = VK_SHIFT;
	ctrlDown.ki.wVk = ctrlUp.ki.wVk = VK_CONTROL;
	altDown.ki.wVk = altUp.ki.wVk = VK_MENU;

	try {
		// Rimaniamo in attesa di comandi dal client fintanto che la connessione non viene chiusa
		while (s->receiveData(buffer, 1 + sizeof(int)) != 0) {
			//leggiamo il primo byte dal buffer che rappresenta il modificatore
			char modifier = buffer[0];
			//leggiamo il tasto premuto
			int key = ntohl(*((u_long*)&buffer[1]));
			std::wcout << "Input from client: " << key << ", modifier: " << (u_short)modifier << std::endl;

			nOfInput = 0;
			//nel buffer input salviamo i modificatori premuti
			if ((modifier & SHIFT) != 0)
				input[nOfInput++] = shiftDown;
			if ((modifier & CTRL) != 0)
				input[nOfInput++] = ctrlDown;
			if ((modifier & ALT) != 0)
				input[nOfInput++] = altDown;

			//concateniamo sia la pressione sia il rilascio del tasto
			keyDown.ki.wVk = keyUp.ki.wVk = key;
			input[nOfInput++] = keyDown;
			input[nOfInput++] = keyUp;

			//concateniamo i rilasci dei modificatori eventualmente premuti
			if ((modifier & SHIFT) != 0)
				input[nOfInput++] = shiftUp;
			if ((modifier & CTRL) != 0)
				input[nOfInput++] = ctrlUp;
			if ((modifier & ALT) != 0)
				input[nOfInput++] = altUp;

			// funzione che invia direttamente all'app in foreground un vettore con i modificatori selezionati
			int res = SendInput(nOfInput, input, sizeof(INPUT));
		}
	}
	catch (std::exception& e) {
		std::wcerr << "Client close connection: " << e.what() << std::endl;
	}
	s->setStatus(false);
}

void func(SocketStream& socket, std::atomic_bool& continua) {

	try {

		while (continua) {

			socket.waitingforConnection();	// in attesa di una connessione

			ListManager manager(socket);
			
			std::thread listener(tmpThread, &manager, &socket);		// thread che esegue la funzione di ricezione comandi

			std::wcout << "Start client service" << std::endl;

			manager.refreshList();		

			std::wcout << "Ending Client service routine" << std::endl;

			listener.join();		//quando il listener rileva il socket disconnesso termina

			socket.closeConnection(); 

		}

	}
	catch (socket_exception) {
		PostQuitMessage(-10);
	}
	catch (std::exception& e) {
		std::cerr << e.what() << std::endl;
	}
}