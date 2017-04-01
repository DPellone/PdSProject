#pragma once

#include "SocketStream.hpp"
#include <Windows.h>
#include <thread>
#include <memory>
#include <mutex>
#include <deque>
#include <vector>
#include <map>
#include <iostream>
#include <psapi.h>
#include "Application.hpp"
#include <system_error>

//Classe che gestisce la lista delle applicazioni
class ListManager {
private:

	unsigned long refreshTime;							//Tempo di refresh della lista
	std::map<DWORD, ApplicationItem> applications;		//Lista delle applicazioni indicizzata per pid
	DWORD focusedApplication = 0;						//Pid dell'applicazione in foreground
	std::deque<Modification> modifications;				//Puntatore alla lista delle modifiche
	SocketStream& socket;
	void sendToClient();

public:
	void buildList(std::map<DWORD, ApplicationItem>& list);
	void refreshList();
	void setRefreshTime(unsigned long time);
	ListManager(SocketStream& s, unsigned long refreshTime = 100) : socket(s), refreshTime(refreshTime) {}
};

void func(SocketStream& socket, std::atomic_bool& continua);

#ifdef UNICODE

#define splitpath _wsplitpath_s

#else

#define splitpath _splitpath_s

#endif
