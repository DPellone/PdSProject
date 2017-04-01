#pragma comment(lib, "Ws2_32.lib")
#include "Application.hpp"

Modification::Modification(modification_t m, DWORD pid) : mod(m), pid(pid){
	if (mod == add)
		throw std::invalid_argument("Application to add not provided");
}

Modification::Modification(DWORD pid, ApplicationItem app) : mod(add), pid(pid), app(app){}

char * Modification::getSerializedMod(int& length) {
	length = dimShort + dimWord;

	char * buff = (char *) malloc(length);
	if (buff == NULL)
		throw std::bad_alloc();

	*(u_short*)buff = htons(u_short(mod));
	*(PDWORD)(buff + dimShort) = pid;

	return buff;
}

char * Modification::getSerializedIcon(int & length) {
	if(mod != add)
		return nullptr;
	HRSRC res = NULL;

	LPTSTR groupIconName = NULL;
	/* Funzione che ci permette di caricare nello spazio di memoria del nostro processo
	*	l'eseguibile dell'applicazione in formato binario per recuperare.
	*/
	HMODULE hExe = LoadLibraryEx(app.Exec_name.c_str(), NULL, LOAD_LIBRARY_AS_DATAFILE);
	if (hExe != NULL) {
		//per ogni risorsa di tipo RT_GROUP_ICON
		EnumResourceNames(hExe, RT_GROUP_ICON, [](HMODULE hModule, LPCTSTR lpszType, LPTSTR lpszName, LONG_PTR lParam)-> BOOL {
			// salviamo la prima risorsa disponibile
			if (lpszName != NULL) {
				LPTSTR* name = (LPTSTR*)lParam;
				*name = lpszName;
				// appena la troviamo interrompiamo la funzione EnumResourceNames
				return FALSE;
			}
			return TRUE;
		}, (LONG_PTR)&groupIconName);

		res = FindResource(hExe, groupIconName, RT_GROUP_ICON);
	}

	// Se non riusciamo a caricare la libreria o non troviamo un gruppo_icona valido verrà caricata sul client l'icona di default
	if (hExe == NULL || res == NULL) {
		// rilasciamo la libreria
		FreeLibrary(hExe);
		return nullptr;
	}

	length = SizeofResource(hExe, res);
	if (length == 0) {
		FreeLibrary(hExe);
		return nullptr;
	}

	HGLOBAL resptr = LoadResource(hExe, res);
	if (resptr == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}

	LPVOID icon = LockResource(resptr);
	if (icon == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}

	// Cerchiamo l'id dal gruppo_icona di un'icona di queste dimensioni
	int idIcon = LookupIconIdFromDirectoryEx((PBYTE)icon, TRUE, 48, 48, LR_DEFAULTCOLOR);
	if (idIcon == 0) {
		FreeLibrary(hExe);
		return nullptr;
	}

	// Restituiscimi un puntatore alla risorsa icona con quell'ID
	res = FindResource(hExe, MAKEINTRESOURCE(idIcon), RT_ICON);
	if (hExe == NULL || res == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}

	length = SizeofResource(hExe, res);
	if (length == 0) {
		FreeLibrary(hExe);
		return nullptr;
	}

	resptr = LoadResource(hExe, res);
	if (resptr == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}

	icon = LockResource(resptr);
	if (icon == NULL) {
		FreeLibrary(hExe);
		return nullptr;
	}

	char * buff = (char *)malloc(length);
	if (buff == NULL) {
		FreeLibrary(hExe);
		throw std::bad_alloc();
	}

	if (memcpy_s(buff, length, icon, length) != 0) {
		FreeLibrary(hExe);
		free(buff);
		return nullptr;
	}

	FreeLibrary(hExe);

	//restituiamo i byte dell'icona
	return buff;
}

char * Modification::getSerializedName(int & length) {
	if(mod != add)
		return nullptr;

	length = (app.Name.size() + 1) * sizeof(wchar_t);

	char * buff = (char *)malloc(length);
	if(buff == NULL)
		throw std::bad_alloc();

	if(memcpy_s(buff, length, app.Name.c_str(), length) != 0) {
		free(buff);
		throw std::overflow_error("Cannot copy executable name");
	}

	return buff;
}