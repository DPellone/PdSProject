#pragma once

#include <string>
#include <exception>
#include <Windows.h>

#define dimShort sizeof(u_short)
#define dimWord sizeof(DWORD)

//Struct che contiene le inforamzioni su un'applicazione
struct ApplicationItem {
	std::wstring Name;		//Nome dell'applicazione
	std::wstring Exec_name;
};

//Tipo di modifica alla lista
enum modification_t { add, rem, chf, heartbeat };

//Struct che rappresenta una modifica alla lista
class Modification {
private:
	modification_t mod;
	DWORD pid;
	ApplicationItem app;

public:
	Modification(modification_t m, DWORD pid); // Costruttore di modifica change_focus o remove
	Modification(DWORD pid, ApplicationItem app); // Costruttore modifica add
	char * getSerializedMod(int& length);
	char * getSerializedName(int& length);
	char * getSerializedIcon(int& length);
};