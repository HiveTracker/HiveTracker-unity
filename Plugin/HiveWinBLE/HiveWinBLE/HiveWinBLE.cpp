#include "stdafx.h"
#include "HiveWinBLE.h"
#include "Utils.h"

#pragma warning (disable: 4068)

#include <windows.h>
#include <stdio.h>
#include <tchar.h>
#include <setupapi.h>
#include <devguid.h>
#include <regstr.h>
#include <bthdef.h>
#include <bluetoothleapis.h>
#include <comdef.h>
#include <mutex>

#include <iostream>
#include <sstream>
#include <string>
#include <locale>
#include <queue>
#include <array>
#include <vector>
#include <regex>
#include <algorithm>    // std::find_if
#include <regex>
#include <thread>

#pragma comment(lib, "SetupAPI")
#pragma comment(lib, "BluetoothApis.lib")

//#define LOG_ALL

#if defined(LOG_ALL)
#define Log(str) LogToFile(str)
#else
#define Log(str)
#endif

void LogToFile(const char* message)
{
	FILE* f = NULL;
	fopen_s(&f, "C:\\Users\\marct\\Documents\\UnityProjects\\Uduino\\log.txt", "a+");
	if (f) {
		fprintf(f, message);
		fprintf(f, "\n");
		fclose(f);
	}
}

static DebugCallback debugLogCallback = NULL;
static DebugCallback debugWarningCallback = NULL;
static DebugCallback debugErrorCallback = NULL;
static SendBluetoothMessageCallback sendMessageCallback = NULL;
//static SendBluetoothFunctionCallback sendFunctionCallback = NULL;

static const char* gameObjectName = "DesktopBLECommunication";
std::string currentBufferString;
bool isTreatindData = false;

char inputMessage[22];

std::queue<std::string> elementsToSend;
bool isSending = false;


struct BLEDeviceInfo
{
	GUID containerId;
	std::string deviceName;
};

struct BLEServiceInfo
{
	GUID containerId; // Used to match service and devices...
	BTH_LE_UUID id;
	std::string name;
	std::string path;
	BLEDeviceInfo* device;
};

struct BLEConnectedServiceInfo
{
	HANDLE deviceHandle;
	BTH_LE_GATT_SERVICE gattService;
	BLEServiceInfo* service;
	std::vector<BTH_LE_GATT_CHARACTERISTIC> characteristics;
};

struct BLERegisteredCharacteristicInfo
{
	BLEConnectedServiceInfo* service;
	BTH_LE_GATT_CHARACTERISTIC characteristic;
	BLUETOOTH_GATT_EVENT_HANDLE characteristicHandle;
};

std::vector<BLEDeviceInfo*> devices;
std::vector<BLEServiceInfo*> services;
std::vector<BLEConnectedServiceInfo*> connectedServices;
std::vector<BLERegisteredCharacteristicInfo*> registeredCharacteristics;

// --------------------------------------------------------------------------
// Called by mono side to hook up message handlers!
// --------------------------------------------------------------------------
void _winBluetoothLEConnectCallbacks(SendBluetoothMessageCallback sendMessageMethod, DebugCallback logMethod, DebugCallback warningMethod, DebugCallback errorMethod)
{
	sendMessageCallback = sendMessageMethod;
	debugLogCallback = logMethod;
	debugWarningCallback = warningMethod;
	debugErrorCallback = errorMethod;
	DebugLog("Hooked Debug Functions");
	inputMessage[0] = '\0';
	currentBufferString = "";
	elementsToSend.empty();
}

// --------------------------------------------------------------------------
// Talks back to the mono side of things!
// --------------------------------------------------------------------------
void SendBluetoothMessage(const char* message)
{

	if (sendMessageCallback != NULL)
	{
		sendMessageCallback(gameObjectName, "OnBluetoothMessage", message);
	}
}


void SendUnityFunction(const char* function, const char* message)
{
	if (sendMessageCallback != NULL && sendMessageCallback != nullptr)
	{
		LogToFile("sendMessageCallback start");
		LogToFile(function);
		LogToFile(message);
	//	std::string a = message;
		//std::string b;
		//b = a; // C++11 forces this to be a copy as well
	//	sendMessageCallback(gameObjectName, function, b.c_str());
		sendMessageCallback(gameObjectName, function,message);
		LogToFile("sendMessageCallback done");
		LogToFile("\r\n");
	}
}

bool PluginWriteLoop() {
	while (!elementsToSend.empty())
	{
		std::string e = elementsToSend.front();
		sendMessageCallback(gameObjectName, "PluginMessageReceived", e.c_str());
		elementsToSend.pop();
	}
	return true;
}

void PluginWrite(const char * message) {

	if (sendMessageCallback != NULL)
	{
		sendMessageCallback(gameObjectName, "PluginMessageReceived", message);
	}
}
/*
void SendUnityFunction(const char* function)
{
	if (sendMessageCallback != NULL)
	{
		sendMessageCallback(gameObjectName, function, "");
	}
}*/

// --------------------------------------------------------------------------
// Sends a log to the mono side of things
// --------------------------------------------------------------------------
void DebugLog(const char* message)
{
	if (debugLogCallback != NULL)
	{
		debugLogCallback(message);
	}
}

// --------------------------------------------------------------------------
// Sends a log to the mono side of things
// --------------------------------------------------------------------------
void DebugWarning(const char* message)
{
	if (debugWarningCallback != NULL)
	{
		debugWarningCallback(message);
	}
}

// --------------------------------------------------------------------------
// Sends a log to the mono side of things
// --------------------------------------------------------------------------
void DebugError(const char* message)
{
	if (debugErrorCallback != NULL)
	{
		debugErrorCallback(message);
	}
}

// --------------------------------------------------------------------------
// Reads a device Property, used to retrieve device name, address, etc...
// --------------------------------------------------------------------------
std::string ReadProperty(HDEVINFO hDevInfo, PSP_DEVINFO_DATA pDeviceInfoData, DWORD property)
{
	DWORD DataT3;
	LPTSTR buffer3 = NULL;
	DWORD buffersize3 = 0;
	while (!SetupDiGetDeviceRegistryProperty(hDevInfo, pDeviceInfoData, property, &DataT3, (PBYTE)buffer3, buffersize3, &buffersize3))
	{
		if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
		{
			// Change the buffer size.
			if (buffer3) delete(buffer3);
			// Double the size to avoid problems on
			// W2k MBCS systems per KB 888609.
			buffer3 = new wchar_t[buffersize3 * 2];
		}
		else
		{
			wchar_t buf[256];
			FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM, NULL, GetLastError(), MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), buf, 256, NULL);
			DebugError(std::string("Could not read device property: ").append(BLEUtils::ToNarrow(buf)).c_str());
			break;
		}
	}

	std::string prop = "";
	if (buffersize3 > 0)
	{
		prop = BLEUtils::ToNarrow(buffer3);
		delete(buffer3);
	}
	return prop;
}

// --------------------------------------------------------------------------
// Reads a device's instance Id, used to generate the device path and later open handle to it
// --------------------------------------------------------------------------
std::string ReadDeviceInstanceId(HDEVINFO hDevInfo, PSP_DEVINFO_DATA pDeviceInfoData)
{
	LPTSTR deviceIdBuffer = NULL;
	DWORD deviceIdBufferSize = 0;

	while (!SetupDiGetDeviceInstanceId(hDevInfo, pDeviceInfoData, deviceIdBuffer, deviceIdBufferSize, &deviceIdBufferSize))
	{
		if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
		{
			if (deviceIdBuffer) delete(deviceIdBuffer);
			deviceIdBuffer = new wchar_t[deviceIdBufferSize * 2];
		}
		else
		{
			wchar_t buf[256];
			FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM, NULL, GetLastError(), MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), buf, 256, NULL);
			DebugError(std::string("Could not read device instance Id: ").append(BLEUtils::ToNarrow(buf)).c_str());
			break;
		}
	}
	std::string id = "<no_id>";
	if (deviceIdBufferSize > 0)
	{
		id = BLEUtils::ToNarrow(deviceIdBuffer);
		delete(deviceIdBuffer);
	}
	return id;
}

// --------------------------------------------------------------------------
// Reads a device's interface details, we use this to get service GUIDs
// --------------------------------------------------------------------------
std::string ReadDeviceInterfaceDetails(HDEVINFO hDevInfo, PSP_DEVINFO_DATA pDeviceInfoData, PSP_DEVICE_INTERFACE_DATA pDeviceInterfaceData)
{
	PSP_DEVICE_INTERFACE_DETAIL_DATA pInterfaceDetailData = NULL;
	DWORD size = 0;
	while (!SetupDiGetDeviceInterfaceDetail(hDevInfo, pDeviceInterfaceData, NULL, size, &size, pDeviceInfoData))
	{
		if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
		{
			pInterfaceDetailData = (PSP_DEVICE_INTERFACE_DETAIL_DATA)GlobalAlloc(GPTR, size);
			pInterfaceDetailData->cbSize = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA);
		}
		else
		{
			wchar_t buf[256];
			FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM, NULL, GetLastError(), MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), buf, 256, NULL);
			DebugError(std::string("Could not read device interface details: ").append(BLEUtils::ToNarrow(buf)).c_str());
			break;
		}
	}

	std::string ret = "<no path>";
	if (pInterfaceDetailData != NULL)
	{
		ret = BLEUtils::ToNarrow(pInterfaceDetailData->DevicePath);
		GlobalFree(pInterfaceDetailData);
	}

	return ret;
}

// --------------------------------------------------------------------------
// Finds all the bluetooth devices
// --------------------------------------------------------------------------
bool ScanBLEInterfaces()
{
	ClearAll();

	HDEVINFO hDevInfo;
	SP_DEVINFO_DATA DeviceInfoData;
	DWORD i;
	// Create a HDEVINFO with all present devices.
	hDevInfo = SetupDiGetClassDevs(&GUID_DEVCLASS_BLUETOOTH, 0, 0, DIGCF_PRESENT);

	if (hDevInfo == INVALID_HANDLE_VALUE)
	{
		wchar_t buf[256];
		FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM, NULL, GetLastError(), MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), buf, 256, NULL);
		DebugError(std::string("Could not request bluetooth device list: ").append(BLEUtils::ToNarrow(buf)).c_str());
		return false;
	}

	// Enumerate through all devices in Set.
	DeviceInfoData.cbSize = sizeof(SP_DEVINFO_DATA);
	for (i = 0; SetupDiEnumDeviceInfo(hDevInfo, i, &DeviceInfoData); i++)
	{
		// Check the hardware Id
		std::string hardwareId = ReadProperty(hDevInfo, &DeviceInfoData, SPDRP_HARDWAREID);

		// We're only interested in entries that start with either 'BTHLE\' or 'BTHLEDEVICE\'
		bool isDevice = hardwareId.find("BTHLE\\") == 0;
		bool isService = hardwareId.find("BTHLEDevice\\") == 0;
		if (!isDevice && !isService)
		{
			continue;
		}

		// Then grab the container GUID, this is what we use to match devices and services to the same physical device
		GUID containerGUID = BLEUtils::StringToGUID(ReadProperty(hDevInfo, &DeviceInfoData, SPDRP_BASE_CONTAINERID));

		if (isDevice)
		{
			// Fetch the name!
			auto info = new BLEDeviceInfo();
			info->deviceName = ReadProperty(hDevInfo, &DeviceInfoData, SPDRP_FRIENDLYNAME);
			info->containerId = containerGUID;
			devices.push_back(info);
		}

		if (isService)
		{
			// Fetch the guid!
			auto service = new BLEServiceInfo();
			service->name = ReadProperty(hDevInfo, &DeviceInfoData, SPDRP_DEVICEDESC);

			std::regex guidRegex("\\{.*\\}");
			std::smatch match;
			if (std::regex_search(hardwareId, match, guidRegex))
			{
				service->containerId = containerGUID;

				std::string guidString = match.str();
				service->id = BLEUtils::StringToBTHLEUUID(guidString);

				// Parse the device instance id to get the device path!
				std::string deviceId = ReadDeviceInstanceId(hDevInfo, &DeviceInfoData);

				// Create the device path
				std::string path = "\\\\?\\";
				std::replace(deviceId.begin(), deviceId.end(), '\\', '#');
				path.append(deviceId);
				path.append("#");
				path.append(guidString);
				service->path = path;
			}
			else
			{
				DebugError(std::string("Could not extract service GUID from the hardware ID \'").append(hardwareId).append("\'").c_str());
			}

			services.push_back(service);
		}
	}

	// Set device pointers
	for (auto service : services)
	{
		auto devIt = std::find_if(devices.begin(), devices.end(), [service](BLEDeviceInfo* d) { return d->containerId == service->containerId; });
		if (devIt != devices.end())
		{
			service->device = *devIt;
		}
		else
		{
			DebugError(std::string("Could not find the device that service ").append(BLEUtils::BTHLEGUIDToString(service->id)).append(" belongs to").c_str());
		}
	}

	return 0;
}


void ClearAll() {
	isTreatindData = false;
	currentBufferString = "";
	currentBufferString.clear();
	elementsToSend.empty();
}
// --------------------------------------------------------------------------
// Iterates over all devices and sends notifications back for each one that matches the uuids passed in
// --------------------------------------------------------------------------
void notifyDevicesWithServices(const std::vector<BTH_LE_UUID>& uuids)
{
	std::vector<GUID> returnedDevices;
	// Find any device that has a service whose uuid matches one of the uuids passed in!
	for (auto service : services)
	{
		// Does this service match the uuid?
		if (std::find(uuids.begin(), uuids.end(), service->id) != uuids.end())
		{
			// Yes, send a message for each discovered peripheral
			// Sadly we don't have access to advertisement data, it is managed by Windows!
			std::string deviceDiscoveredMessage = "";
			deviceDiscoveredMessage.append(BLEUtils::GUIDToString(service->device->containerId));
			deviceDiscoveredMessage.append("|");
			deviceDiscoveredMessage.append(service->device->deviceName);

			SendUnityFunction("DeviceDetected", deviceDiscoveredMessage.c_str());
		}
	}
}

// --------------------------------------------------------------------------
// Iterates over all devices and sends notifications back for each one
// --------------------------------------------------------------------------
void notifyAllDevices()
{
	// Find any device that has a service whose uuid matches one of the uuids passed in!
	for (auto device : devices)
	{
		// Yes, send a message for each discovered peripheral
		// Sadly we don't have access to advertisement data...
		std::string deviceDiscoveredMessage = "";
		deviceDiscoveredMessage.append(BLEUtils::GUIDToString(device->containerId));
		deviceDiscoveredMessage.append("|");
		deviceDiscoveredMessage.append(device->deviceName);
		SendBluetoothMessage(deviceDiscoveredMessage.c_str());

		SendUnityFunction("DeviceDetected", deviceDiscoveredMessage.c_str());
	}
}

// --------------------------------------------------------------------------
// Iterates over all connected devices and sends notifications back for each one that matches the uuids passed in
// --------------------------------------------------------------------------
void notifyConnectedServices(const std::vector<BTH_LE_UUID>& uuids)
{
	// Send messages for all the ones
	for (auto service : connectedServices)
	{
		// Find any service that has a service whose uuid matches one of the uuids passed in!
		if (std::find_if(uuids.begin(), uuids.end(), [&service](const BTH_LE_UUID& uuid) { return service->service->id == uuid; }) != uuids.end())
		{
			std::string deviceDiscoveredMessage = "RetrievedConnectedPeripheral~";
			deviceDiscoveredMessage.append(BLEUtils::GUIDToString(service->service->device->containerId));
			deviceDiscoveredMessage.append("~");
			deviceDiscoveredMessage.append(service->service->device->deviceName);
			SendBluetoothMessage(deviceDiscoveredMessage.c_str());
		}
	}
}

// --------------------------------------------------------------------------
// Iterates over all connected devices and sends notifications back for each one
// --------------------------------------------------------------------------
void notifyAllConnected()
{
	// Send messages for all the ones
	for (auto service : connectedServices)
	{
		// Send a message for each discovered service
		std::string connectedDeviceRetrievedMessage = "RetrievedConnectedPeripheral~";
		connectedDeviceRetrievedMessage.append(BLEUtils::GUIDToString(service->service->device->containerId));
		connectedDeviceRetrievedMessage.append("~");
		connectedDeviceRetrievedMessage.append(service->service->device->deviceName);
		SendBluetoothMessage(connectedDeviceRetrievedMessage.c_str());
	}
}

// --------------------------------------------------------------------------
// Retrieves the GATT service struct that matches the given service
// --------------------------------------------------------------------------
bool GetGATTService(HANDLE serviceHandle, BTH_LE_GATT_SERVICE& outService)
{
	// Get GATT service
	PBTH_LE_GATT_SERVICE services = nullptr;
	USHORT serviceCount = 0;
	HRESULT hr = S_OK;
	while ((hr = BluetoothGATTGetServices(serviceHandle, serviceCount, services, &serviceCount, BLUETOOTH_GATT_FLAG_NONE)) != S_OK)
	{
		if (hr == HRESULT_FROM_WIN32(ERROR_MORE_DATA))
		{
			// Change the buffer size.
			services = new BTH_LE_GATT_SERVICE[serviceCount];
		}
		else
		{
			_com_error err(hr);
			DebugError(std::string("Could not retrieve service GATT info: ").append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
			break;
		}
	}

	bool ret = hr == S_OK && serviceCount > 0;
	if (ret)
	{
		// Only grab the first service!
		outService = services[0];
		delete[] services;
	}
	return ret;
}


// --------------------------------------------------------------------------
// Retrieves the characteristics associated with a service
// --------------------------------------------------------------------------
std::vector<BTH_LE_GATT_CHARACTERISTIC> GetGATTCharacteristics(HANDLE serviceHandle, BTH_LE_GATT_SERVICE& gattService)
{
	std::vector<BTH_LE_GATT_CHARACTERISTIC> ret;
	PBTH_LE_GATT_CHARACTERISTIC characteristicsBuffer = nullptr;
	USHORT characteristicCount = 0;
	HRESULT hr = S_OK;
	while ((hr = BluetoothGATTGetCharacteristics(serviceHandle, &gattService, characteristicCount, characteristicsBuffer, &characteristicCount, BLUETOOTH_GATT_FLAG_NONE)) != S_OK)
	{
		if (hr == HRESULT_FROM_WIN32(ERROR_MORE_DATA))
		{
			// Change the buffer size.
			ret.resize(characteristicCount);
			// And have the method write directly into the vector!
			characteristicsBuffer = ret.data();
		}
		else
		{
			_com_error err(hr);
			DebugError(std::string("Could not retrieve service characteristics: ").append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
			break;
		}
	}
	return ret;
}

// --------------------------------------------------------------------------
// Retrieves a characteristic's value, allocates memory because the data has variable size!
// --------------------------------------------------------------------------
PBTH_LE_GATT_CHARACTERISTIC_VALUE AllocAndReadCharacteristic(HANDLE serviceHandle, BTH_LE_GATT_CHARACTERISTIC* currGattChar)
{
	if (currGattChar->IsReadable)
	{
		// Determine Characteristic Value Buffer Size
		USHORT charValueDataSize = 0;
		PBTH_LE_GATT_CHARACTERISTIC_VALUE pCharValueBuffer = nullptr;
		HRESULT hr = S_OK;
		while ((hr = BluetoothGATTGetCharacteristicValue(serviceHandle, currGattChar, (ULONG)charValueDataSize, pCharValueBuffer, &charValueDataSize, BLUETOOTH_GATT_FLAG_NONE)) != S_OK)
		{
			if (hr == HRESULT_FROM_WIN32(ERROR_MORE_DATA))
			{
				pCharValueBuffer = (PBTH_LE_GATT_CHARACTERISTIC_VALUE)malloc(charValueDataSize);
				RtlZeroMemory(pCharValueBuffer, charValueDataSize);
				pCharValueBuffer->DataSize = charValueDataSize;
			}
			else
			{
				_com_error err(hr);
				DebugError(std::string("Could not get characteristic ").append(BLEUtils::BTHLEGUIDToString(currGattChar->CharacteristicUuid)).append(" value: ").append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
				return nullptr;
			}
		}

		return pCharValueBuffer;
	}
	else
	{
		DebugError(std::string("Characteristic ").append(BLEUtils::BTHLEGUIDToString(currGattChar->CharacteristicUuid)).append(" is not readable.").c_str());
		return nullptr;
	}
}

// --------------------------------------------------------------------------
// Disconnects ALL connected services associated with a device!
// --------------------------------------------------------------------------
void DisconnectServicesForDevice(GUID addressGUID)
{
	for (auto servIt = connectedServices.begin(); servIt != connectedServices.end();)
	{
		auto cservice = *servIt;
		if (cservice->service->device->containerId == addressGUID)
		{
			// Do we have any registered characteristics?
			for (auto charIt = registeredCharacteristics.begin(); charIt != registeredCharacteristics.end();)
			{
				auto charInfo = *charIt;
				if (charInfo->service->service->device->containerId == addressGUID)
				{
					// We should unregister!
					HRESULT hr = BluetoothGATTUnregisterEvent(charInfo->characteristicHandle, BLUETOOTH_GATT_FLAG_WRITE_WITHOUT_RESPONSE);
					if (hr == S_OK)
					{
						// Send message
						std::string registerCharacteristicMessage = "DidUpdateNotificationStateForCharacteristic~";
						registerCharacteristicMessage.append(BLEUtils::GUIDToString(addressGUID));
						registerCharacteristicMessage.append("~");
						registerCharacteristicMessage.append(BLEUtils::BTHLEGUIDToString(cservice->service->id));
						registerCharacteristicMessage.append("~");
						registerCharacteristicMessage.append(BLEUtils::BTHLEGUIDToString(charInfo->characteristic.CharacteristicUuid));
						SendBluetoothMessage(registerCharacteristicMessage.c_str());

						// Clean up
						delete charInfo;
						charIt = registeredCharacteristics.erase(charIt);
					}
					else
					{
						_com_error err(hr);
						DebugError(std::string("Could not unregister from characteristic ").append(BLEUtils::GUIDToString(addressGUID)).append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
						// Next element!
						++charIt;
					}
				}
				else
				{
					// Next element!
					++charIt;
				}
			}

			if (CloseHandle(cservice->deviceHandle))
			{
				delete cservice;
				servIt = connectedServices.erase(servIt);
			}
			else
			{
				DebugError(std::string("Could not close handle to device ").append(BLEUtils::GUIDToString(addressGUID)).c_str());

			}
		}
		else
		{
			++servIt;
		}
	}
	// Notify that we disconnected to a service!
	std::string connectedMessage = "";
	connectedMessage.append(BLEUtils::GUIDToString(addressGUID));
	//SendBluetoothMessage(connectedMessage.c_str());
	SendUnityFunction("BoardDisconnectedFromSource", connectedMessage.c_str());
	ClearAll();
}

// --------------------------------------------------------------------------
// Retrieves the descriptors associated with a characteristic
// --------------------------------------------------------------------------
std::vector<BTH_LE_GATT_DESCRIPTOR> GetGATTDescriptors(HANDLE serviceHandle, PBTH_LE_GATT_CHARACTERISTIC characteristic)
{
	std::vector<BTH_LE_GATT_DESCRIPTOR> ret;
	PBTH_LE_GATT_DESCRIPTOR descriptorsBuffer = nullptr;
	USHORT descriptorsCount = 0;
	HRESULT hr = S_OK;
	while ((hr = BluetoothGATTGetDescriptors(serviceHandle, characteristic, descriptorsCount, descriptorsBuffer, &descriptorsCount, BLUETOOTH_GATT_FLAG_NONE)) != S_OK)
	{
		if (hr == HRESULT_FROM_WIN32(ERROR_MORE_DATA))
		{
			// Change the buffer size.
			ret.resize(descriptorsCount);
			// And have the method write directly into the vector!
			descriptorsBuffer = ret.data();
		}
		else
		{
			_com_error err(hr);
			DebugError(std::string("Could not retrieve characteristic descriptors: ").append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
			break;
		}
	}
	return ret;
}

// --------------------------------------------------------------------------
// Retrieves a descriptor's value, allocates memory because the data has variable size!
// --------------------------------------------------------------------------
PBTH_LE_GATT_DESCRIPTOR_VALUE AllocAndReadDescriptor(HANDLE serviceHandle, PBTH_LE_GATT_DESCRIPTOR descriptor)
{
	// Determine Characteristic Value Buffer Size
	USHORT descValueDataSize = 0;
	PBTH_LE_GATT_DESCRIPTOR_VALUE pDescValueBuffer = nullptr;
	HRESULT hr = S_OK;
	while ((hr = BluetoothGATTGetDescriptorValue(serviceHandle, descriptor, (ULONG)descValueDataSize, pDescValueBuffer, &descValueDataSize, BLUETOOTH_GATT_FLAG_NONE)) != S_OK)
	{
		if (hr == HRESULT_FROM_WIN32(ERROR_MORE_DATA))
		{
			pDescValueBuffer = (PBTH_LE_GATT_DESCRIPTOR_VALUE)malloc(descValueDataSize);
			RtlZeroMemory(pDescValueBuffer, descValueDataSize);
			pDescValueBuffer->DataSize = descValueDataSize;
		}
		else
		{
			_com_error err(hr);
			DebugError(std::string("Could not get descriptor value ").append(BLEUtils::BTHLEGUIDToString(descriptor->DescriptorUuid)).append(" value: ").append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
			return nullptr;
		}
	}

	return pDescValueBuffer;
}



// --------------------------------------------------------------------------
// Logs some info from the mono side
// --------------------------------------------------------------------------
void _winBluetoothLELog(const char* message)
{
	DebugLog(message);
}

// --------------------------------------------------------------------------
// Initialize the bluetooth 'stack'
// --------------------------------------------------------------------------
void _winBluetoothLEInitialize(bool asCentral, bool asPeripheral)
{
	SendUnityFunction("BleInitialized", "Success");
}

// --------------------------------------------------------------------------
// Clean up!
// --------------------------------------------------------------------------
void _winBluetoothLEDeInitialize()
{
	_winBluetoothLEDisconnectAll();
	SendBluetoothMessage("DeInitialized");
}

// --------------------------------------------------------------------------
// Pause sending messages back to the mono side
// --------------------------------------------------------------------------
void _winBluetoothLEPauseMessages(bool isPaused)
{
	
}

// --------------------------------------------------------------------------
// Scans all the bluetooth devices and notifies the mono side
// --------------------------------------------------------------------------
void _winBluetoothLEScanForPeripheralsWithServices(const char* serviceUUIDsString, bool allowDuplicates, bool rssiOnly, bool clearPeripheralList)
{
	// Devices are managed by windows, so we don't need to 'remember' old devices
	devices.clear();
	services.clear();
	connectedServices.clear();
	registeredCharacteristics.clear();

	ClearAll();
	// Scan for devices
	ScanBLEInterfaces();

	// Retrieve the devices with proper service UUID
	if (serviceUUIDsString != nullptr)
	{
		auto uuids = BLEUtils::GenerateGUIDList(serviceUUIDsString);
		notifyDevicesWithServices(uuids);
	}
	else
	{
		notifyAllDevices();
	}
}

// --------------------------------------------------------------------------
// Lists all the currently connected devices
// --------------------------------------------------------------------------
void _winBluetoothLERetrieveListOfPeripheralsWithServices(const char* serviceUUIDsString)
{
	if (serviceUUIDsString != nullptr)
	{
		auto uuids = BLEUtils::GenerateGUIDList(serviceUUIDsString);
		notifyConnectedServices(uuids);
	}
	else
	{
		notifyAllConnected();
	}
}

// --------------------------------------------------------------------------
// Stops scanning for bluetooth devices
// --------------------------------------------------------------------------
void _winBluetoothLEStopScan()
{
	// Nothing to do for now, scanning is handled by windows
}

// --------------------------------------------------------------------------
// Connects to a given device and list services/characteristics
// --------------------------------------------------------------------------
void _winBluetoothLEConnectToPeripheral(const char* address)
{
	if (address != nullptr)
	{
		// Iterate all the services for the given device
		bool firstService = true;
		GUID addressGUID = BLEUtils::StringToGUID(address);
		for (auto service : services)
		{
			if (service->containerId == addressGUID)
			{
				// Open a handle to the peripheral, and scan the services and characteristics
				HANDLE serviceHandle = CreateFile(BLEUtils::ToWide(service->path.c_str()).c_str(), GENERIC_WRITE | GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL);
				if (serviceHandle != INVALID_HANDLE_VALUE)
				{
					// Remember we connected to the device, so we can clean up later!
					auto connInfo = new BLEConnectedServiceInfo();
					connInfo->service = service;
					connInfo->deviceHandle = serviceHandle;
					connectedServices.push_back(connInfo);

					// Notify that we connected to a service!
					if (firstService)
					{
						ClearAll();

						firstService = false;
						std::string connectedMessage = "";
						connectedMessage.append(address);
						SendUnityFunction("BoardConnected", connectedMessage.c_str());
					}
					// Get GATT service ids and characteristics
					if (GetGATTService(serviceHandle, connInfo->gattService))
					{
						// Check that the gatt service ID matches the service ID
						auto gattServiceUuidString = BLEUtils::BTHLEGUIDToString(connInfo->gattService.ServiceUuid);
						if (connInfo->gattService.ServiceUuid == service->id)
						{
							// Notify that we indeed got the gatt service info!
							std::string discoveredServiceMessage = "DiscoveredService~";
							discoveredServiceMessage.append(address);
							discoveredServiceMessage.append("~");
							discoveredServiceMessage.append(gattServiceUuidString);
							SendBluetoothMessage(discoveredServiceMessage.c_str());

							// Scan characteristics now!
							connInfo->characteristics = GetGATTCharacteristics(serviceHandle, connInfo->gattService);
							if (connInfo->characteristics.size() > 0)
							{
								for (auto characteristic : connInfo->characteristics)
								{
									auto gattCharacteristicUuidString = BLEUtils::BTHLEGUIDToString(characteristic.CharacteristicUuid);

									// Notify that we got characteristic info
									std::string discoveredCharacteristicMessage = "DiscoveredCharacteristic~";
									discoveredCharacteristicMessage.append(address);
									discoveredCharacteristicMessage.append("~");
									discoveredCharacteristicMessage.append(std::to_string(characteristic.IsNotifiable));
									discoveredCharacteristicMessage.append("~");
									discoveredCharacteristicMessage.append(gattServiceUuidString);
									discoveredCharacteristicMessage.append("~");
									discoveredCharacteristicMessage.append(gattCharacteristicUuidString);
									SendBluetoothMessage(discoveredCharacteristicMessage.c_str());
								}
							}
							else
							{
								DebugError(std::string("Device ").append(address).append(" reported 0 characteristics.").c_str());
							}
						}
						else
						{
							DebugError(std::string("GATT service id ").append(gattServiceUuidString).append(" does not match service id ").append(BLEUtils::BTHLEGUIDToString(service->id)).c_str());
						}
					}
				}
			}
		}

		if (firstService)
		{
			DebugError(std::string("Did not find any service for device ").append(address).c_str());
		}
	}
	else
	{
		DebugError(std::string("Can't connect to Null device address").c_str());
	}
}

// --------------------------------------------------------------------------
// Disconnects from a given device
// --------------------------------------------------------------------------
void _winBluetoothLEDisconnectPeripheral(const char* address)
{
	if (address != nullptr)
	{
		// Disconnect all services associated with this device
		GUID addressGUID = BLEUtils::StringToGUID(address);
		DisconnectServicesForDevice(addressGUID);
		ClearAll();
	}
	else
	{
		DebugError(std::string("Can't connect to Null device address").c_str());
	}
}

// --------------------------------------------------------------------------
// Reads a characteristic from a device/service
// --------------------------------------------------------------------------
void _winBluetoothLEReadCharacteristic(const char* address, const char* service, const char* characteristic)
{
	if (address == nullptr)
	{
		DebugError("Null address");
		return;
	}

	if (service == nullptr)
	{
		DebugError("Null service");
		return;
	}

	if (characteristic == nullptr)
	{
		DebugError("Null characteristic");
		return;
	}

	// Find connected service handle
	GUID addressGUID = BLEUtils::StringToGUID(address);
	BTH_LE_UUID serviceGUID = BLEUtils::StringToBTHLEUUID(service);
	auto servIt = std::find_if(connectedServices.begin(), connectedServices.end(), [addressGUID, serviceGUID](BLEConnectedServiceInfo* s) { return s->service->device->containerId == addressGUID && s->service->id == serviceGUID; });
	if (servIt != connectedServices.end())
	{
		// Find characteristic!
		BTH_LE_UUID characteristicGUID = BLEUtils::StringToBTHLEUUID(characteristic);
		auto cservice = *servIt;
		auto charIt = std::find_if(cservice->characteristics.begin(), cservice->characteristics.end(), [characteristicGUID](const BTH_LE_GATT_CHARACTERISTIC& c) { return c.CharacteristicUuid == characteristicGUID; });
		if (charIt != cservice->characteristics.end())
		{
			auto charVal = AllocAndReadCharacteristic(cservice->deviceHandle, &(*charIt));
			if (charVal != nullptr)
			{
				// Notify that we got characteristic info
				std::string readCharacteristicMessage = "DidUpdateValueForCharacteristic~";
				readCharacteristicMessage.append(address);
				readCharacteristicMessage.append("~");
				readCharacteristicMessage.append(characteristic);
				readCharacteristicMessage.append("~");
				readCharacteristicMessage.append(BLEUtils::Base64Encode(charVal->Data,charVal->DataSize));
				SendBluetoothMessage(readCharacteristicMessage.c_str());

				// Clean up!
				free(charVal);
			}
		}
		else
		{
			DebugError(std::string("Could not find characteristic ").append(characteristic).append(" to read.").c_str());
		}
	}
	else
	{
		DebugError(std::string("Could not find device ").append(address).append(" to read from.").c_str());
	}
}

// --------------------------------------------------------------------------
// Writes a characteristic to a device/service
// --------------------------------------------------------------------------
void _winBluetoothLEWriteCharacteristic(const char* address, const char* service, const char* characteristic, const char* data, int length, bool withResponse)
{
	if (address == nullptr)
	{
		DebugError("Null address");
		return;
	}

	if (service == nullptr)
	{
		DebugError("Null service");
		return;
	}

	if (characteristic == nullptr)
	{
		DebugError("Null characteristic");
		return;
	}

	if (data == nullptr)
	{
		DebugError("Null data");
		return;
	}


	// Find connected service handle
	GUID addressGUID = BLEUtils::StringToGUID(address);
	BTH_LE_UUID serviceGUID = BLEUtils::StringToBTHLEUUID(service);
	auto servIt = std::find_if(connectedServices.begin(), connectedServices.end(), [addressGUID, serviceGUID](BLEConnectedServiceInfo* s) { return s->service->device->containerId == addressGUID && s->service->id == serviceGUID; });
	if (servIt != connectedServices.end())
	{
		// Find characteristic!
		BTH_LE_UUID characteristicGUID = BLEUtils::StringToBTHLEUUID(characteristic);
		auto cservice = *servIt;

		// Find characteristic!
		auto charIt = std::find_if(cservice->characteristics.begin(),
			cservice->characteristics.end(),
			[characteristicGUID](const BTH_LE_GATT_CHARACTERISTIC& c) { return c.CharacteristicUuid == characteristicGUID; });

		if (charIt != cservice->characteristics.end())
		{
			ULONG charValueSize = length + sizeof(ULONG);
			PBTH_LE_GATT_CHARACTERISTIC_VALUE newCharVal = (PBTH_LE_GATT_CHARACTERISTIC_VALUE)malloc(charValueSize);
			RtlZeroMemory(newCharVal, charValueSize);
			newCharVal->DataSize = charValueSize;
			for (int i = 0; i < length; i++) {
				newCharVal->Data[i] = (byte)data[i];
			}

			/*BTH_LE_GATT_CHARACTERISTIC_VALUE newCharVal;

			RtlZeroMemory(&newCharVal, (sizeof(data)));

			newCharVal.DataSize = sizeof(data);
			newCharVal.Data[0] = (byte)data;
			*/

			PBTH_LE_GATT_CHARACTERISTIC_VALUE newValue = (PBTH_LE_GATT_CHARACTERISTIC_VALUE)malloc(sizeof(BTH_LE_GATT_CHARACTERISTIC_VALUE) * length);
			newValue->DataSize = length;
			for (int n = 0; n < static_cast<int>(length); n++)
			{
				newValue->Data[n] = data[n];
			}

			HRESULT hr = BluetoothGATTSetCharacteristicValue(
				cservice->deviceHandle,
				&(*charIt),
				newValue,
				NULL,
				BLUETOOTH_GATT_FLAG_WRITE_WITHOUT_RESPONSE); // BLUETOOTH_GATT_FLAG_NONE, BLUETOOTH_GATT_FLAG_WRITE_WITHOUT_RESPONSE

			if (hr == S_OK)
			{
				// TODO : Ici on d�bug si on re�oit un truc

				std::string wroteCharacteristicMessage = "DidWriteCharacteristic~";
				for (int i = 0; i < length; i++) {
					wroteCharacteristicMessage.append(std::to_string(newCharVal->Data[i]) );
				}
				SendBluetoothMessage(wroteCharacteristicMessage.c_str());
				
			}
			else
			{
				_com_error err(hr);
				DebugError(std::string("Could not fetch characteristic value for ").append(characteristic).append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
			}

			// Clean up!
			free(newCharVal);
		}
		else
		{
			DebugError(std::string("Could not find characteristic ").append(characteristic).append(" to write.").c_str());
		}
	}
	else
	{
		DebugError(std::string("Could not find device ").append(address).append(" to write to.").c_str());
	}
}



bool ends_with(std::string const &a, std::string const &b) {
	auto len = b.length();
	auto pos = a.length() - len;
	if (pos < 0)
		return false;
	auto pos_a = &a[pos];
	auto pos_b = &b[0];
	while (*pos_a)
		if (*pos_a++ != *pos_b++)
			return false;
	return true;
}


void AddToInBuffer(std::string inBuffer) {
	
	std::string nextCurrentBuffer = "";
	//std::string input_string = "firstline\nsecondline\r\n \r\nthirdline\nfourthline\r\n\r\nemptybefore\rotherlinenotended";
	currentBufferString.append(inBuffer);
	std::vector<std::string> newLines; // (optionaly) Here I want to store the results without \r\n
									   //std::regex rx("\r\n|\r|\n");
	//std::regex rx("(?:\\r\\n|\\r|\\n)");
	std::regex rx("(?:\\r\\n|\\n)");
	std::sregex_token_iterator iter(currentBufferString.begin(), currentBufferString.end(), rx, -1);

	for (std::sregex_token_iterator end; iter != end; ++iter)
	{
		std::string newString = iter->str();
		if (newString.compare("\r\n") == 0 || newString.compare("\n") == 0 || newString.compare("\r") == 0 || newString.compare("") == 0) {
			LogToFile("compared");
		}
		else {
			newLines.push_back(newString);
		}
	}

	if (!ends_with(currentBufferString, "\n")) {
		nextCurrentBuffer = newLines[newLines.size() - 1];
		newLines.pop_back();
	}

	for (int i = 0; i < newLines.size(); i++) {
		LogToFile("-----");
		LogToFile(newLines[i].c_str());
		elementsToSend.push(newLines[i]);
		//PluginWrite(.c_str());
		LogToFile("-----");
	}
	
	currentBufferString = nextCurrentBuffer;
	LogToFile("==== restBuffer");
	LogToFile(currentBufferString.c_str());


	isTreatindData = false;
}

// --------------------------------------------------------------------------
// Called when a characteristic value changes!
// --------------------------------------------------------------------------
void  HandleBLENotification(BTH_LE_GATT_EVENT_TYPE EventType, PVOID EventOutParameter, PVOID Context)
{
	LogToFile("new");

	if (isTreatindData) {
		LogToFile("skip");

		return;
	}
	isTreatindData = true;
	PBLUETOOTH_GATT_VALUE_CHANGED_EVENT ValueChangedEventParameters = (PBLUETOOTH_GATT_VALUE_CHANGED_EVENT)EventOutParameter;

	if ((BLERegisteredCharacteristicInfo*)Context == nullptr)
		return;
	auto charInfo = (BLERegisteredCharacteristicInfo*)Context;
	auto charIt = std::find(registeredCharacteristics.begin(), registeredCharacteristics.end(), charInfo);

	if (charIt != registeredCharacteristics.end())
	{
		if (ValueChangedEventParameters->CharacteristicValueDataSize == 0) {
			return;
		}

		int size = (unsigned int)(ValueChangedEventParameters->CharacteristicValue->DataSize);

		for (int i = 0; i < size; i++) {
			inputMessage[i] = ValueChangedEventParameters->CharacteristicValue->Data[i];
		}

		inputMessage[size] = '\0';
		LogToFile(inputMessage);

		//std::string stroutput(reinterpret_cast<const char>(ValueChangedEventParameters->CharacteristicValue->Data), size);

		AddToInBuffer(inputMessage);

	}
	else
	{
		LogToFile(std::string("Received BLE notification for wrong characteristic ").c_str());
		int size = (unsigned int)(ValueChangedEventParameters->CharacteristicValue->DataSize);

		for (int i = 0; i < size; i++) {
			inputMessage[i] = ValueChangedEventParameters->CharacteristicValue->Data[i];
		}
		inputMessage[size] = '\0';

		LogToFile(inputMessage);
		// .append(BLEUtils::BTHLEGUIDToString(charInfo->characteristic.CharacteristicUuid)).append(" which we did not register with.").c_str());
		isTreatindData = false;
	//DebugError(std::string("Received BLE notification for characteristic ").c_str());// .append(BLEUtils::BTHLEGUIDToString(charInfo->characteristic.CharacteristicUuid)).append(" which we did not register with.").c_str());
	}

}


// --------------------------------------------------------------------------
// Subscribe to a characteristic changing values!
// --------------------------------------------------------------------------
void _winBluetoothLESubscribeCharacteristic(const char* address, const char* service, const char* characteristic)
{
	if (address == nullptr)
	{
		DebugError("Null address");
		return;
	}

	if (service == nullptr)
	{
		DebugError("Null service");
		return;
	}

	if (characteristic == nullptr)
	{
		DebugError("Null characteristic");
		return;
	}

	// Find connected service handle
	GUID addressGUID = BLEUtils::StringToGUID(address);
	BTH_LE_UUID serviceGUID = BLEUtils::StringToBTHLEUUID(service);
	auto servIt = std::find_if(connectedServices.begin(), connectedServices.end(), [addressGUID, serviceGUID](BLEConnectedServiceInfo* s) { return s->service->device->containerId == addressGUID && s->service->id == serviceGUID; });
	if (servIt != connectedServices.end())
	{
		// Find characteristic!
		BTH_LE_UUID characteristicGUID = BLEUtils::StringToBTHLEUUID(characteristic);
		auto cservice = *servIt;
		auto charIt = std::find_if(cservice->characteristics.begin(), cservice->characteristics.end(), [characteristicGUID](const BTH_LE_GATT_CHARACTERISTIC& c) { return c.CharacteristicUuid == characteristicGUID; });
		if (charIt != cservice->characteristics.end())
		{
			if (charIt->IsNotifiable)
			{
				// Set up the Client Characteristic Configuration Descriptor, so that we are 'allowed' to receive notifications!

				// Retrieve all descriptors for this characteristic
				auto descs = GetGATTDescriptors(cservice->deviceHandle, &(*charIt));

				// And find the client one
				auto descIt = std::find_if(descs.begin(), descs.end(), [](const BTH_LE_GATT_DESCRIPTOR& d) { return d.DescriptorType == ClientCharacteristicConfiguration; });
				if (descIt != descs.end())
				{
					// Got it, write to it now to indicate we want to be notified!
					BTH_LE_GATT_DESCRIPTOR_VALUE newValue;
					RtlZeroMemory(&newValue, sizeof(newValue));
					newValue.DescriptorType = ClientCharacteristicConfiguration;
					newValue.ClientCharacteristicConfiguration.IsSubscribeToNotification = TRUE;

					// Subscribe to an event.
					HRESULT hr = BluetoothGATTSetDescriptorValue(cservice->deviceHandle, &(*descIt), &newValue, BLUETOOTH_GATT_FLAG_NONE);
					if (hr == S_OK)
					{
						// set the appropriate callback function when the descriptor change value
						auto charInfo = new BLERegisteredCharacteristicInfo();
						charInfo->service = cservice;
						charInfo->characteristic = *charIt;

						BLUETOOTH_GATT_VALUE_CHANGED_EVENT_REGISTRATION EventParameterIn;
						EventParameterIn.Characteristics[0] = *charIt;
						EventParameterIn.NumCharacteristics = 1;
						hr = BluetoothGATTRegisterEvent(
							cservice->deviceHandle,
							CharacteristicValueChangedEvent,
							&EventParameterIn,
							HandleBLENotification,
							charInfo,
							&charInfo->characteristicHandle,
							BLUETOOTH_GATT_FLAG_WRITE_WITHOUT_RESPONSE);
						if (hr == S_OK)
						{
							// Remember we registered with the characteristic
							registeredCharacteristics.push_back(charInfo);

							// Send message
							std::string registerCharacteristicMessage = "SUBSCRIBED!!!DidUpdateNotificationStateForCharacteristic~";
							registerCharacteristicMessage.append(address);
							registerCharacteristicMessage.append("~");
							registerCharacteristicMessage.append(characteristic);
							SendBluetoothMessage(registerCharacteristicMessage.c_str());
						}
						else
						{
							delete charInfo;
							_com_error err(hr);
							DebugError(std::string("Could not register with characteristic ").append(address).append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
						}
					}
					else
					{
						_com_error err(hr);
						DebugError(std::string("Could not set Client Config descriptor value for characteristic ").append(address).append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
					}
				}
				else
				{
					DebugError(std::string("Could not find Client Config descriptor for characteristic ").append(characteristic).c_str());
				}
			}
			else
			{
				DebugError(std::string("Characteristic ").append(characteristic).append(" is not Notifiable.").c_str());
			}
		}
		else
		{
			DebugError(std::string("Could not find characteristic ").append(characteristic).append(" to subscribe to.").c_str());
		}
	}
	else
	{
		DebugError(std::string("Could not find device ").append(address).append(" to subscribe to.").c_str());
	}
}

// --------------------------------------------------------------------------
// Unsubscribe! ;)
// --------------------------------------------------------------------------
void _winBluetoothLEUnSubscribeCharacteristic(const char* address, const char* service, const char* characteristic)
{
	if (address == nullptr)
	{
		DebugError("Null address");
		return;
	}

	if (service == nullptr)
	{
		DebugError("Null service");
		return;
	}

	if (characteristic == nullptr)
	{
		DebugError("Null characteristic");
		return;
	}

	// Find registered characteristic!
	GUID addressGUID = BLEUtils::StringToGUID(address);
	BTH_LE_UUID serviceGUID = BLEUtils::StringToBTHLEUUID(service);
	BTH_LE_UUID characteristicGUID = BLEUtils::StringToBTHLEUUID(characteristic);
	auto charIt = std::find_if(
		registeredCharacteristics.begin(),
		registeredCharacteristics.end(),
		[addressGUID, serviceGUID, characteristicGUID] (BLERegisteredCharacteristicInfo* c)
		{
		// Check device!
		return	c->service->service->device->containerId == addressGUID &&
				c->service->service->id == serviceGUID &&
				c->characteristic.CharacteristicUuid == characteristicGUID;
		});

	if (charIt != registeredCharacteristics.end())
	{
		// Unregister
		auto charInfo = *charIt;
		HRESULT hr = BluetoothGATTUnregisterEvent(charInfo->characteristicHandle, BLUETOOTH_GATT_FLAG_WRITE_WITHOUT_RESPONSE);
		if (hr == S_OK)
		{
			// Clean up
			delete charInfo;
			registeredCharacteristics.erase(charIt);

			// Send message
			std::string registerCharacteristicMessage = "DidUpdateNotificationStateForCharacteristic~";
			registerCharacteristicMessage.append(address);
			registerCharacteristicMessage.append("~");
			registerCharacteristicMessage.append(characteristic);
			SendBluetoothMessage(registerCharacteristicMessage.c_str());
			ClearAll();
		}
		else
		{
			_com_error err(hr);
			DebugError(std::string("Could not unregister from characteristic ").append(address).append(BLEUtils::ToNarrow(err.ErrorMessage())).c_str());
		}
	}
	else
	{
		DebugError(std::string("Could not find notification registration data for characteristic ").append(characteristic).c_str());
	}

	//TODO : that's not cool here...
	ClearAll();

}

// --------------------------------------------------------------------------
// Clean up
// --------------------------------------------------------------------------
void _winBluetoothLEDisconnectAll()
{
	// Disconnect from devices if needed!
	for (auto device : devices)
	{
		DisconnectServicesForDevice(device->containerId);
	}
	devices.clear();
	isTreatindData = false;

}



void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
	Log("Plugin Load");
}

// Unity plugin unload event
void UNITY_INTERFACE_API UnityPluginUnload()
{
	Log("Plugin Unload");
	_winBluetoothLEDeInitialize();
}

BOOL WINAPI DllMain(_In_ HINSTANCE hinstDLL, _In_ DWORD fdwReason, _In_ LPVOID lpvReserved)
{
	switch (fdwReason)
	{
	case DLL_PROCESS_ATTACH:
		Log("DLL_PROCESS_ATTACH");
		break;
	case DLL_PROCESS_DETACH:
		Log("DLL_PROCESS_DETACH");
		break;
	case DLL_THREAD_ATTACH:
		Log("DLL_THREAD_ATTACH");
		break;
	case DLL_THREAD_DETACH:
		Log("DLL_THREAD_DETACH");
		break;
	default:
		break;
	}
	return TRUE;
}

void _threadLoop()
{
	if(devices.size() > 0)
		PluginWriteLoop();
}
