// TeleshakeLibraryWrapper.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"


typedef unsigned char tByte;
typedef unsigned short tWord;
typedef unsigned long tDWord;
typedef int tErrInfo;
typedef char tVersion[9];
typedef enum portNum { COM1 = 1, COM2 } tPortNum;
typedef enum DeviceAddress {
	device01 = 1,
	device02,
	device03,
	device04,
	device05,
	device06,
	device07,
	device08,
	device09,
	device10,
	device11,
	device12,
	device13,
	device14
} tDeviceAddress; /* Geräteadressen */
typedef tDWord tCycleT;
typedef tWord tRpm;
typedef enum Seq {
	OFF = 0x00,
	N_W_S_E = 0x01,
	N_E_S_W = 0x02,
	NE_SW = 0x04,
	NW_SE = 0x08,
	N_S = 0x10,
	E_W = 0x20
} tSeq;
typedef tWord tTime;
typedef tDWord tNSeq;
typedef enum Power {
	_20 = 2,
	_30,
	_40,
	_50,
	_60,
	_70,
	_80,
	_90,
	_100,
} tPower;
typedef tWord tTime;

/* Geräteinformation */
typedef struct {
	tDeviceAddress devAddr;
	tByte fwVersionMajor;
	tByte fwVersionMinor;
	tDWord serialNumber;
	tByte status;
} tDevInfo;

/* Liste der angeschlossenen Geräte mit Geräteinformationen */
typedef struct tnode *tRefDevList;
typedef struct tnode {
	tDevInfo deviceInfo;
	tRefDevList next;
} tDevListNode;


/*
populate comments
 */

typedef int(__stdcall *cmdInitPtr)(tVersion input, tVersion *output);
typedef int(__stdcall *queryPortPtr)(tPortNum port, tRefDevList *refFirst, tWord *bitLeiste);
typedef int(__stdcall *setRpmPtr)(tPortNum port, tDeviceAddress devAddr, tRpm *rpm);
typedef int(__stdcall *startDevicePtr)(tPortNum port, tDeviceAddress devAddr);
typedef int(__stdcall *stopDevicePtr)(tPortNum port, tDeviceAddress devAddr);
typedef int(__stdcall *closePtr)();




__declspec(dllexport) int Connect(tPortNum port) {

	LPCSTR libName = "cmdlib.dll";
	HMODULE dllHandle = LoadLibrary(libName);//dynamically load dll
	cmdInitPtr initPtr = NULL;//init function pointer
	LPCSTR initFuncName = "cmdInit";//init function name
	//if dll not found return error code of 404
	if (NULL == dllHandle) {
		return 404;
	}
	//get function pointer and cast to specific func pointer type
	initPtr = (cmdInitPtr)GetProcAddress(dllHandle, initFuncName);
	//return 404 if not found
	if (NULL == initPtr)
	{
		return 404;
	}

	tVersion libVersion = "01.00.00";/* declare library version */
	tVersion cmdlibVersion;/* allocate var for function that sets the version */
	int error;/* declare error var  */

	/* initialize library and pass library version by reference to be modified by function */
	error = initPtr(libVersion, &cmdlibVersion);

	/*return early if not successful */
	if (error != 0) {
		return error;
	}

	tRefDevList devices = NULL;/* init device list*/
	tWord bitList;/* dont know what this is dont care but its required to be set by queryPort function*/
	queryPortPtr queryPtr = NULL;//init function pointer
	LPCSTR queryFuncName = "queryPort";//init function name

	//get function pointer and cast to specific func pointer type
	queryPtr = (queryPortPtr)GetProcAddress(dllHandle, queryFuncName);
	//return 404 if not found
	if (NULL == queryPtr) {
		return 404;
	}

	/* query port effectively connects to/prepares specified port and populates device list */
	error = queryPtr(port, &devices, &bitList);

	/*return early if not successful */
	if (error != 0) {
		return error;
	}

	return error;

}

__declspec(dllexport) int Disconnect() {

	LPCSTR libName = "cmdlib.dll";
	HMODULE dllHandle = LoadLibrary(libName);//dynamically load dll
	closePtr closePt = NULL;//init function pointer
	LPCSTR closeFuncName = "cmdClose";//init function name
	//if dll not found return error code of 404
	if (NULL == dllHandle) {
		return 404;
	}

	//get function pointer and cast to specific func pointer type
	closePt = (closePtr)GetProcAddress(dllHandle, closeFuncName);
	//return 404 if not found
	if (NULL == closePt)
	{
		return 404;
	}

	int error;/* declare error var  */

	/* initialize library and pass library version by reference to be modified by function */
	error = closePt();

	/*return early if not successful */
	return error;

}

__declspec(dllexport) int Shake(tPortNum port, tDeviceAddress devAddress, tRpm *rpm) {

	LPCSTR libName = "cmdlib.dll";
	HMODULE dllHandle = LoadLibrary(libName);//dynamically load dll
	setRpmPtr setRpmPt = NULL;//init function pointer
	LPCSTR rpmFuncName = "setRpm";//init function name
	//if dll not found return error code of 404
	if (NULL == dllHandle) {
		return 404;
	}

	//get function pointer and cast to specific func pointer type
	setRpmPt = (setRpmPtr)GetProcAddress(dllHandle, rpmFuncName);
	//return 404 if not found
	if (NULL == setRpmPt)
	{
		return 404;
	}

	int error;/* declare error var  */

	/* initialize library and pass library version by reference to be modified by function */
	error = setRpmPt(port, devAddress, rpm);

	/*return early if not successful */
	if (error != 0) {
		return error;
	}

	startDevicePtr startDevPt = NULL;//init function pointer
	LPCSTR startFuncName = "startDevice";//init function name

	//get function pointer and cast to specific func pointer type
	startDevPt = (startDevicePtr)GetProcAddress(dllHandle, startFuncName);
	//return 404 if not found
	if (NULL == startDevPt)
	{
		return 404;
	}

	/* initialize library and pass library version by reference to be modified by function */
	error = startDevPt(port, devAddress);

	return error;
}

__declspec(dllexport) int Stop(tPortNum port, tDeviceAddress devAddress) {

	LPCSTR libName = "cmdlib.dll";
	HMODULE dllHandle = LoadLibrary(libName);//dynamically load dll
	stopDevicePtr stopPt = NULL;//init function pointer
	LPCSTR stopFuncName = "stopDevice";//init function name
	//if dll not found return error code of 404
	if (NULL == dllHandle) {
		return 404;
	}

	//get function pointer and cast to specific func pointer type
	stopPt = (stopDevicePtr)GetProcAddress(dllHandle, stopFuncName);
	//return 404 if not found
	if (NULL == stopPt)
	{
		return 404;
	}

	int error;/* declare error var  */

	/* initialize library and pass library version by reference to be modified by function */
	error = stopPt(port, devAddress);

	return error;
}






