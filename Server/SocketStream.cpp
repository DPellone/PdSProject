#pragma once
#define maxlength 2048
#include "SocketStream.hpp"
#include <iostream>

SocketStream::SocketStream(int service)
{
	ListenSocket = INVALID_SOCKET;
	ClientSocket = INVALID_SOCKET;

	// Initialize Winsock
	if ((iResult = WSAStartup(MAKEWORD(2, 2), &wsaData)) != 0)
		throw socket_exception("WSAStartup failed");

	if ((ListenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP)) == INVALID_SOCKET)
		throw socket_exception("Socket constructor failed");

	//Preparo la struttura per fare il bind
	
	ZeroMemory(&addr, sizeof(addr));
	addr.sin_family = AF_INET;
	addr.sin_port = htons(service);
	addr.sin_addr.s_addr = htonl(INADDR_ANY);

	if (bind(ListenSocket, (struct sockaddr *)&addr, sizeof(addr)) != 0) {
		closesocket(ListenSocket);
		throw socket_exception("Bind failed");
	}

	if (listen(ListenSocket, LISTENQ) == SOCKET_ERROR) {
		closesocket(ListenSocket);
		throw socket_exception("Listen failed");
	}
}

void SocketStream::waitingforConnection()
{

	if ((ClientSocket = accept(ListenSocket, (struct sockaddr *)&cliaddr, &cliaddrlen)) == INVALID_SOCKET) {
		closesocket(ListenSocket);
		throw socket_exception("Accept failed");
	}

	setStatus(true);			//il client è connesso

}

void SocketStream::setStatus(bool status)
{
	isConnected = status;		// per segnalare lo stato di ClientSocket
}

bool SocketStream::getStatus()
{
	return isConnected;
}

void SocketStream::closeConnection()
{
	if ((closesocket(ClientSocket)) == SOCKET_ERROR) {
		throw socket_exception("Socket close failed");
	}
}


void SocketStream::sendData(char * buffer, int len)
{
	int nleft = len;

	while(nleft>0){

		if(nleft <= maxlength){
			
			iResult = send(ClientSocket, buffer, nleft, 0);
		}
		else
		{
			iResult = send(ClientSocket, buffer, maxlength, 0);
		}

		if (iResult == SOCKET_ERROR)
			throw socket_exception("Send failed");

		nleft = nleft - iResult;
		buffer += iResult;
	}
}

int SocketStream::receiveData(char * buffer, int len)
{
	iResult = recv(ClientSocket, buffer, len, 0); //0: no flag

	if (iResult == SOCKET_ERROR)
		throw socket_exception("Recv failed");

	return iResult;
}

