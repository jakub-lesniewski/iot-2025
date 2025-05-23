# Industrial IoT – Quick Start

## Wymagania
- .NET 6+ SDK  
- Azure CLI  
- Azure Functions Core Tools  
- IIoTSim (symulator OPC UA)  
- Azure IoT Hub i Storage Account  

## Kroki uruchomienia
1. **Symulator**  
   - Uruchom `IIoTSim.exe` i dodaj trzy urządzenia: `device1`, `device2`, `device3`.  
2. **Azure Functions**  
   - W `IndustrialIoT.Functions` stwórz `local.settings.json` z kluczami:
     ```jsonc
     {
       "Values": {
         "IoTHubConnectionString": "HostName=…;SharedAccessKey=…",
         "IoTHubEventHubConnectionString": "Endpoint=…;EntityPath=messages/events;…",
         "StorageConnectionString": "DefaultEndpointsProtocol=…",
         "DeviceIdTemplate": "device{id}"
       }
     }
     ```
   - Uruchom `func start` lub F5 w VS (profil Azure Functions).  
3. **Agent**  
   - W każdym folderze `agent-1`, `agent-2`, `agent-3` zmień w `appsettings.json` DeviceId i ConnectionString, potem:
     ```bash
     dotnet run
     ```
4. **Weryfikacja**  
   - Sprawdź w IoT Explorer: napływ telemetrii, aktualizacje Device Twin, logi Functions.  
