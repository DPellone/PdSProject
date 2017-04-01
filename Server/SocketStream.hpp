#pragma once

#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdexcept>
#include <atomic>

#define LISTENQ 0

class SocketStream {

	WSADATA wsaData;		
	SOCKET ListenSocket;
	SOCKET ClientSocket;
	struct sockaddr_in addr, cliaddr;
	socklen_t cliaddrlen = sizeof(cliaddr);
	std::atomic_int iResult;
	std::atomic_bool isConnected = false;							//stato del socket connesso

public:
	SocketStream(int service);
	void waitingforConnection();
	void setStatus(bool status);
	bool getStatus();
	void closeConnection();
	int receiveData(char* buffer, int len);
	void sendData(char* buffer, int len);
};

class socket_exception : public std::runtime_error {
public:
	socket_exception(const char * message) : runtime_error(message) {};
};